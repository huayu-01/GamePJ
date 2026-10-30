using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class DamageBlinkRewardEffect : RewardEffectDefinition
{
	[Export] public float DamageThreshold { get; set; } = 20;
	[Export] public float MinRadius { get; set; } = 3;
	[Export] public float MaxRadius { get; set; } = 6;
	[Export] public float Cooldown { get; set; } = 2;
	public override IEnumerable<string> Validate()
	{
		foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error;
		if (DamageThreshold <= 0 || MaxRadius < MinRadius) yield return "瞬移门槛须为正，半径范围不能倒置。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId,
		new DamageBlinkRule { Threshold = DamageThreshold, MinRadius = MinRadius, MaxRadius = MaxRadius, Cooldown = Cooldown });
}
