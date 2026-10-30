using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>替换武技共用出手快照、直接伤害与可见目标查询；不依赖模型骨骼。</summary>
public abstract partial class EvolvedAttackAbility : Ability
{
	[Export] public float Damage { get; set; } = 24;
	[Export] public float Radius { get; set; } = 3;
	protected AttackOffense Offense;
	protected float PowerSnapshot;
	protected long Cast;
	private bool _committed;
	// 角色换模后，原场景的 Model 可能只是隐藏缓存；动作始终操作当前展示模型。
	protected Node3D? ActiveModel => OwnerUnit?.GetNodeOrNull<CharacterDisplay>("Appearance")?.ActiveModel ?? OwnerUnit?.GetNodeOrNull<Node3D>("Model");
	protected float EffectiveRadius => Radius * Mathf.Max(.1f, OwnerUnit!.Stats.Get("AttackRangeMultiplier")) * OwnerUnit.GlobalBasis.Scale.Abs().X;
	protected void Snapshot()
	{
		_committed = true; Offense = AttackOffense.Capture(OwnerUnit); PowerSnapshot = (OwnerUnit?.Stats.Get("AttackPowerAdd") ?? 0) * Offense.DamageMultiplier; Cast = AttackInstance.NextCastId();
	}
	protected AttackInstance Attack(float damage, float powerShare = 1, bool component = false) => new()
	{
		Attacker = OwnerUnit, Ability = AbilityId, AbilityKind = AbilityId, BaseDamage = damage * Offense.DamageMultiplier,
		IsManualWeaponAttack = !component, IsWeaponSkillComponent = component, IsSecondary = component,
		CastId = Cast, CanCrit = true, CritChance = Offense.CritChance, CritMultiplier = Offense.CritMultiplier,
		PowerDamageSnapshotOverride = PowerSnapshot, PowerDamageWeight = powerShare,
	};
	protected Unit[] Enemies(Vector3 center, float radius) => OwnerUnit!.GetTree().GetNodesInGroup("enemy_units").OfType<Unit>()
		.Where(u => u.IsValidTarget && OwnerUnit.IsHostileTo(u) && Targeting.AimPoint(u).DistanceSquaredTo(center) <= radius * radius).ToArray();
	protected bool CanSee(Unit target, Vector3 from)
	{
		using var query = PhysicsRayQueryParameters3D.Create(from, Targeting.AimPoint(target), CombatLayers.World);
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query); return hit.Count == 0;
	}
	protected void Strike(Unit target, AttackInstance attack)
	{
		if (IsInstanceValid(target) && target.IsValidTarget && OwnerUnit!.IsHostileTo(target))
			target.ReceiveHit(attack.RollHit(target, (target.GlobalPosition - OwnerUnit.GlobalPosition).Normalized(), Targeting.AimPoint(target)));
	}
	protected override void OnFinishOrCancel() { if (_committed) { _committed = false; StartCommittedCooldown(); } }
}
