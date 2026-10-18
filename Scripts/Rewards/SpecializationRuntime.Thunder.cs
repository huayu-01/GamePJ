using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

public partial class SpecializationRuntime
{
	private readonly Dictionary<Unit, float> _marks = new();
	private float _retaliationGuard;
	public int MarkCount => _marks.Count;
	private void Mark(Unit target)
	{
		if (target.HasStatus("ThunderMark")) return;
		_marks[target] = Value(K.ThunderMark, "Delay");
		target.ApplyStatus(new StatusEffect { Id = "ThunderMark", Duration = Value(K.ThunderMark, "Delay") }, _owner, _rows[K.ThunderMark].Id);
	}
	private void TickThunder(float dt)
	{
		_retaliationGuard = Mathf.Max(0f, _retaliationGuard - dt);
		foreach (Unit unit in _marks.Keys.ToArray())
		{
			if (!IsInstanceValid(unit) || !unit.IsValidTarget) { _marks.Remove(unit); continue; }
			_marks[unit] -= dt;
			if (_marks[unit] <= 0f) Detonate(unit, 1f);
		}
	}
	private void Detonate(Unit target, float scale)
	{
		if (!_marks.Remove(target)) return;
		target.RemoveStatus("ThunderMark");
		Thunder(target, Value(K.ThunderMark, "Damage") * scale);
	}
	public void Thunder(Unit target, float damage)
	{
		if (!target.IsValidTarget) return;
		ThunderCount++;
		Vector3 position = target.GlobalPosition;
		SpecializationFx.Line(_owner.GetParent(), position + Vector3.Up * 8, position, new Color(1f, 0.86f, 0.2f));
		var targets = Has(K.ThunderBlast) ? Enemies(position, Value(K.ThunderBlast, "Radius")) : new[] { target };
		foreach (var victim in targets) DirectHit(victim, damage, "Thunder");
	}
	private void Chain(Vector3 from, Unit? first, K source)
	{
		ChainCount++;
		var hit = new HashSet<Unit>();
		int jumps = (int)(Value(source, "Jumps") + Value(K.ThunderMastery, "ExtraJumps"));
		float range = Value(source, "Range");
		Unit? next = first;
		for (int i = 0; i < jumps; i++)
		{
			next ??= Enemies(from, range).Where(u => !hit.Contains(u)).OrderBy(u => u.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();
			if (next == null) break;
			hit.Add(next); Vector3 position = next.GlobalPosition;
			SpecializationFx.Line(_owner.GetParent(), from + Vector3.Up, position + Vector3.Up, new Color(0.45f, 0.8f, 1f));
			// 在链伤害击杀前引爆，且主动引爆只用配置的30%倍率。
			if (Has(K.ThunderMastery)) Detonate(next, Value(K.ThunderMastery, "DetonateScale"));
			DirectHit(next, Value(source, "Damage"), "ThunderChain");
			from = position; next = null;
		}
	}
	private void Retaliate(Unit attacker)
	{
		if (_retaliationGuard > 0f || !attacker.IsValidTarget) return;
		// 一次架势成功会同时派发Parried与Damaged，只反击一次；不限制不同帧的新攻击。
		_retaliationGuard = 0.001f;
		DirectHit(attacker, Value(K.ThunderArmor, "ReflectDamage"), "ThunderArmor");
		Chain(attacker.GlobalPosition, attacker, K.ThunderArmor);
	}
	private void FireProjectile(K kind, bool homing)
	{
		if (!_rows.TryGetValue(kind, out var row) || row.SpecializationProjectile == null) return;
		Unit? target = Targeting.FindNearestHostile(_owner, _owner, Value(kind, "Range"));
		if (target == null) return;
		var projectile = row.SpecializationProjectile.Instantiate<Projectile>();
		projectile.Speed = Value(kind, "Speed"); projectile.MaxLifetime = Value(kind, "Lifetime");
		projectile.SeekAnyHostile = homing; projectile.AssignedHomingTarget = target;
		projectile.HomingTurnRateDegrees = Value(kind, "TurnRate"); projectile.HomingRange = Value(kind, "Range");
		_owner.GetParent().AddChild(projectile);
		projectile.GlobalPosition = _owner.GlobalPosition + Vector3.Up;
		AttackOffense offense = AttackOffense.Capture(_owner);
		if (kind == K.WindSword)
		{
			projectile.QueryGeometry = 1; projectile.SweepRadius = Value(kind, "Radius") * WindSizeScale;
			projectile.Scale = Vector3.One * WindSizeScale;
		}
		projectile.Launch(new AttackInstance { Attacker = _owner, Ability = row.Id, AbilityKind = row.Id, BaseDamage = Value(kind, "Damage") * offense.DamageMultiplier, Lifetime = Value(kind, "Lifetime"), IsSecondary = true },
			(target.GlobalPosition + Vector3.Up * 0.7f - projectile.GlobalPosition).Normalized());
		if (kind == K.WarSpear) SpearCount++;
		else WindShotCount++;
	}
}
