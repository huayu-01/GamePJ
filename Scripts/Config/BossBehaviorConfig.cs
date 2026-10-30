using Godot;

namespace ActRoguelike.Config;

/// <summary>首领构筑专用技能定义；普通玩家／敌人技能不显示或存储首领处理器与抽取权重。</summary>
[Tool, GlobalClass]
public partial class BossBehaviorConfig : AbilityConfig
{
	[ExportGroup("首领行为技能")]
	[Export] public override string ApplyHandler { get; set; } = string.Empty;
	[Export] public override float Weight { get; set; } = 1;
}
