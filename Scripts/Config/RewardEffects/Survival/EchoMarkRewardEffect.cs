using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class EchoMarkRewardEffect : SurvivalRewardEffect
{
	[Export] public float Duration { get; set; } = 4f;
	[Export] public float DamageBonus { get; set; } = 0.15f;
	protected override IEnumerable<float> PositiveValues => new float[] { Duration, DamageBonus };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new EchoMarkRule(director, player, Duration, DamageBonus);
}
