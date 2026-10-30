using Godot;

namespace ActRoguelike.Combat;

public partial class Projectile
{
	private Unit? _curveTarget;
	private Vector3 _curveStart, _curveControl, _curveApproach;
	private float _curveTime, _curveDuration, _curveSpeed, _curveHandle;
	public bool IsHomingCurveActive => _curveTarget != null;
	public Vector3 HomingAimPoint { get; private set; }
	public float HomingStartedAtDistance { get; private set; } = -1f;

	private void ResetHomingCurve()
	{
		_curveTarget = null;
		_curveTime = 0;
	}

	/// <summary>取真实身体碰撞形状中心，兼容脚底原点、中心原点与缩放后的精英体型。</summary>
	public static Vector3 GetTargetCenter(Unit target)
	{
		foreach (Node child in target.GetChildren())
			if (child is CollisionShape3D { Disabled: false, Shape: not null } shape)
				return shape.GlobalPosition;
		return target.GlobalPosition + Vector3.Up * .9f;
	}

	private bool CanHomeTo(Unit? target)
	{
		Unit? owner = _attack?.Attacker;
		return owner != null && IsInstanceValid(owner) && target != null && IsInstanceValid(target)
			&& target.IsValidTarget && target.Health > 0 && owner.IsHostileTo(target)
			&& !_hitTargets.Contains(target.GetInstanceId())
			&& (AssignedTargetOnly ? target == AssignedHomingTarget : SeekAnyHostile || target.HasStatus(HomingStatusId))
			&& (HomingRange <= 0 || GlobalPosition.DistanceSquaredTo(GetTargetCenter(target)) <= HomingRange * HomingRange);
	}

	private Unit? FindHomingTarget()
	{
		if (AssignedTargetOnly) return CanHomeTo(AssignedHomingTarget) ? AssignedHomingTarget : null;
		if (SeekAnyHostile && CanHomeTo(AssignedHomingTarget)) return AssignedHomingTarget;
		Unit? owner = _attack?.Attacker;
		if (owner == null || !IsInstanceValid(owner)) return null;
		Unit? best = null;
		float distance = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup(owner.Faction == Faction.Player ? "enemy_units" : "player_units"))
		{
			if (node is not Unit candidate || !CanHomeTo(candidate)) continue;
			float next = GlobalPosition.DistanceSquaredTo(GetTargetCenter(candidate));
			if (next < distance) { distance = next; best = candidate; }
		}
		return best;
	}

	private bool FollowHomingCurve(float delta)
	{
		Unit? target = CanHomeTo(_curveTarget) ? _curveTarget : FindHomingTarget();
		if (target == null || _velocity.LengthSquared() < .0001f)
		{
			ResetHomingCurve();
			return false;
		}
		HomingAimPoint = GetTargetCenter(target);
		HomingTargetName = target.Name;
		if (_curveTarget != target || _curveTime >= _curveDuration)
		{
			_curveTarget = target;
			_curveStart = GlobalPosition;
			_curveSpeed = Mathf.Max(1f, ActualSpeed);
			float distance = _curveStart.DistanceTo(HomingAimPoint);
			_curveHandle = Mathf.Max(.15f, distance * .35f);
			_curveControl = _curveStart + VelocityDirection * _curveHandle;
			_curveApproach = (HomingAimPoint - _curveControl).Normalized();
			_curveTime = 0;
			// 用控制折线估计时长，保留出手切线且避免急转时瞬间横向跳跃。
			Vector3 endControl = HomingAimPoint - _curveApproach * _curveHandle;
			_curveDuration = Mathf.Max(.06f, (_curveStart.DistanceTo(_curveControl)
				+ _curveControl.DistanceTo(endControl) + endControl.DistanceTo(HomingAimPoint)) / _curveSpeed);
			if (HomingStartedAtDistance < 0) HomingStartedAtDistance = TravelledDistance;
		}
		_curveTime = Mathf.Min(_curveDuration, _curveTime + delta);
		float t = _curveTime / _curveDuration;
		Vector3 p2 = HomingAimPoint - _curveApproach * _curveHandle;
		Vector3 next = CubicBezier(_curveStart, _curveControl, p2, HomingAimPoint, t);
		Vector3 motion = next - GlobalPosition;
		LastHomingWeight = t;
		LastHomingAngleDegrees = motion.LengthSquared() > .000001f ? Mathf.RadToDeg(_velocity.AngleTo(motion)) : 0f;
		// 移动目标会更新终点；位移仍受原速度与全局上限约束，沿每帧路径做真实碰撞查询。
		_velocity = motion / Mathf.Max(.00001f, delta);
		if (_velocity.LengthSquared() > _curveSpeed * _curveSpeed) _velocity = _velocity.Normalized() * _curveSpeed;
		return true;
	}

	private static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
	{
		float u = 1f - t;
		return a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
	}
}
