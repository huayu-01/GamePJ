using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class DeathAvoidRewardEffect : RewardEffectDefinition
{
	[Export] public int Charges { get; set; } = 1;
	[Export] public float HealFraction { get; set; } = .3f;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (HealFraction > 1) yield return "HealFraction：不得超过1。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new RuneDeathAvoidRule { Name = $"Rune_{rewardId}_DeathAvoid", Charges = Charges, HealFraction = HealFraction });
}
