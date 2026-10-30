using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ElectromagneticRingRewardEffect : RewardEffectDefinition
{
	[Export] public float Radius { get; set; } = 3;
	[Export] public float Damage { get; set; } = 3;
	[Export] public float LinkRadius { get; set; } = 4;
	[Export] public int Targets { get; set; } = 3;
	[Export] public float TargetInterval { get; set; } = 1;
	[Export] public float ChainInterval { get; set; } = .5f;
	public override IEnumerable<string> Validate()
	{
		foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error;
		if (Radius <= 0 || Targets < 1 || TargetInterval <= 0 || ChainInterval <= 0) yield return "电圈半径、目标数与间隔必须为正。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId,
		new ElectromagneticRingRule { Radius = Radius, Damage = Damage, LinkRadius = LinkRadius, Targets = Targets, TargetInterval = TargetInterval, ChainInterval = ChainInterval });
}
