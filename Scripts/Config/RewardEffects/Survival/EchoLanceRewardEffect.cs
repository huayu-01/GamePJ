using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class EchoLanceRewardEffect : SurvivalRewardEffect
{
	[Export] public float DamageScale { get; set; } = 1.1f;
	[Export] public float Interval { get; set; } = 2.2f;
	[Export] public int ExtraPierce { get; set; } = 2;
	protected override IEnumerable<float> PositiveValues => new float[] { DamageScale, Interval, ExtraPierce };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new EchoLanceRule(director, player, DamageScale, Interval, ExtraPierce);
}
