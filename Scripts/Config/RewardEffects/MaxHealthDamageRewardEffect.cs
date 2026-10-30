using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class MaxHealthDamageRewardEffect : RewardEffectDefinition
{
	[Export] public float Ratio { get; set; } = .1f;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new RuneMaxHealthDamageRule { Name = $"Rune_{rewardId}_MaxHealthDamage", Ratio = Ratio });
}
