using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class RiskContractRewardEffect : SurvivalRewardEffect
{
	[Export] public float ExperienceScale { get; set; } = 1.2f;
	[Export] public float HealthDamageScale { get; set; } = 1.1f;
	protected override IEnumerable<float> PositiveValues => new float[] { ExperienceScale, HealthDamageScale };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new RiskContractRule(director, player, ExperienceScale, HealthDamageScale);
}
