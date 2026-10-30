using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ScavengerCompassRewardEffect : SurvivalRewardEffect
{
	[Export] public float RadiusScale { get; set; } = 1.5f;
	[Export] public float SpeedScale { get; set; } = 1.25f;
	protected override IEnumerable<float> PositiveValues => new float[] { RadiusScale, SpeedScale };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new ScavengerCompassRule(director, player, RadiusScale, SpeedScale);
}
