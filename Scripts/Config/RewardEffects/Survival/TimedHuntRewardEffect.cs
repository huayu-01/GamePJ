using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class TimedHuntRewardEffect : SurvivalRewardEffect
{
	[Export] public float Interval { get; set; } = 50f;
	[Export] public float Duration { get; set; } = 15f;
	[Export] public float SpeedScale { get; set; } = 1.2f;
	[Export] public int ExperienceReward { get; set; } = 14;
	protected override IEnumerable<float> PositiveValues => new float[] { Interval, Duration, SpeedScale, ExperienceReward };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new TimedHuntRule(director, player, Interval, Duration, SpeedScale, ExperienceReward);
}
