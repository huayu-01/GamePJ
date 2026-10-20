using Godot;

namespace ActRoguelike.Config;

/// <summary>指定技能自身的伤害、尺寸与冷却调整，不写玩家全局乘区。</summary>
[Tool]
[GlobalClass]
public partial class SkillEnhancementTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public float DamageScale { get; set; } = 1f;
	/// <summary>灵能汲取：本人击杀后只延长仍存在的灵剑。</summary>
	[Export] public float KillExtensionSeconds { get; set; }
	[Export] public float SizeScale { get; set; } = 1f;
	/// <summary>近战单段模式：负数选择末段，非负数选择指定段（0起）；仅近战目标消费。</summary>
	[Export] public int ComboStepIndex { get; set; } = -1;
	/// <summary>近战盒形判定沿前方的长度与中心偏移倍率，不改变宽高。</summary>
	[Export] public float ForwardReachScale { get; set; } = 1f;
	/// <summary>共通落真实落地后的纯击退强度；负数不配置。</summary>
	[Export] public float LandingKnockback { get; set; } = -1f;
	[Export] public int ProjectileSweepMaxHits { get; set; }
	[Export] public float ProjectileSweepRadius { get; set; } = 0.08f;
	/// <summary>小于零保留技能原冷却；零表示无冷却。</summary>
	[Export] public float CooldownSeconds { get; set; } = -1f;
}
