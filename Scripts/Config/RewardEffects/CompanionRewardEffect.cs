using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class CompanionRewardEffect : RewardEffectDefinition
{
	[Export] public float IntervalSeconds { get; set; } = 5;
	[Export] public float SearchRadius { get; set; } = 8;
	[Export] public float Damage { get; set; } = 20;
	[Export] public float OrbitRadius { get; set; } = 1.6f;
	[Export] public float OrbitDegreesPerSecond { get; set; } = 90;
	[Export] public float HoverHeight { get; set; } = 1.2f;
	public override IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (IntervalSeconds <= 0) yield return "IntervalSeconds：必须大于零。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new PixieCompanion { AbilityId = rewardId, Name = $"Rune_{rewardId}_Pixie", IntervalSeconds = IntervalSeconds, SearchRadius = SearchRadius, Damage = Damage, OrbitRadius = OrbitRadius, OrbitDegreesPerSecond = OrbitDegreesPerSecond, HoverHeight = HoverHeight });
}
