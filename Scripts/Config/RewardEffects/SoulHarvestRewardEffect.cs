using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class SoulHarvestRewardEffect : RewardEffectDefinition
{
	[Export] public float Chance { get; set; } = .3f;
	[Export] public float Heal { get; set; } = 8;
	[Export] public float PickupRadius { get; set; } = 1.2f;
	[Export] public float LifetimeSeconds { get; set; } = 15;
	[Export] public float PickupDelaySeconds { get; set; } = 0;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (Chance > 1) yield return "Chance：不得超过1。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new SoulHarvestRule { RewardId = rewardId, Chance = Chance, Heal = Heal, PickupRadius = PickupRadius, LifetimeSeconds = LifetimeSeconds, PickupDelaySeconds = PickupDelaySeconds });
}
