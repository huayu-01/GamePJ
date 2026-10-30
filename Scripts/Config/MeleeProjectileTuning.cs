using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>真实近战 ATK 同时发射的额外投射物；伤害继承当段攻击快照，不重复乘玩家属性。</summary>
[Tool]
[GlobalClass]
public partial class MeleeProjectileTuning : AbilityRewardTuning
{
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public float DamageScale { get; set; } = 1f;
	[Export] public float Speed { get; set; } = 60f;
	[Export] public float Lifetime { get; set; } = 0.45f;
	[Export] public int Pierce { get; set; } = 8;
	[Export] public float SizeRatio { get; set; } = 0.5f;
	[Export] public int SweepMaxHits { get; set; } = 8;
	[Export] public Godot.Collections.Array<string> AttackTags { get; set; } = new();
	public override bool Supports(System.Type abilityType) => typeof(MeleeComboAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { ((MeleeComboAbility)ability).ConfigureExtraProjectile(this); }
	public override System.Collections.Generic.IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (ProjectileScene == null) yield return "ProjectileScene：必须指定投射物。"; else { var node = ProjectileScene.Instantiate(); bool valid = node is ActRoguelike.Combat.Projectile; node.Free(); if (!valid) yield return "ProjectileScene：根节点必须继承Projectile。"; } }
}
