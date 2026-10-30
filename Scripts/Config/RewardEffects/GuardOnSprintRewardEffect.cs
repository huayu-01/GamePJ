using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class GuardOnSprintRewardEffect : RewardEffectDefinition
{
	[Export] public float Cooldown { get; set; } = 5;
	[Export] public float BlockDamageMultiplier { get; set; } = 0;
	[Export] public float FrontDotThreshold { get; set; } = 1;
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Cooldown) || Cooldown < 0 || !float.IsFinite(BlockDamageMultiplier) || BlockDamageMultiplier < 0 || BlockDamageMultiplier > 1 || !float.IsFinite(FrontDotThreshold) || FrontDotThreshold < -1 || FrontDotThreshold > 1) yield return "Cooldown / BlockDamageMultiplier / FrontDotThreshold：间隔非负，剩伤0到1，朝向阈值-1到1。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new GuardOnSprintRule { RewardId = rewardId, Cooldown = Cooldown, BlockDamageMultiplier = BlockDamageMultiplier, FrontDotThreshold = FrontDotThreshold });
}
