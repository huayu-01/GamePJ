using Godot;

namespace ActRoguelike.Config;

/// <summary>指定技能的蓄力与齐射进化参数；负数保留原值，覆盖优先于缩时倍率。</summary>
[Tool]
[GlobalClass]
public partial class ChargeBehaviorTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public float CooldownScale { get; set; } = -1f;
	[Export] public bool AlwaysPerfectRelease { get; set; }
	[Export] public float BaseSecondsOverride { get; set; } = -1f;
	[Export] public float StepSecondsOverride { get; set; } = -1f;
	[Export] public float StepTimeScale { get; set; } = 1f;
	[Export] public bool AutoReleaseOnFullCharge { get; set; }
	[Export] public int VolleyCountOverride { get; set; }
	[Export] public float DamagePerLostArrow { get; set; }
	[Export] public bool SeekDistinctHostiles { get; set; }
	[Export] public float HomingRange { get; set; }
	[Export] public float HomingTurnRateDegrees { get; set; }
}
