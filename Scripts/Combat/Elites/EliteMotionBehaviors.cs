using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Config;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>投射物能力只依赖这个可组合协议，不按特性名称判断。</summary>
public interface IEliteProjectileRule
{
	EliteProjectilePlan Plan(int originalCount);
	void Configure(Projectile projectile, Unit? target);
}

public readonly record struct EliteProjectilePlan(int Count, float DamageScale, float SpreadDegrees, bool IsModified)
{
	public static EliteProjectilePlan Unchanged(int count) => new(Mathf.Max(1, count), 1, 0, false);
}

public static class EliteProjectileRules
{
	public static Vector3 SpreadDirection(Vector3 direction, float angle)
	{
		direction = direction.Normalized();
		Vector3 reference = Mathf.Abs(direction.Dot(Vector3.Up)) > .98f ? Vector3.Right : Vector3.Up;
		Vector3 axis = direction.Cross(reference).Cross(direction).Normalized();
		return direction.Rotated(axis, Mathf.DegToRad(angle));
	}
	private static IEnumerable<EnemyActionState> Steps(Unit owner)
	{
		var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(owner, abilities);
		return abilities.OfType<EnemyPlanAbility>().Where(a => a.Plan != null)
			.SelectMany(a => a.Plan.Opening.Concat(a.Plan.Near).Concat(a.Plan.Far).Concat(a.Plan.Closing))
			.Select(s => EnemyActionState.From(s, Vector3.Zero));
	}
	public static bool CanSeek(Unit owner)
	{
		var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(owner, abilities);
		return abilities.OfType<ProjectileAttackAbility>().Any(a => a.GetNodeOrNull<ActRoguelike.Weapons.Weapon>("Weapon")?.ProjectileScene != null)
			|| Steps(owner).Any(s => s.Kind == EnemyActionStep.ActionKind.Shot && s.TurnDegreesPerSecond <= 0);
	}
	public static bool CanMultishot(Unit owner)
	{
		var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(owner, abilities);
		return abilities.OfType<ProjectileAttackAbility>().Any(a => a.GetNodeOrNull<ActRoguelike.Weapons.Weapon>("Weapon")?.ProjectileScene != null)
			|| Steps(owner).Any(s => s.Kind == EnemyActionStep.ActionKind.Shot && s.TurnDegreesPerSecond <= 0);
	}
	private static IEliteProjectileRule? Rule(Unit? owner)
		=> owner != null && GodotObject.IsInstanceValid(owner) && EliteTraitRuntime.Find(owner) is { Stopped: false } runtime
			? runtime.Behavior as IEliteProjectileRule : null;
	public static EliteProjectilePlan GetPlan(Unit? owner, bool eligible, int baseCount)
		=> eligible && Rule(owner) is { } rule ? rule.Plan(baseCount) : EliteProjectilePlan.Unchanged(baseCount);
	public static void Configure(Unit? owner, Projectile projectile, Unit? target, bool eligible)
	{
		if (eligible) Rule(owner)?.Configure(projectile, target);
	}
	public static float RecoveryScale(Unit? owner)
		=> Mathf.Clamp(owner?.Stats.Get("EliteRecoverySpeed") ?? 1, 1, 1.5f);
	/// <summary>同一次出手的分弹保留冻结快照与CastId，仅调整总伤害预算及数量。</summary>
	public static AttackInstance WithDamage(AttackInstance attack, float scale, int count, bool projectileTag = false) => new()
	{
		CastId = attack.CastId, Attacker = attack.Attacker, Ability = attack.Ability, AbilityKind = attack.AbilityKind,
		IsManualWeaponAttack = attack.IsManualWeaponAttack, IsDirectAbilityAttack = attack.IsDirectAbilityAttack,
		BaseDamage = attack.BaseDamage * scale, FeedbackWeight = attack.FeedbackWeight,
		CanCrit = attack.CanCrit, CritChance = attack.CritChance, CritMultiplier = attack.CritMultiplier,
		IsDamageOverTime = attack.IsDamageOverTime, IsSecondary = attack.IsSecondary,
		Knockback = attack.Knockback, Policy = attack.Policy, HitInterval = attack.HitInterval,
		Lifetime = attack.Lifetime, ProjectileCount = count, Pierce = attack.Pierce, Ricochet = attack.Ricochet,
		ExplosionRadius = attack.ExplosionRadius, ExplosionDamage = attack.ExplosionDamage * scale,
		Tags = projectileTag && !attack.HasTag("Projectile") ? attack.Tags.Append("Projectile").ToArray() : attack.Tags,
		BypassBlock = attack.BypassBlock, Status = attack.Status,
		ComboStep = attack.ComboStep, ComboStepCount = attack.ComboStepCount,
	};
}

public sealed class SeekerTraitBehavior : EliteTraitBehavior, IEliteProjectileRule
{
	public SeekerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public EliteProjectilePlan Plan(int originalCount) => EliteProjectilePlan.Unchanged(originalCount);
	public void Configure(Projectile projectile, Unit? target)
	{
		// 只增强原来不追踪的直射弹，保留原定出手方向和航程。
		if (projectile.ExplosionOnly || projectile.ShowImpactWarning || projectile.GravityScale != 0 || projectile.HomingTurnRateDegrees > 0) return;
		projectile.SeekAnyHostile = true; projectile.AssignedHomingTarget = target;
		projectile.HomingStatusId = string.Empty; projectile.BezierHoming = false;
		projectile.HomingStartDistance = Mathf.Max(.8f, Def.Radius);
		projectile.HomingTurnRateDegrees = Mathf.Clamp(Def.Magnitude, 30, 110);
		projectile.HomingRange = 24;
	}
}

public sealed class MultishotTraitBehavior : EliteTraitBehavior, IEliteProjectileRule
{
	public MultishotTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public EliteProjectilePlan Plan(int originalCount)
	{
		int original = Mathf.Max(1, originalCount), count = Mathf.Clamp(original + Mathf.Max(1, Def.Count) - 1, original, Mathf.Max(original, 12));
		if (count == original) return EliteProjectilePlan.Unchanged(original);
		return new(count, Mathf.Clamp(Def.Magnitude, 1, 1.8f) * original / count, Mathf.Clamp(Def.Radius, 12, 60), count != original);
	}
	public void Configure(Projectile projectile, Unit? target) { }
}

public sealed class SpeederTraitBehavior : EliteTraitBehavior
{
	public SpeederTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Attach()
	{
		float scale = Mathf.Clamp(Def.Magnitude, 1.05f, 1.5f);
		Owner.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = scale, Source = this });
		Owner.Stats.AddModifier(new StatModifier { Stat = "CooldownMultiplier", Operation = ModifierOperation.Multiply, Value = 1 / scale, Source = this });
		Owner.Stats.SetBase("EliteRecoverySpeed", 1);
		Owner.Stats.AddModifier(new StatModifier { Stat = "EliteRecoverySpeed", Operation = ModifierOperation.Multiply, Value = scale, Source = this });
		// 前摇及ATK事件动画不加速，只加快恢复与下一次攻击，保留自爆反应窗口。
	}
	public override void Dispose() => Owner.Stats.RemoveBySource(this);
}

public sealed class KnockbackTraitBehavior : EliteTraitBehavior
{
	private float _next;
	public int AppliedCount { get; private set; }
	public KnockbackTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void OnFact(GameplayEvent fact)
	{
		if (fact.Kind != GameplayEventKind.Damaged || fact.Source != Owner || fact.DamageApplied <= 0 || fact.IsSecondary || fact.IsDamageOverTime
			|| fact.Target is not { IsDead: false } target || !GodotObject.IsInstanceValid(target) || target.Health <= 0 || Wave.Time < _next) return;
		Vector3 direction = target.GlobalPosition - Owner.GlobalPosition; direction.Y = 0;
		if (direction.LengthSquared() < .001f) direction = -Owner.GlobalBasis.Z;
		if (!target.ReceiveKnockback(direction.Normalized() * Mathf.Clamp(Def.Magnitude, 2, 12))) return;
		_next = Wave.Time + Mathf.Max(.6f, Def.Cooldown); AppliedCount++;
	}
}

/// <summary>先预警后换位；不侵占正在执行的攻击或空中动作。</summary>
public abstract class RepositionTraitBehavior : EliteTraitBehavior
{
	private readonly List<Ability> _abilities = new();
	private EnemyAttackWarning? _warning;
	private float _next, _waiting = -1;
	protected Vector3 Destination;
	protected Vector3[] Path = Array.Empty<Vector3>();
	public int RepositionCount { get; protected set; }
	public Vector3 LastDestination => Destination;
	protected RepositionTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Attach()
	{
		NodeSearch.CollectInSubtree(Owner, _abilities); _next = Wave.Time + Mathf.Max(1, Def.Cooldown);
		_warning = EnemyAttackWarning.Create(EliteTraitRuntime.Find(Owner) ?? (Node)Owner, Owner);
	}
	protected bool Busy => _abilities.Any(a => GodotObject.IsInstanceValid(a) && a.IsBusy);
	protected bool CanMove => !Busy && Owner.Movement is { IsGrounded: true, IsControlDisabled: false } && !Owner.HasActionLock;
	protected abstract bool SelectDestination(MapManager map, Unit player);
	protected abstract void Execute(MapManager map);
	protected void ShowPath(Vector3 from, Vector3 to)
	{
		Vector3 delta = to - from; delta.Y = 0;
		_warning?.SweepCircle(from, delta, delta.Length(), .5f);
	}
	protected void HideWarning() => _warning?.HideAll();
	public override void Tick(float dt)
	{
		if (Wave.Player is not { IsDead: false } player || !GodotObject.IsInstanceValid(player) || MapManager.Instance is not { IsReady: true } map) return;
		if (_waiting >= 0)
		{
			if (!CanMove) { _waiting = -1; HideWarning(); Owner.Movement?.EndVelocityOverride(this); _next = Wave.Time + 1; return; }
			_waiting -= dt;
			if (_waiting > 0) return;
			_waiting = -1; HideWarning(); Owner.Movement?.EndVelocityOverride(this);
			if (map.TryValidPoint(Destination, Owner.GlobalPosition, out var point, Owner) && point.DistanceTo(Destination) < .3f) Execute(map);
			_next = Wave.Time + Mathf.Max(1, Def.Cooldown); return;
		}
		if (Wave.Time < _next || !CanMove || Owner.Movement!.HasVelocityOverride || !SelectDestination(map, player)) return;
		_waiting = Mathf.Max(.4f, Def.WarningSeconds); _next = Wave.Time + Mathf.Max(1, Def.Cooldown);
		Owner.GetNodeOrNull<UnitPresentation>("Presentation")?.FlashAttack();
		Owner.Movement.BeginVelocityOverride(this, Vector3.Zero, _waiting);
		ShowPath(Owner.GlobalPosition, Destination);
	}
	public override void Dispose()
	{
		Owner.Movement?.EndVelocityOverride(this);
		if (_warning != null && GodotObject.IsInstanceValid(_warning)) _warning.QueueFree();
		_abilities.Clear();
	}
}

public sealed class ShifterTraitBehavior : RepositionTraitBehavior
{
	public ShifterTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override bool SelectDestination(MapManager map, Unit player)
		=> map.TryRingPoint(player.GlobalPosition, 2.5f, Mathf.Clamp(Def.Radius, 4, 8), out Destination, Owner, Owner.GlobalPosition, 24)
			&& Destination.DistanceTo(Owner.GlobalPosition) > 2;
	protected override void Execute(MapManager map)
	{
		Owner.Movement?.Stop(); Owner.GlobalPosition = Destination + Vector3.Up * .03f; Owner.Velocity = Vector3.Zero;
		Owner.ResetPhysicsInterpolation(); RepositionCount++;
	}
}

public sealed class FlankerTraitBehavior : RepositionTraitBehavior
{
	private int _waypoint;
	private float _moving;
	public FlankerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override bool SelectDestination(MapManager map, Unit player)
	{
		Vector3 facing = -player.GlobalBasis.Z; facing.Y = 0; facing = facing.Normalized();
		Vector3 side = facing.Cross(Vector3.Up) * (GD.Randf() < .5f ? 1 : -1);
		Vector3 preferred = player.GlobalPosition + side * Mathf.Clamp(Def.Radius, 2.5f, 4.5f);
		if (!map.TryValidPoint(preferred, Owner.GlobalPosition, out Destination, Owner)
			|| !map.TryPath(Owner.GlobalPosition, Destination, out Path, Owner) || Path.Length < 2) return false;
		float length = 0; for (int i = 1; i < Path.Length; i++) length += Path[i].DistanceTo(Path[i - 1]);
		return length >= 1 && length <= 14;
	}
	protected override void Execute(MapManager map) { _waypoint = 0; _moving = 2.5f; RepositionCount++; }
	public override void Tick(float dt)
	{
		if (_moving <= 0) { base.Tick(dt); return; }
		_moving -= dt;
		if (!CanMove || _moving <= 0) { FinishMove(); return; }
		while (_waypoint < Path.Length)
		{
			Vector3 direction = Path[_waypoint] - Owner.GlobalPosition; direction.Y = 0;
			if (direction.Length() < .35f) { _waypoint++; continue; }
			Owner.Movement!.FaceDirection(direction, dt);
			float speed = Mathf.Clamp(Def.Magnitude, 5, 10);
			if (!Owner.Movement.BeginVelocityOverride(this, direction.Normalized() * Mathf.Min(speed, direction.Length() / Mathf.Max(.001f, dt)), -1, true)) FinishMove();
			return;
		}
		FinishMove();
	}
	private void FinishMove() { _moving = 0; Owner.Movement?.EndVelocityOverride(this); HideWarning(); }
	public override void Dispose() { FinishMove(); base.Dispose(); }
}

public sealed class SummonerTraitBehavior : EliteTraitBehavior
{
	private int _remaining, _attempts;
	private float _timer;
	private EnemyAttackWarning? _warning;
	public int SummonedCount { get; private set; }
	public bool Finished => _remaining <= 0 || _attempts >= 3;
	public SummonerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Attach()
	{
		_remaining = Mathf.Clamp(Def.Count, 1, 6); _timer = Mathf.Max(.5f, Def.WarningSeconds);
		_warning = EnemyAttackWarning.Create(EliteTraitRuntime.Find(Owner) ?? (Node)Owner, Owner);
		_warning?.Circle(Owner.GlobalPosition, 5);
	}
	public override void Tick(float dt)
	{
		if (Finished || !Wave.IsSealed) return;
		_warning?.Circle(Owner.GlobalPosition, 5);
		_timer -= dt; if (_timer > 0) return;
		int spawned = Wave.TrySummon(Owner, _remaining); _remaining -= spawned; SummonedCount += spawned; _attempts++;
		_timer = 1;
		if (Finished) _warning?.HideAll();
	}
	public override void Dispose()
	{
		if (_warning != null && GodotObject.IsInstanceValid(_warning)) _warning.QueueFree();
	}
}
