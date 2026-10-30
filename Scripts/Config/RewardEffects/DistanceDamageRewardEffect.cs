using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class DistanceDamageRewardEffect : RewardEffectDefinition
{
	[Export] public float NearRange { get; set; } = 3;
	[Export] public float NearScale { get; set; } = .5f;
	[Export] public float FarRange { get; set; } = 15;
	[Export] public float FarScale { get; set; } = 1.5f;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (FarRange <= NearRange) yield return "FarRange：必须大于NearRange。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new RuneDistanceDamageRule { Name = $"Rune_{rewardId}_DistanceDamage", NearRange = NearRange, NearScale = NearScale, FarRange = FarRange, FarScale = FarScale });
}
