using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class EchoFamiliarRewardEffect : SurvivalRewardEffect
{
	[Export] public float Interval { get; set; } = 1.5f;
	[Export] public float DamageScale { get; set; } = 0.35f;
	[Export] public float Speed { get; set; } = 30f;
	protected override IEnumerable<float> PositiveValues => new float[] { Interval, DamageScale, Speed };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new EchoFamiliarRule(director, player, Interval, DamageScale, Speed);
}
