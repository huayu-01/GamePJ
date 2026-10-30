using System;
using ActRoguelike.Combat;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Abilities;

/// <summary>新旧攻击共用伤害快照，避免抽执行器时改变乘区或事件标签。</summary>
internal static class EnemyAttackFactory
{
	public static AttackInstance Create(Unit owner, string abilityId, float damage, float radius = 0, bool bomb = false)
	{
		float scaled = damage * owner.Stats.Get("DamageMultiplier");
		return new AttackInstance { Attacker = owner, Ability = abilityId, AbilityKind = abilityId,
			BaseDamage = bomb ? 0 : scaled, ExplosionDamage = scaled, ExplosionRadius = bomb ? radius : 0,
			Knockback = 3, Lifetime = 12, Tags = bomb ? new[] { "Projectile", "Explosion" } : new[] { "EnemyAttack" } };
	}
}

/// <summary>命中体和移动覆盖按本次执行实例归属，取消时当帧停伤害。</summary>
public abstract class EnemyHitboxExecution : EnemyActionExecution
{
	protected EnemyActionState Step { get; }
	protected bool Contact { get; private set; }
	private AttackHitbox? _hitbox;
	protected EnemyHitboxExecution(EnemyActionContext context) : base(context) => Step = context.Step ?? throw new ArgumentException("攻击执行器需要本次动作快照。");
	public override void Begin()
	{
		_hitbox = new AttackHitbox { Position = Vector3.Up * .8f + (Step.Kind == Kind.Spin || Step.Centered ? Vector3.Zero : Vector3.Forward * .7f) };
		_hitbox.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Mathf.Max(.2f, Step.Radius) } });
		Context.Owner.AddChild(_hitbox); _hitbox.HitLanded += _ => Contact = true;
		_hitbox.Activate(Context.Owner, EnemyAttackFactory.Create(Context.Owner, Context.AbilityId, Step.Damage));
		if (!IsDisposed) Sweep();
	}
	protected void Sweep()
	{
		if (_hitbox != null && GodotObject.IsInstanceValid(_hitbox)) _hitbox.SweepSphere(_hitbox.GlobalPosition, _hitbox.GlobalPosition);
	}
	protected override void End()
	{
		if (GodotObject.IsInstanceValid(Context.Owner.Movement)) Context.Owner.Movement!.EndVelocityOverride(this);
		if (GodotObject.IsInstanceValid(_hitbox)) { _hitbox!.Deactivate(); _hitbox.QueueFree(); } _hitbox = null;
	}
}

public sealed class EnemyMeleeExecution : EnemyHitboxExecution
{
	private readonly Node3D? _visual;
	private readonly Vector3 _rotation;
	private float _elapsed;
	public EnemyMeleeExecution(EnemyActionContext context) : base(context)
	{
		// Model 是单位外层表现根约定，不依赖内部骨架或骨骼名。
		_visual = context.Owner.GetNodeOrNull<Node3D>("Model"); _rotation = _visual?.Rotation ?? Vector3.Zero;
	}
	public override void Tick(float delta)
	{
		_elapsed += delta;
		if (Step.Speed > 0) Context.Owner.Movement?.BeginVelocityOverride(this, Context.Heading * Step.Speed, -1, true);
		if (Step.Kind == Kind.Spin && GodotObject.IsInstanceValid(_visual)) _visual!.Rotation = _rotation + Vector3.Up * Mathf.Tau * Mathf.Clamp(_elapsed / Mathf.Max(.01f, Step.ActiveSeconds), 0, 1);
		Sweep();
	}
	protected override void End() { base.End(); if (GodotObject.IsInstanceValid(_visual)) _visual!.Rotation = _rotation; }
}

public sealed class EnemyChargeExecution : EnemyHitboxExecution
{
	private bool _complete;
	public override bool IsComplete => _complete;
	public EnemyChargeExecution(EnemyActionContext context) : base(context) { }
	public override void Tick(float delta)
	{
		Unit? target = Context.Target();
		if (Step.Kind == Kind.HomingCharge && GodotObject.IsInstanceValid(target))
		{
			Vector3 desired = target!.GlobalPosition - Context.Owner.GlobalPosition; desired.Y = 0;
			if (desired.LengthSquared() > .001f) Context.Heading = Context.Heading.Slerp(desired.Normalized(), Mathf.Min(1, Mathf.DegToRad(Step.TurnDegreesPerSecond) * delta / Mathf.Max(.001f, Context.Heading.AngleTo(desired.Normalized())))).Normalized();
		}
		Context.Owner.Movement?.FaceDirection(Context.Heading, delta);
		Context.Owner.Movement?.BeginVelocityOverride(this, Context.Heading * Step.Speed, -1, true);
		// 与旧路径保持同一碰撞完成时机：本次 Sweep 的命中事实在下一次 Tick 判定。
		_complete = Contact || Context.Owner.GlobalPosition.DistanceTo(Step.StartPosition) >= Step.TravelDistance
			|| (Step.Kind == Kind.HomingCharge && GodotObject.IsInstanceValid(target) && Context.Owner.GlobalPosition.DistanceTo(target!.GlobalPosition) <= Step.Radius);
		Sweep();
	}
}
