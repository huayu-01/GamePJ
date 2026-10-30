using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class EnergyEchoRewardEffect : SurvivalRewardEffect
{
	[Export] public float Threshold { get; set; } = 20f;
	[Export] public float Radius { get; set; } = 3f;
	[Export] public float DamageScale { get; set; } = 0.4f;
	protected override IEnumerable<float> PositiveValues => new float[] { Threshold, Radius, DamageScale };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new EnergyEchoRule(director, player, Threshold, Radius, DamageScale);
}
