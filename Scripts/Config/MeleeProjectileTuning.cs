using Godot;

namespace ActRoguelike.Config;

/// <summary>真实近战 ATK 同时发射的额外投射物；伤害继承当段攻击快照，不重复乘玩家属性。</summary>
[Tool]
[GlobalClass]
public partial class MeleeProjectileTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public float DamageScale { get; set; } = 1f;
	[Export] public float Speed { get; set; } = 60f;
	[Export] public float Lifetime { get; set; } = 0.45f;
	[Export] public int Pierce { get; set; } = 8;
	[Export] public float SizeRatio { get; set; } = 0.5f;
	[Export] public int SweepMaxHits { get; set; } = 8;
	[Export] public Godot.Collections.Array<string> AttackTags { get; set; } = new();
}
