using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class AnchorAfterglowRewardEffect : SurvivalRewardEffect
{
	[Export] public float RadiusBonus { get; set; } = 1f;
	[Export] public float LingerSeconds { get; set; } = 2f;
	protected override IEnumerable<float> PositiveValues => new float[] { RadiusBonus, LingerSeconds };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new AnchorAfterglowRule(director, player, RadiusBonus, LingerSeconds);
}
