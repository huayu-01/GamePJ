using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ScavengerWardRewardEffect : SurvivalRewardEffect
{
	[Export] public float Threshold { get; set; } = 20f;
	[Export] public float ShieldRatio { get; set; } = 0.05f;
	[Export] public float CapRatio { get; set; } = 0.1f;
	[Export] public float Duration { get; set; } = 4f;
	protected override IEnumerable<float> PositiveValues => new float[] { Threshold, ShieldRatio, CapRatio, Duration };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new ScavengerWardRule(director, player, Threshold, ShieldRatio, CapRatio, Duration);
}
