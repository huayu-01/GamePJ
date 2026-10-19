using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

public partial class CharacterMovement
{
	public enum FlightPhase { Ground, Flying, Falling, Recovering }
	[Export] public UnitPhysicsProfile? PhysicsProfile { get; set; }
	public bool CanFly => PhysicsProfile?.CanFly == true;
	public bool IsControlDisabled => FlightState is FlightPhase.Falling or FlightPhase.Recovering;
	public bool IsControlImmune => PhysicsProfile?.ControlImmune == true;
	public float Mass => Mathf.Max(0.01f, PhysicsProfile?.Mass ?? 1f);
	public FlightPhase FlightState { get; private set; }
	public float EffectiveHoverHeight { get; private set; }
	public Vector3 ExternalVelocity => _externalVelocity;
	public event Action<FlightPhase>? FlightStateChanged;
	/// <summary>实际打落着地事实，数值为碰撞前向下速度；不自动伤害或触发主动跳跃。</summary>
	public event Action<float>? KnockdownLanded;
	private sealed record TimedForce(Vector3 Value, float Remaining);
	private sealed record HeightLimit(float Height, float Remaining);
	private readonly Dictionary<object, TimedForce> _forces = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<object, HeightLimit> _heightLimits = new(ReferenceEqualityComparer.Instance);
	private readonly List<object> _physicsKeys = new();
	private float _previousExternalY, _recoveryLeft, _lastGroundY;
	private bool _flightInitialized;

	/// <summary>换装／运动模式切换时清理临时受力和控制；不修改资源或阵营。</summary>
	public void ConfigurePhysics(UnitPhysicsProfile? profile)
	{
		PhysicsProfile = profile;
		_forces.Clear(); _heightLimits.Clear();
		_externalVelocity = Vector3.Zero; _previousExternalY = 0f;
		_recoveryLeft = 0f; _flightInitialized = false;
		if (IsInstanceValid(_body)) _body.Velocity = _body.Velocity with { Y = 0f };
		SetFlightState(CanFly ? FlightPhase.Flying : FlightPhase.Ground);
	}

	/// <summary>真实冲量（质量×速度），与旧AddImpulse的速度增量接口区分。</summary>
	public void ApplyPhysicalImpulse(Vector3 impulse) => AddImpulse(impulse / Mass);
	/// <summary>来源拥有的持续力；同来源刷新，不同来源相加。环境力不走战斗免疫。</summary>
	public void SetForce(object source, Vector3 force, float seconds)
	{
		if (!float.IsFinite(seconds) || seconds <= 0f || !force.IsFinite()) { _forces.Remove(source); return; }
		_forces[source] = new(force, seconds);
	}
	public void RemoveForce(object source) => _forces.Remove(source);
	public bool SuppressFlightHeight(object source, float height, float seconds)
	{
		if (!CanFly || IsControlImmune || !float.IsFinite(height) || !float.IsFinite(seconds) || seconds <= 0f) return false;
		_heightLimits[source] = new(Mathf.Max(0f, height), seconds);
		return true;
	}
	public void RemoveHeightSuppression(object source) => _heightLimits.Remove(source);
	public bool TryKnockDown(float downwardSpeed)
	{
		if (!CanFly || IsControlImmune || PhysicsProfile?.CanBeKnockedDown == false || !float.IsFinite(downwardSpeed) || downwardSpeed <= 0f) return false;
		if (!_flightInitialized) { _flightInitialized = true; _lastGroundY = _body.GlobalPosition.Y - PhysicsProfile!.HoverHeight; }
		// 硬控制走能力原有收尾，释放技能持有的无敌／碰撞Mask／速度覆盖。
		foreach (var ability in _movementAbilities.ToArray())
			if (IsInstanceValid(ability)) ability.InterruptByControl();
		if (_velocityOverrideSource != null) EndVelocityOverride(_velocityOverrideSource);
		EndDash(); Stop(); _inputVelocity = Vector3.Zero;
		_externalVelocity.Y = 0f; _previousExternalY = 0f;
		SetVerticalVelocity(-downwardSpeed);
		_recoveryLeft = 0f;
		SetFlightState(FlightPhase.Falling);
		return true;
	}

	private void SetFlightState(FlightPhase phase)
	{
		if (FlightState == phase) return;
		FlightState = phase; FlightStateChanged?.Invoke(phase);
	}
	private void StepPhysicsEffects(float dt)
	{
		if (!_flightInitialized)
		{
			_flightInitialized = true;
			_lastGroundY = _body.GlobalPosition.Y - (PhysicsProfile?.HoverHeight ?? 0f);
			SetFlightState(CanFly ? FlightPhase.Flying : FlightPhase.Ground);
		}
		// 精确消费剩余时间，避免末帧把0.001秒持续力算成一整帧。
		Vector3 deltaVelocity = Vector3.Zero;
		_physicsKeys.Clear(); _physicsKeys.AddRange(_forces.Keys);
		foreach (object key in _physicsKeys)
		{
			var entry = _forces[key];
			deltaVelocity += entry.Value / Mass * Mathf.Min(dt, entry.Remaining);
			if (entry.Remaining <= dt) _forces.Remove(key);
			else _forces[key] = entry with { Remaining = entry.Remaining - dt };
		}
		// 已有速度先衰减，再累计本帧持续力；默认地面水平击退保留原衰减公式。
		_externalVelocity *= Mathf.Max(0f, 1f - ImpulseDamping * dt);
		_externalVelocity += deltaVelocity;
		EffectiveHoverHeight = Mathf.Max(0f, PhysicsProfile?.HoverHeight ?? 0f);
		_physicsKeys.Clear(); _physicsKeys.AddRange(_heightLimits.Keys);
		foreach (object key in _physicsKeys)
		{
			var entry = _heightLimits[key];
			EffectiveHoverHeight = Mathf.Min(EffectiveHoverHeight, entry.Height);
			if (entry.Remaining <= dt) _heightLimits.Remove(key);
			else _heightLimits[key] = entry with { Remaining = entry.Remaining - dt };
		}
		if (FlightState == FlightPhase.Recovering)
		{
			// 离开平台后重新落地才恢复计时，空中绝不自动起飞。
			if (!_body.IsOnFloor()) { SetFlightState(FlightPhase.Falling); return; }
			_recoveryLeft -= dt;
			if (_recoveryLeft <= 0f) SetFlightState(FlightPhase.Flying);
		}
	}
	private bool ApplyFlightLift(ref Vector3 velocity, float dt)
	{
		if (!CanFly || FlightState != FlightPhase.Flying || PhysicsProfile is not { } profile) return false;
		var origin = _body.GlobalPosition + Vector3.Up * 0.1f;
		var query = PhysicsRayQueryParameters3D.Create(origin, origin + Vector3.Down * Mathf.Max(1f, profile.GroundProbeDistance), CombatLayers.World);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0 && hit["normal"].AsVector3().Y >= 0.7f) _lastGroundY = hit["position"].AsVector3().Y;
		float error = _lastGroundY + EffectiveHoverHeight - _body.GlobalPosition.Y;
		float cap = Mathf.Max(0f, profile.MaxVerticalSpeed);
		float desired = Mathf.Clamp(error * Mathf.Max(0f, profile.HeightResponse), -cap, cap);
		velocity.Y = Mathf.MoveToward(velocity.Y, desired, Mathf.Max(0f, profile.LiftAcceleration) * dt);
		return true;
	}
	private void FinishPhysicsMove(float downwardSpeed)
	{
		// 接触面消去朝墙／地板内的残留外力，离墙后不释放蓄积的旧推力。
		for (int i = 0; i < _body.GetSlideCollisionCount(); i++)
		{
			Vector3 normal = _body.GetSlideCollision(i).GetNormal();
			if (_externalVelocity.Dot(normal) < 0f) _externalVelocity = _externalVelocity.Slide(normal);
		}
		_previousExternalY = Mathf.IsZeroApprox(_body.Velocity.Y) ? 0f : _externalVelocity.Y;
		if (FlightState != FlightPhase.Falling || !_body.IsOnFloor()) return;
		_recoveryLeft = Mathf.Max(0f, PhysicsProfile?.GroundRecoverySeconds ?? 0f);
		SetFlightState(FlightPhase.Recovering);
		KnockdownLanded?.Invoke(downwardSpeed);
	}
}
