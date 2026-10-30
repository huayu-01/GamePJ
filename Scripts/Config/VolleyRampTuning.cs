using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>点按连射进化的强类型参数；用技能资源寻址，不按字符串反射写属性。</summary>
[Tool]
[GlobalClass]
public partial class VolleyRampTuning : AbilityRewardTuning
{
	[Export] public int MaxCount { get; set; } = 3;
	[Export] public float WindowSeconds { get; set; } = 1.2f;
	[Export] public float ShotIntervalSeconds { get; set; } = 0.08f;
	public override bool Supports(System.Type abilityType) => typeof(ArrowVolleyAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { ((ArrowVolleyAbility)ability).ConfigureRamp(MaxCount, WindowSeconds, ShotIntervalSeconds); }
	public override System.Collections.Generic.IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (MaxCount < 1 || WindowSeconds <= 0 || ShotIntervalSeconds <= 0) yield return "连射数量至少1，窗口与发射间隔必须大于零。"; }
}
