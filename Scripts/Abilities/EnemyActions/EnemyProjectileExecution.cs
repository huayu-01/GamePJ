using System;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Abilities;

/// <summary>预警与实际发射共用弹道初值；全局限速仍由 Projectile 应用。</summary>
public readonly record struct EnemyProjectileMotion(Vector3 Origin, Vector3 Velocity, float GravityScale)
{
	public static EnemyProjectileMotion Resolve(EnemyActionState step, Unit owner, Unit target)
	{
		Vector3 origin = owner.GlobalPosition + Vector3.Up;
		Vector3 destination = target.GlobalPosition + Vector3.Up * .8f;
		Vector3 velocity = (destination - origin).Normalized() * step.Speed;
		float gravity = 0;
		if (step.Kind is Kind.ArcShot or Kind.ArcBomb)
		{
			gravity = 1.5f; float flight = Mathf.Max(.2f, step.FlightSeconds);
			velocity = (destination - origin) / flight + Vector3.Up * ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * gravity * flight * .5f;
		}
		if (step.Kind == Kind.Meteor) { origin = target.GlobalPosition + Vector3.Up * 8; velocity = Vector3.Down * step.Speed; gravity = 1; }
		return new(origin, velocity, gravity);
	}
}

public sealed class EnemyProjectileExecution : EnemyActionExecution
{
	private readonly EnemyActionState _step;
	private bool _launched;
	public EnemyProjectileExecution(EnemyActionContext context) : base(context) => _step = context.Step ?? throw new ArgumentException("发射动作需要本次参数快照。");
	public override void Begin()
	{
		if (IsDisposed || _launched || Context.Target() is not { } target || !GodotObject.IsInstanceValid(target)) return;
		_launched = true;
		Launch(Context.Owner, target, _step, Context.AbilityId);
	}
	/// <summary>旧 Kind 路径复用同一发射实现；结束动作不会撤销已独立发射的弹体。</summary>
	internal static void Launch(Unit owner, Unit target, EnemyActionState step, string abilityId)
	{
		bool bomb = step.Kind is Kind.Bomb or Kind.ArcBomb or Kind.Meteor;
		bool eligible = step.Kind == Kind.Shot && step.TurnDegreesPerSecond <= 0;
		var elitePlan = ActRoguelike.Combat.Elites.EliteProjectileRules.GetPlan(owner, eligible, 1);
		var motion = EnemyProjectileMotion.Resolve(step, owner, target);
		var attack = ActRoguelike.Combat.Elites.EliteProjectileRules.WithDamage(
			EnemyAttackFactory.Create(owner, abilityId, step.Damage, step.Radius, bomb), elitePlan.DamageScale, elitePlan.Count, true);
		for (int i = 0; i < elitePlan.Count; i++)
		{
			var projectile = (step.ProjectileScene ?? RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Bullet.tscn")).Instantiate<Projectile>();
			projectile.Pierce = 0; projectile.MaxTravelDistance = step.TravelDistance;
			projectile.DistanceEnd = bomb ? Projectile.DistanceEndMode.Explode : Projectile.DistanceEndMode.Disappear;
			projectile.ExplosionOnly = bomb; projectile.QueryGeometry = 1;
			projectile.SweepRadius = step.Kind == Kind.Meteor ? .7f : bomb ? .2f : .1f;
			if (step.ProjectileScene == null) projectile.AddChild(new MeshInstance3D { Mesh = new SphereMesh
			{
				Radius = projectile.SweepRadius, Height = projectile.SweepRadius * 2,
				Material = new StandardMaterial3D { AlbedoColor = bomb ? new Color(1, .35f, .1f) : new Color(.8f, .3f, 1), EmissionEnabled = true, Emission = new Color(.7f, .2f, .1f) }
			} });
			projectile.GravityScale = motion.GravityScale; projectile.ShowImpactWarning = bomb;
			// 场景负责外观；重置模式参数，避免换弹体后混入其预设追踪或延迟重力。
			projectile.GravityStartDistance = 0; projectile.HomingStartDistance = 0; projectile.BezierHoming = false;
			projectile.HomingStatusId = string.Empty; projectile.HomingRange = 0;
			projectile.SeekAnyHostile = step.Kind == Kind.Shot;
			projectile.AssignedHomingTarget = step.Kind == Kind.Shot ? target : null;
			projectile.HomingTurnRateDegrees = step.Kind == Kind.Shot ? step.TurnDegreesPerSecond : 0;
			projectile.Speed = motion.Velocity.Length();
			(owner.GetTree().CurrentScene ?? owner.GetTree().Root).AddChild(projectile); projectile.GlobalPosition = motion.Origin;
			ActRoguelike.Combat.Elites.EliteProjectileRules.Configure(owner, projectile, target, eligible);
			float angle = elitePlan.Count <= 1 ? 0 : Mathf.Lerp(-elitePlan.SpreadDegrees * .5f, elitePlan.SpreadDegrees * .5f, (float)i / (elitePlan.Count - 1));
			projectile.Launch(attack, ActRoguelike.Combat.Elites.EliteProjectileRules.SpreadDirection(motion.Velocity, angle));
		}
	}
}
