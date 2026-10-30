using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>技能可用次数与回复间隔；不允许用任意字符串覆盖技能字段。</summary>
[Tool, GlobalClass]
public partial class AbilityChargesTuning : AbilityRewardTuning
{
	[Export] public int MaxCharges { get; set; } = 1;
	[Export] public float RechargeSeconds { get; set; }
	public override bool Supports(System.Type abilityType) => typeof(Ability).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { ability.MaxCharges = Mathf.Max(1, MaxCharges); ability.RechargeSeconds = Mathf.Max(0, RechargeSeconds); }
	public override System.Collections.Generic.IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (MaxCharges < 1) yield return "MaxCharges：至少1。"; }
}
