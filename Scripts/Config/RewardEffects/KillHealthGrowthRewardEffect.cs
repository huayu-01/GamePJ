using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>参数资源只定义击杀门槛和成长值，各持有者独立计数。</summary>
[Tool, GlobalClass]
public partial class KillHealthGrowthRewardEffect : RewardEffectDefinition
{
	[Export] public int KillsPerTrigger { get; set; } = 5;
	[Export] public float HealthPerTrigger { get; set; } = 1;
	[Export] public float BossBonusHealth { get; set; } = 20;
	[Export] public bool FillNewHealth { get; set; } = true;
	public override IEnumerable<string> Validate()
	{
		if (KillsPerTrigger < 1) yield return "KillsPerTrigger：必须至少1次击杀。";
		foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error;
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) =>
		new NodeRewardEffectInstance(context, rewardId, new KillHealthGrowthRule {
			KillsPerTrigger = KillsPerTrigger, HealthPerTrigger = HealthPerTrigger,
			BossBonusHealth = BossBonusHealth, FillNewHealth = FillNewHealth });
}
