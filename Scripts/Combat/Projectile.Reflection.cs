using Godot;

namespace ActRoguelike.Combat;

public partial class Projectile
{
	public int ReflectionCount { get; private set; }
	private void Reflect(Unit defender, Unit original)
	{
		var attack = _attack!;
		Vector3 direction = IsInstanceValid(original) && !original.IsDead
			? (GetTargetCenter(original) - GlobalPosition).Normalized() : -_velocity.Normalized();
		ResetReflectionState();
		Launch(new AttackInstance
		{
			Attacker = defender, Ability = "ReflectedProjectile", AbilityKind = attack.AbilityKind,
			BaseDamage = attack.BaseDamage, CanCrit = attack.CanCrit, CritChance = attack.CritChance,
			CritMultiplier = attack.CritMultiplier, Knockback = attack.Knockback, FeedbackWeight = attack.FeedbackWeight,
			Policy = attack.Policy, HitInterval = attack.HitInterval, Lifetime = attack.Lifetime,
			Pierce = attack.Pierce, Ricochet = attack.Ricochet, ExplosionRadius = attack.ExplosionRadius,
			ExplosionDamage = attack.ExplosionDamage, Tags = attack.Tags, Status = attack.Status,
			BypassBlock = attack.BypassBlock, IsSecondary = true,
		}, direction);
		ReflectionCount++;
	}
	private void ResetReflectionState()
	{
		_tracker.Reset(); _hitTargets.Clear(); _lifetime = 0; _exploded = false;
		AssignedHomingTarget = null; _impactWarning?.QueueFree(); _impactWarning = null;
	}
}
