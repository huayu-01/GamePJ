using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>厌恶：点按一圈，蓄力后定距前冲并持续旋转；各段共用出手快照。</summary>
public partial class ReapingSpinAbility : EvolvedAttackAbility
{
	[Export] public float TapDamage { get; set; } = 32;
	[Export] public float DashDamage { get; set; } = 16;
	[Export] public float SpinDamage { get; set; } = 10;
	[Export] public float BaseDashDistance { get; set; } = 3;
	[Export] public float DashDistancePerStep { get; set; } = 3;
	[Export] public float DashSpeed { get; set; } = 18;
	[Export] public float BaseSpinSeconds { get; set; } = 1;
	[Export] public float SpinSecondsPerStep { get; set; } = 1;
	[Export] public float HitInterval { get; set; } = .5f;
	public float LastDashDistance { get; private set; }
	public float LastSpinSeconds { get; private set; }
	public int PulseCount { get; private set; }
	public float CurrentAttackRadius => EffectiveRadius;
	private enum Phase { Charging, Tap, Dash, Spin }
	private Phase _phase;
	private float _remaining, _pulse, _powerDenominator;
	private Node3D? _model; private Vector3 _modelRotation;
	private readonly HashSet<Unit> _dashHits = new(); private AttackInstance? _dashAttack;
	protected override void OnActivate(Unit? target) { _phase = Phase.Charging; _dashHits.Clear(); PulseCount = 0; }
	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		Snapshot(); var owner = OwnerUnit!; _model = ActiveModel; _modelRotation = _model?.Rotation ?? Vector3.Zero;
		StateTime = 0;
		if (LastReleaseEarly)
		{
			_phase = Phase.Tap; _remaining = .25f; LastDashDistance = 0; LastSpinSeconds = .25f;
			var attack = Attack(TapDamage); foreach (var enemy in Enemies(Targeting.AimPoint(owner), EffectiveRadius)) Strike(enemy, attack); PulseCount++;
			return;
		}
		LastDashDistance = BaseDashDistance + Mathf.Clamp(steps, 0, 3) * DashDistancePerStep;
		LastSpinSeconds = BaseSpinSeconds + Mathf.Clamp(steps, 0, 3) * SpinSecondsPerStep;
		int ticks = Mathf.Max(1, Mathf.CeilToInt(LastSpinSeconds / Mathf.Max(.05f, HitInterval)));
		_powerDenominator = Mathf.Max(.01f, DashDamage + SpinDamage * ticks);
		_dashAttack = Attack(DashDamage, DashDamage / _powerDenominator);
		Vector3 direction = ResolveAimDirection(); direction.Y = 0; direction = direction.LengthSquared() > .001f ? direction.Normalized() : -owner.GlobalBasis.Z;
		_phase = Phase.Dash;
		if (Movement?.BeginVelocityOverride(this, direction * DashSpeed, LastDashDistance / Mathf.Max(.1f, DashSpeed), isDash: true, planarOnly: true) != true) BeginSpin();
	}
	private void BeginSpin() { Movement?.EndVelocityOverride(this); _phase = Phase.Spin; _remaining = LastSpinSeconds; _pulse = 0; }
	protected override void OnChargeAborted() => Finish();
	protected override void Tick(float delta)
	{
		if (!IsActive) return;
		if (_phase == Phase.Charging) return;
		if (IsInstanceValid(_model)) _model!.RotateY(delta * (_phase == Phase.Tap ? Mathf.Tau / .25f : Mathf.Tau * 2));
		if (_phase == Phase.Dash)
		{
			foreach (var enemy in Enemies(Targeting.AimPoint(OwnerUnit!), EffectiveRadius)) if (_dashHits.Add(enemy)) Strike(enemy, _dashAttack!);
			if (Movement?.HasVelocityOverride != true || Movement.VelocityOverrideFinished(this)) BeginSpin();
			return;
		}
		_remaining -= delta; if (_remaining <= 0) { Finish(); return; }
		if (_phase == Phase.Tap) return;
		_pulse -= delta; if (_pulse > 0) return; _pulse += Mathf.Max(.05f, HitInterval); PulseCount++;
		var attack = Attack(SpinDamage, SpinDamage / _powerDenominator);
		foreach (var enemy in Enemies(Targeting.AimPoint(OwnerUnit!), EffectiveRadius)) Strike(enemy, attack);
	}
	protected override void OnFinishOrCancel()
	{
		Movement?.EndVelocityOverride(this); if (IsInstanceValid(_model)) _model!.Rotation = _modelRotation;
		_model = null; _dashHits.Clear(); _dashAttack = null; base.OnFinishOrCancel();
	}
	public override void _ExitTree() { OnFinishOrCancel(); base._ExitTree(); }
}
