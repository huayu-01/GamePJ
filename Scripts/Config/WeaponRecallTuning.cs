using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>投掷技能的召回策略与全部蓄力时间缩放。</summary>
[Tool]
[GlobalClass]
public partial class WeaponRecallTuning : AbilityRewardTuning
{
	[Export] public float ChargeTimeScale { get; set; } = 1f;
	[Export] public bool RecallOnSecondPress { get; set; }
	[Export] public bool AutoRecallOnSettle { get; set; } = true;
	public override bool Supports(System.Type abilityType) => typeof(SpearThrowAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { var spear = (SpearThrowAbility)ability; float scale = Mathf.Max(.01f, ChargeTimeScale); spear.ChargeBaseSeconds = TargetAbility!.ChargeBaseSeconds * scale; spear.ChargeStepSeconds = TargetAbility.ChargeStepSeconds * scale; spear.ChargeMaxSeconds = TargetAbility.ChargeMaxSeconds * scale; spear.RecallOnSecondPress = RecallOnSecondPress; spear.AutoRecallOnSettle = AutoRecallOnSettle; }
	public override System.Collections.Generic.IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (ChargeTimeScale <= 0) yield return "ChargeTimeScale：必须大于零。"; }
}
