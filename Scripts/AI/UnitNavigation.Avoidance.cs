using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.AI;

public partial class UnitNavigation
{
	private EnemyCrowdConfig _crowd = null!;
	private Rid _agent, _avoidanceMap;
	private Vector3 _safeVelocity, _previousPosition;
	private bool _hasSafeVelocity;
	private float _sizeTimer, _radius = .45f, _height = 1.8f;
	private uint _defaultMask;
	public bool HasAvoidanceAgent => _agent.IsValid;
	public Rid AvoidanceMap => _avoidanceMap;
	public float AvoidanceRadius => _radius;
	internal void RefreshCrowd()
	{
		uint nextMask = EnemyCrowdConfig.BodyMaskFor(_unit);
		// 只改通常身体掩码，不抢占技能临时穿透；玩家与飞行单位继续原规则。
		if (_unit.CollisionMask == CombatLayers.BodyMask || _unit.CollisionMask == _defaultMask) _unit.CollisionMask = nextMask;
		_defaultMask = nextMask;
		ReleaseAgent(); _sizeTimer = 0;
	}
	private void ReleaseAgent()
	{
		if (_agent.IsValid) { NavigationServer3D.AgentSetAvoidanceCallback(_agent, default); NavigationServer3D.FreeRid(_agent); }
		_agent = _avoidanceMap = default; _hasSafeVelocity = false;
	}
	private void OnDied(HitInstance hit) => ReleaseAgent();
	private void UpdateAvoidance(float delta)
	{
		using var profile = ActRoguelike.Core.RuntimeFrameProfiler.Measure("AvoidanceSync");
		var map = MapManager.Instance;
		if (_crowd.Mode != EnemyCrowdMode.Avoidance || !EnemyCrowdConfig.IsGroundEnemy(_unit) || _unit.IsDead || map == null || !map.IsInsideTree())
		{
			if (_agent.IsValid) ReleaseAgent(); return;
		}
		Rid shared = map.CrowdMap;
		if (!_agent.IsValid)
		{
			_agent = NavigationServer3D.AgentCreate(); _avoidanceMap = shared;
			NavigationServer3D.AgentSetMap(_agent, shared); NavigationServer3D.AgentSetUse3DAvoidance(_agent, false);
			NavigationServer3D.AgentSetAvoidanceEnabled(_agent, true);
			NavigationServer3D.AgentSetAvoidanceLayers(_agent, 1); NavigationServer3D.AgentSetAvoidanceMask(_agent, 1);
			NavigationServer3D.AgentSetNeighborDistance(_agent, Mathf.Max(1f, _crowd.NeighborDistance));
			NavigationServer3D.AgentSetMaxNeighbors(_agent, Mathf.Max(1, _crowd.MaxNeighbors));
			NavigationServer3D.AgentSetTimeHorizonAgents(_agent, Mathf.Max(.05f, _crowd.TimeHorizon));
			NavigationServer3D.AgentSetAvoidanceCallback(_agent, Callable.From<Vector3>(OnSafeVelocity));
			_previousPosition = _unit.GlobalPosition; _sizeTimer = 0;
		}
		else if (_avoidanceMap != shared) { _avoidanceMap = shared; NavigationServer3D.AgentSetMap(_agent, shared); _hasSafeVelocity = false; }
		_sizeTimer -= delta;
		if (_sizeTimer <= 0)
		{
			_sizeTimer = .3f;
			(_radius, _height) = MapManager.BodySize(_unit);
			_radius *= Mathf.Max(1f, _crowd.RadiusScale);
			NavigationServer3D.AgentSetRadius(_agent, _radius); NavigationServer3D.AgentSetHeight(_agent, _height);
		}
		Vector3 position = _unit.GlobalPosition;
		if (position.DistanceSquaredTo(_previousPosition) > 9) { NavigationServer3D.AgentSetVelocityForced(_agent, Vector3.Zero); _hasSafeVelocity = false; }
		_previousPosition = position; NavigationServer3D.AgentSetPosition(_agent, position);
		var movement = _unit.Movement;
		bool forced = movement == null || movement.IsDashing || movement.HasVelocityOverride || movement.IsControlDisabled
			|| movement.IsActionMovementLocked || _unit.HasActionLock || _unit.ImpactPause?.IsHolding == true || movement.IntentDirection.IsZeroApprox();
		NavigationServer3D.AgentSetAvoidancePriority(_agent, forced ? 1f : .5f);
		if (forced)
		{
			Vector3 actual = movement?.IsDashing == true || movement?.HasVelocityOverride == true ? _unit.Velocity : Vector3.Zero;
			actual.Y = 0; NavigationServer3D.AgentSetVelocity(_agent, actual); _hasSafeVelocity = false;
		}
	}
	private void OnSafeVelocity(Vector3 velocity)
	{
		// Server回调不受SceneTree暂停自动约束，只缓存水平结果，不在回调中移动单位。
		if (!IsInsideTree() || !CanProcess() || _unit.IsDead || !velocity.IsFinite()) return;
		velocity.Y = 0; _safeVelocity = velocity; _hasSafeVelocity = true;
	}
	public Vector3 Avoid(Vector3 desired)
	{
		if (!_agent.IsValid || _crowd.Mode != EnemyCrowdMode.Avoidance) return desired;
		desired.Y = 0;
		NavigationServer3D.AgentSetMaxSpeed(_agent, Mathf.Max(.01f, desired.Length()));
		NavigationServer3D.AgentSetVelocity(_agent, desired);
		return desired.IsZeroApprox() || !_hasSafeVelocity ? desired : _safeVelocity.LimitLength(desired.Length());
	}
	public override void _Notification(int what)
	{
		if (!_agent.IsValid) return;
		if (what == NotificationPaused || what == NotificationDisabled) NavigationServer3D.AgentSetPaused(_agent, true);
		else if (what == NotificationUnpaused || what == NotificationEnabled) { NavigationServer3D.AgentSetPaused(_agent, !CanProcess()); _hasSafeVelocity = false; }
	}
	public override void _ExitTree()
	{
		ReleaseAgent();
		if (IsInstanceValid(_crowd)) _crowd.Changed -= RefreshCrowd;
		if (IsInstanceValid(_unit)) _unit.Died -= OnDied;
	}
}
