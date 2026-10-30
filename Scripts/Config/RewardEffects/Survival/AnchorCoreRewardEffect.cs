using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class AnchorCoreRewardEffect : SurvivalRewardEffect
{
	[Export] public float StationarySeconds { get; set; } = 1f;
	[Export] public float Radius { get; set; } = 5f;
	[Export] public float DamageBonus { get; set; } = 0.15f;
	[Export] public float ExpirySeconds { get; set; } = 3f;
	protected override IEnumerable<float> PositiveValues => new float[] { StationarySeconds, Radius, DamageBonus, ExpirySeconds };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new AnchorCoreRule(director, player, StationarySeconds, Radius, DamageBonus, ExpirySeconds);
}
