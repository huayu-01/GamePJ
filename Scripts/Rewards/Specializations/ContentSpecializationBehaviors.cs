using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

public abstract class DefinedSpecialization<T> : SpecializationBehavior where T : SpecializationBehaviorDefinition
{
	protected T Data { get; }
	protected DefinedSpecialization(SpecializationContext context, T data) : base(context.Owner) => Data = data;
	protected float WindScale => Context.Definition<WildWindBehaviorDefinition>(K.WildWind)?.SizeScale ?? 1;
	protected SpecializationField Field(K kind, Vector3 position, float duration, float radius, float interval, float damage, bool thunder = false)
		=> Own(Context.Services.Field(kind, position, duration, () => radius * (thunder ? 1 : WindScale), interval,
			target => { if (thunder) Context.Services.Thunder(target, damage); else Context.Services.WindHit(target, damage, kind); }, thunder));
}

public sealed class PassiveSpecializationBehavior : DefinedSpecialization<SpecializationBehaviorDefinition>
{
	public PassiveSpecializationBehavior(SpecializationContext c, SpecializationBehaviorDefinition d) : base(c, d) { }
}

public sealed class ThunderMarkBehavior : DefinedSpecialization<ThunderMarkBehaviorDefinition>
{
	private readonly Dictionary<Unit, float> _marks = new();
	public int Count => _marks.Count;
	public override int Priority => -300;
	public ThunderMarkBehavior(SpecializationContext c, ThunderMarkBehaviorDefinition d) : base(c, d) { }
	public override void OnPrimaryHit(GameplayEvent fact)
	{
		if (fact.Target is not { IsValidTarget: true } target || !GodotObject.IsInstanceValid(target) || target.HasStatus("ThunderMark")) return;
		_marks[target] = Data.Delay;
		if (StatusPresets.Create("ThunderMark", Data.Delay) is { } mark) target.ApplyStatus(mark, Owner, Context.RewardId);
	}
	public override void Tick(float dt)
	{
		foreach (var unit in _marks.Keys.ToArray())
		{
			if (!GodotObject.IsInstanceValid(unit) || !unit.IsValidTarget) { _marks.Remove(unit); continue; }
			_marks[unit] -= dt; if (_marks[unit] <= 0) Detonate(unit, 1);
		}
	}
	public void Detonate(Unit target, float scale) { if (_marks.Remove(target)) { target.RemoveStatus("ThunderMark"); Context.Services.Thunder(target, Data.Damage * scale); } }
	protected override void Remove() { foreach (var target in _marks.Keys) if (GodotObject.IsInstanceValid(target)) target.RemoveStatus("ThunderMark"); _marks.Clear(); }
}

public sealed class ThunderArmorBehavior : DefinedSpecialization<ThunderArmorBehaviorDefinition>
{
	private float _guard;
	public override int Priority => -290;
	public ThunderArmorBehavior(SpecializationContext c, ThunderArmorBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt) => _guard = Mathf.Max(0, _guard - dt);
	public override void OnFact(GameplayEvent fact) { if (fact.Kind == GameplayEventKind.Parried && fact.Source == Owner && fact.Target is { } attacker) Retaliate(attacker); }
	public override void OnDamaged(DamageResult result, HitInstance hit) { if ((result.ArmorLost > 0 || result.Blocked) && hit.Attacker is { } attacker) Retaliate(attacker); }
	private void Retaliate(Unit attacker)
	{
		if (_guard > 0 || !attacker.IsValidTarget || !Owner.IsHostileTo(attacker)) return; _guard = .001f;
		Context.Services.DirectHit(attacker, Data.ReflectDamage, "ThunderArmor"); Context.Services.Chain(attacker.GlobalPosition, attacker, Data.Damage, Data.Jumps, Data.Range);
	}
}

public sealed class ThunderHeartBehavior : DefinedSpecialization<ThunderHeartBehaviorDefinition>
{
	private double _elapsed, _readyAt;
	public override void Tick(float delta) => _elapsed += System.Math.Max(0, delta);
	public override int Priority => -200;
	public ThunderHeartBehavior(SpecializationContext c, ThunderHeartBehaviorDefinition d) : base(c, d) { }
	public override void OnPrimaryHit(GameplayEvent fact)
	{
		if (_elapsed + .000001 < _readyAt) return;
		_readyAt = _elapsed + Data.CooldownSeconds;
		Unit? living = fact.Target is { IsValidTarget: true } target && GodotObject.IsInstanceValid(target) ? target : null;
		Context.Services.Chain(living?.GlobalPosition ?? fact.Position, living, Data.Damage, Data.Jumps, Data.Range);
	}
}

public sealed class ThunderPoolBehavior : DefinedSpecialization<ThunderPoolBehaviorDefinition>
{
	private Ability[] _abilities = Array.Empty<Ability>();
	public ThunderPoolBehavior(SpecializationContext c, ThunderPoolBehaviorDefinition d) : base(c, d) { }
	public override void Attach()
	{
		_abilities = Data.Targets.Select(t => Ability.FindByAbilityId(Owner, t.Id)).OfType<Ability>().Distinct().ToArray();
		foreach (var ability in _abilities) ability.Activated += OnAbility;
	}
	private void OnAbility(Ability ability) { if (!IsDisposed && ability.IsActive) Field(K.ThunderPool, Owner.GlobalPosition, Data.Duration, Data.Radius, Data.TickInterval, Data.Damage, true); }
	protected override void Remove() { foreach (var ability in _abilities) if (GodotObject.IsInstanceValid(ability)) ability.Activated -= OnAbility; _abilities = Array.Empty<Ability>(); }
}

public sealed class ThunderRecallBehavior : DefinedSpecialization<ThunderRecallBehaviorDefinition>
{
	private Action? _restore;
	public ThunderRecallBehavior(SpecializationContext c, ThunderRecallBehaviorDefinition d) : base(c, d) { }
	public override void Attach()
	{
		var scythe = Context.Ability<ScytheThrowAbility>(Data.Targets); var fall = Context.Ability<FallAttackAbility>(Data.Targets);
		if (scythe != null && fall != null) { _restore = scythe.CaptureThunderRecall(); scythe.ConfigureThunderRecall(Data.SpeedScale, fall, Data.LandingDamageScale, Data.DistanceScale); }
	}
	protected override void Remove() => _restore?.Invoke();
}

public sealed class WarAfterimageBehavior : DefinedSpecialization<WarAfterimageBehaviorDefinition>
{
	public WarAfterimageBehavior(SpecializationContext c, WarAfterimageBehaviorDefinition d) : base(c, d) { }
	public override void OnPrimaryHit(GameplayEvent fact)
	{
		if (fact.Target is not { IsValidTarget: true } target || !GodotObject.IsInstanceValid(target) || GD.Randf() >= Data.Chance) return;
		Context.Services.AfterimageCount++; Context.Services.DirectHit(target, fact.Value, "WarAfterimage", fact.Context, exact: true);
	}
}

public sealed class WarSpearBehavior : DefinedSpecialization<WarSpearBehaviorDefinition>
{
	public WarSpearBehavior(SpecializationContext c, WarSpearBehaviorDefinition d) : base(c, d) { }
	public override void OnFact(GameplayEvent fact)
	{
		if (fact.Source == Owner && !fact.IsSecondary && !fact.IsDamageOverTime && fact.Kind == GameplayEventKind.Critical)
			Context.Services.Projectile(Data.ProjectileScene, Context.RewardId, Data.Damage, Data.Speed, Data.Lifetime, Data.Range, Data.TurnRate, true);
	}
}

public sealed class BloodArmorBehavior : DefinedSpecialization<BloodArmorBehaviorDefinition>
{
	public BloodArmorBehavior(SpecializationContext c, BloodArmorBehaviorDefinition d) : base(c, d) { }
	public override void Attach() => Owner.ConfigureArmorHealthConversion(Data.ArmorHealthRatio);
	protected override void Remove() { if (GodotObject.IsInstanceValid(Owner)) Owner.ResetArmorHealthConversion(); }
}

public sealed class BloodBattleBehavior : DefinedSpecialization<BloodBattleBehaviorDefinition>
{
	public BloodBattleBehavior(SpecializationContext c, BloodBattleBehaviorDefinition d) : base(c, d) { }
	public override float ScaleAgainst(Unit target, AttackInstance attack) => 1 + (1 - Mathf.Clamp(Owner.Health / Owner.MaxHealth, 0, 1)) * Data.MaxDamageBonus;
}

public sealed class WarHeartBehavior : DefinedSpecialization<WarHeartBehaviorDefinition>
{
	public WarHeartBehavior(SpecializationContext c, WarHeartBehaviorDefinition d) : base(c, d) { }
	public override void Attach() => Owner.Stats.AddConversion(new StatConversion("DamageReductionAdd", "CritMultiplierAdd", Data.ConversionRatio, Data.ConvertedRewards.Select(r => r.Id).ToHashSet(), this));
	protected override void Remove() { if (GodotObject.IsInstanceValid(Owner)) Owner.Stats.RemoveBySource(this); }
}

public sealed class StormBehavior : DefinedSpecialization<StormBehaviorDefinition>
{
	public StormBehavior(SpecializationContext c, StormBehaviorDefinition d) : base(c, d) { }
	public override float ScaleAgainst(Unit target, AttackInstance attack)
	{
		float speed = Owner.MoveSpeed * Owner.Stats.Get("MoveSpeedMultiplier");
		if (Owner.GetNodeOrNull<SprintController>("Sprint") is { IsSprinting: true } sprint) speed = sprint.EffectiveSpeed(speed);
		return 1 + speed * Data.DamagePerSpeed;
	}
}

public sealed class WindArmorBehavior : DefinedSpecialization<WindArmorBehaviorDefinition>
{
	private bool _active;
	public override int Priority => 100;
	public override Color? StatusColor => Context.Sprinting && !IsDisposed ? new Color(.35f, 1, .9f, .8f) : null;
	public WindArmorBehavior(SpecializationContext c, WindArmorBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt)
	{
		bool active = Context.Sprinting; if (active == _active) return; _active = active; Owner.Stats.RemoveBySource(this);
		if (active) Owner.Stats.AddModifier(new StatModifier { Stat = "IncomingDamageMultiplier", Operation = ModifierOperation.Multiply, Value = 1 - Data.Reduction, Source = this });
	}
	protected override void Remove() { if (GodotObject.IsInstanceValid(Owner)) Owner.Stats.RemoveBySource(this); }
}

public sealed class WindHeartBehavior : DefinedSpecialization<WindHeartBehaviorDefinition>
{
	private SpecializationField? _field;
	public override int Priority => 110;
	public WindHeartBehavior(SpecializationContext c, WindHeartBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt)
	{
		if (_field == null || !GodotObject.IsInstanceValid(_field)) _field = Field(K.WindHeart, Owner.GlobalPosition, 0, Data.Radius, Data.TickInterval, Data.Damage);
		_field.GlobalPosition = Owner.GlobalPosition; _field.Active = Context.Sprinting;
	}
}

public sealed class WindDomainBehavior : DefinedSpecialization<WindDomainBehaviorDefinition>
{
	private AirHoverController? _hover; private SpecializationField? _field; private CharacterMovement? _movement;
	private float _contactCooldown, _jumpCooldown;
	public int ExtensionCount { get; private set; }
	/// <summary>由"跳跃"额外授予浮空的次数（自测取证）。</summary>
	public int JumpGrantCount { get; private set; }
	public override int Priority => 120;
	public WindDomainBehavior(SpecializationContext c, WindDomainBehaviorDefinition d) : base(c, d) { }
	public override void Attach()
	{
		_hover = NodeSearch.FindInSubtree<AirHoverController>(Owner);
		_movement = Owner.Movement;
		// 口径 A（2026-11-01 用户确认）：启用期间角色获得的**所有**浮空都按倍率放大。
		if (_hover != null && GodotObject.IsInstanceValid(_hover)) _hover.DurationScale = Data.HoverDurationScale;
		if (_movement != null && GodotObject.IsInstanceValid(_movement)) _movement.JumpStarted += OnJumpStarted;
	}
	protected override void Remove()
	{
		if (_movement != null && GodotObject.IsInstanceValid(_movement)) _movement.JumpStarted -= OnJumpStarted;
		_movement = null;
		if (_hover != null && GodotObject.IsInstanceValid(_hover)) _hover.DurationScale = 1f;
		_hover = null;
		_jumpCooldown = 0f;
	}
	/// <summary>每次起跳（含空中跳）给一段浮空；<see cref="WindDomainBehaviorDefinition.JumpHoverRepeatSeconds"/> 内不重复。</summary>
	private void OnJumpStarted(Vector3 _)
	{
		if (_jumpCooldown > 0f || _hover == null || !GodotObject.IsInstanceValid(_hover) || Data.JumpHoverSeconds <= 0f) return;
		_hover.Grant(Data.JumpHoverSeconds);
		_jumpCooldown = Mathf.Max(0f, Data.JumpHoverRepeatSeconds);
		JumpGrantCount++;
	}
	public override void Tick(float dt)
	{
		_jumpCooldown = Mathf.Max(0f, _jumpCooldown - dt);
		_contactCooldown = Mathf.Max(0, _contactCooldown - dt);
		bool active = _hover is { IsHovering: true, Maintaining: true } && Owner.Movement is { IsGrounded: false }
			&& ActRoguelike.World.MapManager.Instance is { IsReady: true };
		if (!active) { if (GodotObject.IsInstanceValid(_field)) _field!.Active = false; return; }
		var map = ActRoguelike.World.MapManager.Instance!;
		if (!map.TryGroundAnchor(Owner.GlobalPosition, out var ground, Owner, horizontalTolerance: .75f) || ground.Y > Owner.GlobalPosition.Y + .1f
			|| new Vector2(ground.X - Owner.GlobalPosition.X, ground.Z - Owner.GlobalPosition.Z).Length() > .75f)
		{ if (GodotObject.IsInstanceValid(_field)) _field!.Active = false; return; }
		if (!GodotObject.IsInstanceValid(_field)) _field = Field(K.WindDomain, ground, 0, Data.Radius, Data.TickInterval, Data.Damage);
		_field!.Active = true; _field.GlobalPosition = ground;
		if (_contactCooldown <= 0 && Owner.GlobalPosition.Y - ground.Y <= Data.ContactHeight && _hover!.Extend(Data.HoverExtensionSeconds))
		{ ExtensionCount++; _contactCooldown = Mathf.Max(.05f, Data.HoverExtensionInterval); }
	}
}

public sealed class WindSwordBehavior : DefinedSpecialization<WindSwordBehaviorDefinition>
{
	private float _timer;
	public override int Priority => 130;
	public WindSwordBehavior(SpecializationContext c, WindSwordBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt)
	{
		if (!Context.Sprinting) { _timer = 0; return; } _timer += dt; if (_timer < Data.Interval) return; _timer = 0;
		Context.Services.Projectile(Data.ProjectileScene, Context.RewardId, Data.Damage, Data.Speed, Data.Lifetime, Data.Range, Data.TurnRate, false, Data.Radius, WindScale);
	}
}

public sealed class WindTrailBehavior : DefinedSpecialization<WindTrailBehaviorDefinition>
{
	private float _timer;
	public override int Priority => 140;
	public WindTrailBehavior(SpecializationContext c, WindTrailBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt)
	{
		if (!Context.Sprinting) { _timer = 0; return; } _timer += dt; if (_timer < Data.Interval) return; _timer = 0;
		Field(K.WindTrail, Owner.GlobalPosition, Data.Duration, Data.Radius, Data.TickInterval, Data.Damage);
	}
}

public sealed class WindFieldBehavior : DefinedSpecialization<WindFieldBehaviorDefinition>
{
	private float _timer;
	public override int Priority => 150;
	public WindFieldBehavior(SpecializationContext c, WindFieldBehaviorDefinition d) : base(c, d) { }
	public override void Tick(float dt)
	{
		_timer += dt; if (_timer < Data.Interval) return; _timer = 0;
		for (int i = 0; i < 12; i++)
		{
			float angle = GD.Randf() * Mathf.Tau, distance = Mathf.Sqrt(GD.Randf()) * Data.SpawnRange;
			Vector3 candidate = Owner.GlobalPosition + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
			using var query = PhysicsRayQueryParameters3D.Create(candidate + Vector3.Up * 5, candidate + Vector3.Down * 10, CombatLayers.World);
			using var hit = Owner.GetWorld3D().DirectSpaceState.IntersectRay(query); if (hit.Count == 0 || hit["normal"].AsVector3().Y < .7f) continue;
			Vector3 position = hit["position"].AsVector3();
			using var clearance = new PhysicsShapeQueryParameters3D { Shape = new SphereShape3D { Radius = .45f }, Transform = new Transform3D(Basis.Identity, position + Vector3.Up * .6f), CollisionMask = CombatLayers.World };
			if (Owner.GetWorld3D().DirectSpaceState.IntersectShape(clearance, 1).Count > 0) continue;
			Field(K.WindField, position, Data.Duration, Data.Radius, Data.TickInterval, Data.Damage); return;
		}
	}
}

public sealed class WildWindBehavior : DefinedSpecialization<WildWindBehaviorDefinition>
{
	public WildWindBehavior(SpecializationContext c, WildWindBehaviorDefinition d) : base(c, d) { }
	public override void Attach() => Refresh();
	public override void DefinitionsChanged() => Refresh();
	protected override void Remove() => Refresh();
	private void Refresh()
	{
		if (!GodotObject.IsInstanceValid(Owner) || !Owner.IsInsideTree()) return;
		var sword = Context.Definition<WindSwordBehaviorDefinition>(K.WindSword); string? id = Context.Runtime?.RewardId(K.WindSword); if (sword == null || id == null) return;
		foreach (var projectile in Owner.GetTree().GetNodesInGroup("projectiles").OfType<Projectile>())
			if (projectile.CurrentAttack?.Attacker == Owner && projectile.CurrentAttack.Ability == id) { projectile.SweepRadius = sword.Radius * WindScale; projectile.Scale = Vector3.One * WindScale; }
	}
}
