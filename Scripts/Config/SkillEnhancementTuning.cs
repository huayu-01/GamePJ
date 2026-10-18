using Godot;

namespace ActRoguelike.Config;

/// <summary>指定技能自身的伤害、尺寸与冷却调整，不写玩家全局乘区。</summary>
[Tool]
[GlobalClass]
public partial class SkillEnhancementTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public float DamageScale { get; set; } = 1f;
	[Export] public float SizeScale { get; set; } = 1f;
	[Export] public int ProjectileSweepMaxHits { get; set; }
	[Export] public float ProjectileSweepRadius { get; set; } = 0.08f;
	/// <summary>小于零保留技能原冷却；零表示无冷却。</summary>
	[Export] public float CooldownSeconds { get; set; } = -1f;
}
