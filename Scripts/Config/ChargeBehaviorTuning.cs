using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>指定技能的蓄力与齐射进化参数；负数保留原值，覆盖优先于缩时倍率。</summary>
[Tool]
[GlobalClass]
public partial class ChargeBehaviorTuning : AbilityRewardTuning
{
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
	public override bool Supports(System.Type abilityType) => typeof(Ability).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { ability.ConfigureChargeBehavior(this); }
	public override System.Collections.Generic.IEnumerable<string> Validate() { if (CooldownScale < -1 || BaseSecondsOverride < -1 || StepSecondsOverride < -1 || StepTimeScale <= 0 || VolleyCountOverride < 0 || DamagePerLostArrow < 0 || HomingRange < 0 || HomingTurnRateDegrees < 0) yield return "蓄力覆盖：负一保留原值，倍率必须大于零，发数、补偿与追踪参数不能为负。"; }
}
