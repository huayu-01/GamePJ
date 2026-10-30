using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>消费本轮首枚有效命中一次；组成段不会再生成刀舞，待发部分随目标死亡取消。</summary>
public partial class KnifeDanceFollowup : Node, IStoppableRewardRule
{
	public Unit OwnerUnit { get; set; } = null!;
	public AttackInstance PrimaryAttack { get; set; } = null!;
	public PackedScene ProjectileScene { get; set; } = null!;
	public float Damage { get; set; } = 8;
	public int Count { get; set; } = 3;
	public float Speed { get; set; } = 32;
	public float Interval { get; set; } = .08f;
	public float HomingDelay { get; set; } = .15f;
	public float Lifetime { get; set; } = 1.5f;
	public float PowerShare { get; set; }
	public int FiredCount { get; private set; }
	private Unit? _target; private float _age, _timer; private bool _triggered, _stopped;
	public override void _Ready() { GameplayEventBus.Raised += OnFact; SetProcess(false); }
	private void OnFact(GameplayEvent fact)
	{
		if (_stopped || _triggered || fact.Kind != GameplayEventKind.Damaged || fact.Source != OwnerUnit || fact.Attack != PrimaryAttack || fact.DamageApplied <= 0
			|| fact.Target is not { IsValidTarget: true } target || !OwnerUnit.IsHostileTo(target)) return;
		_triggered = true; _target = target; _timer = 0;
		if (StatusPresets.Create("Mark", 6) is { } mark) target.ApplyStatus(mark, OwnerUnit, PrimaryAttack.AbilityKind);
	}
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta; _age += dt;
		if (_stopped || !IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead || _age > Lifetime + .5f || (_triggered && (!IsInstanceValid(_target) || _target is not { IsValidTarget: true }))) { Stop(); QueueFree(); return; }
		if (!_triggered) return; _timer -= dt; if (_timer > 0) return;
		_timer = Mathf.Max(.02f, Interval); FiredCount++;
		var shot = ProjectileScene.Instantiate<Projectile>(); shot.Speed = Speed; shot.GravityScale = 0; shot.BezierHoming = true;
		shot.AssignedTargetOnly = true; shot.AssignedHomingTarget = _target; shot.HomingTurnRateDegrees = 360; shot.HomingRange = 40; shot.HomingStartDistance = Speed * HomingDelay;
		Vector3 forward = -OwnerUnit.GlobalBasis.Z;
		Vector3 direction = new Vector3((float)GD.RandRange(-1d, 1d), (float)GD.RandRange(.1d, .5d), (float)GD.RandRange(.15d, .8d)).Normalized();
		Vector3 origin = Targeting.AimPoint(OwnerUnit) - forward * .45f;
		var attack = new AttackInstance { Attacker = OwnerUnit, Ability = PrimaryAttack.Ability, AbilityKind = PrimaryAttack.AbilityKind,
			CastId = PrimaryAttack.CastId, BaseDamage = Damage, IsSecondary = true, IsWeaponSkillComponent = true,
			CanCrit = PrimaryAttack.CanCrit, CritChance = PrimaryAttack.CritChance, CritMultiplier = PrimaryAttack.CritMultiplier,
			PowerDamageSnapshotOverride = PrimaryAttack.CapturedAttackPower, PowerDamageWeight = PowerShare, Lifetime = Lifetime };
		GetTree().CurrentScene.AddChild(shot); shot.GlobalPosition = origin; shot.Launch(attack, direction);
		if (FiredCount >= Count) { Stop(); QueueFree(); }
	}
	public void Stop() { _stopped = true; GameplayEventBus.Raised -= OnFact; SetPhysicsProcess(false); }
	public override void _ExitTree() => Stop();
}
