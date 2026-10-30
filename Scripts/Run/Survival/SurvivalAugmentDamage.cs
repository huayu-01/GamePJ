using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

public static class SurvivalAugmentDamage
{
	/// <summary>只使用冻结基础伤害，不再次套攻击者暴击／目标增伤乘区；仍走目标统一防御结算。</summary>
	public static DamageResult Hit(Unit player, Unit target, float damage, Vector3 origin, string name, EventContext? parent, float push = 0)
	{
		var context = parent?.Child(player, target) ?? (parent == null ? EventContext.Create(player, target) : null);
		if (context == null || target.IsDead || target.Health <= 0 || !player.IsHostileTo(target)) return DamageResult.Miss;
		Vector3 direction = target.GlobalPosition - origin;
		if (direction.LengthSquared() < .001f) direction = Vector3.Forward;
		var attack = new AttackInstance { Attacker = player, Ability = name, BaseDamage = damage, IsSecondary = true, Knockback = push, Tags = new[] { "Survival", "Secondary" } };
		return target.ReceiveHit(new HitInstance { Attacker = player, Target = target, Attack = attack, Context = context, Damage = damage, Position = Projectile.GetTargetCenter(target), Direction = direction.Normalized(), Knockback = push });
	}
	public static void Pulse(Node owner, SurvivalDirector director, GameplayEvent fact, float radius, float scale, string name, Color color, float push = 0)
	{
		SurvivalAugmentFx.Ring(owner, fact.Position, radius, color, .35f);
		foreach (var target in director.LivingEnemies)
			if (Projectile.GetTargetCenter(target).DistanceSquaredTo(fact.Position) <= radius * radius)
				Hit(director.Player!, target, fact.Attack!.BaseDamage * scale, fact.Position, name, fact.Context, push);
	}
}
