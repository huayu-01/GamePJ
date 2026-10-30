using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

/// <summary>可复用的链伤害、次生攻击、区域与发射服务，不选择哪项特化执行。</summary>
public sealed class SpecializationServices
{
	private readonly SpecializationRuntime _runtime;
	private readonly Unit _owner;
	public int ChainCount, ThunderCount, SpearCount, AfterimageCount, FieldCount, WindShotCount;
	public SpecializationServices(SpecializationRuntime runtime, Unit owner) { _runtime = runtime; _owner = owner; }
	public IEnumerable<Unit> Enemies(Vector3 center, float radius) => _owner.GetTree().GetNodesInGroup("enemy_units").OfType<Unit>()
		.Where(u => u.IsValidTarget && _owner.IsHostileTo(u) && u.GlobalPosition.DistanceSquaredTo(center) <= radius * radius).ToArray();
	public void DirectHit(Unit target, float damage, string tag, EventContext? context = null, bool exact = false)
	{
		if (!target.IsValidTarget || !_owner.IsHostileTo(target) || damage <= 0) return;
		EventContext? next = context == null ? EventContext.Create(_owner, target) : context.Child(_owner, target); if (next == null) return;
		var attack = new AttackInstance { Attacker = _owner, Ability = tag, BaseDamage = damage * (exact ? 1 : AttackOffense.Capture(_owner).DamageMultiplier), IsSecondary = true };
		target.ReceiveHit(new HitInstance { Target = target, Attacker = _owner, Attack = attack, Context = next,
			Damage = exact ? damage : attack.RollHit(target, Vector3.Zero, target.GlobalPosition).Damage, Position = target.GlobalPosition });
	}
	public void Thunder(Unit target, float damage)
	{
		if (!target.IsValidTarget) return; ThunderCount++;
		Vector3 position = target.GlobalPosition;
		SpecializationFx.Line(_owner.GetParent(), position + Vector3.Up * 8, position, new Color(1, .86f, .2f));
		var blast = _runtime.Definition<ThunderBlastBehaviorDefinition>(K.ThunderBlast);
		var targets = blast == null ? new[] { target } : Enemies(position, blast.Radius);
		foreach (var victim in targets) DirectHit(victim, damage, "Thunder");
	}
	public void WindHit(Unit target, float damage, K kind)
	{
		var attack = new AttackInstance { Attacker = _owner, Ability = "Whirlwind", AbilityKind = kind.ToString(), BaseDamage = damage * AttackOffense.Capture(_owner).DamageMultiplier, IsSecondary = true };
		target.ReceiveHit(attack.RollHit(target, Vector3.Zero, target.GlobalPosition));
	}
	public void Chain(Vector3 from, Unit? first, float damage, int jumps, float range)
	{
		ChainCount++; var hit = new HashSet<Unit>();
		var mastery = _runtime.Definition<ThunderMasteryBehaviorDefinition>(K.ThunderMastery); jumps += mastery?.ExtraJumps ?? 0;
		Unit? next = first;
		for (int i = 0; i < jumps; i++)
		{
			next ??= Enemies(from, range).Where(u => !hit.Contains(u)).OrderBy(u => u.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault(); if (next == null) break;
			hit.Add(next); Vector3 position = next.GlobalPosition;
			SpecializationFx.Line(_owner.GetParent(), from + Vector3.Up, position + Vector3.Up, new Color(.45f, .8f, 1));
			if (mastery != null) _runtime.Behavior<ThunderMarkBehavior>(K.ThunderMark)?.Detonate(next, mastery.DetonateScale);
			DirectHit(next, damage, "ThunderChain"); from = position; next = null;
		}
	}
	public SpecializationField Field(K kind, Vector3 position, float duration, System.Func<float> radius, float interval, System.Action<Unit> pulse, bool thunder = false)
	{
		if (thunder)
		{
			using var query = PhysicsRayQueryParameters3D.Create(position + Vector3.Up, position + Vector3.Down * 100, CombatLayers.World);
			using var hit = _owner.GetWorld3D().DirectSpaceState.IntersectRay(query); if (hit.Count > 0) position = hit["position"].AsVector3();
		}
		var field = new SpecializationField { Runtime = _runtime, Kind = kind, SourceUnit = _owner, Lifetime = duration, RadiusResolver = radius, TickInterval = interval, Pulse = pulse, ThunderVisual = thunder };
		_runtime.AddChild(field); field.GlobalPosition = position; FieldCount++; return field;
	}
	public void Projectile(PackedScene? scene, string id, float damage, float speed, float lifetime, float range, float turn, bool homing, float radius = 0, float size = 1)
	{
		if (scene == null || Targeting.FindNearestHostile(_owner, _owner, range) is not { } target) return;
		var projectile = scene.Instantiate<Projectile>(); projectile.Speed = speed; projectile.MaxLifetime = lifetime;
		projectile.SeekAnyHostile = homing; projectile.AssignedHomingTarget = target; projectile.HomingTurnRateDegrees = turn; projectile.HomingRange = range;
		_owner.GetParent().AddChild(projectile); projectile.GlobalPosition = _owner.GlobalPosition + Vector3.Up;
		if (radius > 0) { projectile.QueryGeometry = 1; projectile.SweepRadius = radius * size; projectile.Scale = Vector3.One * size; }
		projectile.Launch(new AttackInstance { Attacker = _owner, Ability = id, AbilityKind = id, BaseDamage = damage * AttackOffense.Capture(_owner).DamageMultiplier, Lifetime = lifetime, IsSecondary = true },
			(target.GlobalPosition + Vector3.Up * .7f - projectile.GlobalPosition).Normalized());
		if (homing) SpearCount++; else WindShotCount++;
	}
}
