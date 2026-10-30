using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ArmorOnKillRewardEffect : RewardEffectDefinition
{
	[Export] public int KillsPerTrigger { get; set; } = 10;
	[Export] public float ArmorPerTrigger { get; set; } = 1;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new RuneArmorOnKillRule { RewardId = rewardId, Name = $"Rune_{rewardId}_ArmorOnKill", KillsPerTrigger = KillsPerTrigger, ArmorPerTrigger = ArmorPerTrigger });
}
