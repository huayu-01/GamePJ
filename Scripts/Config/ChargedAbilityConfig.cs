using Godot;

namespace ActRoguelike.Config;

/// <summary>蓄力技能才提供蓄力编辑项；通用接口继续兼容现有配置消费与增幅。</summary>
[Tool, GlobalClass]
public partial class ChargedAbilityConfig : AbilityConfig
{
	[ExportGroup("蓄力输入与释放")]
	[Export] public override bool HoldToCharge { get; set; } = true;
	[Export] public override float ChargeReleaseAnimSpeedScale { get; set; } = 1;
	[Export] public override float ChargeBaseSeconds { get; set; }
	[Export] public override bool AllowEarlyChargeRelease { get; set; }
	[Export(PropertyHint.Range, "0,1,0.01")] public override float EarlyReleaseDamageScale { get; set; } = .5f;
	[Export] public override float ChargeStepSeconds { get; set; }
	[Export] public override float ChargeStepScale { get; set; }
	[Export] public override int ChargeMaxSteps { get; set; }
	[Export] public override float ChargeMaxSeconds { get; set; }
	[Export] public override float PerfectWindowSeconds { get; set; }
}
