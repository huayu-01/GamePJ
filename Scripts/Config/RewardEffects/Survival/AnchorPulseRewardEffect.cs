using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class AnchorPulseRewardEffect : SurvivalRewardEffect
{
	[Export] public float Interval { get; set; } = 2f;
	[Export] public float Radius { get; set; } = 2.8f;
	[Export] public float DamageScale { get; set; } = 0.35f;
	[Export] public float Push { get; set; } = 2f;
	protected override IEnumerable<float> PositiveValues => new float[] { Interval, Radius, DamageScale, Push };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new AnchorPulseRule(director, player, Interval, Radius, DamageScale, Push);
}
