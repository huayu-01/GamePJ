using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class JumpPulseRewardEffect : RewardEffectDefinition
{
	[Export] public float Radius { get; set; } = 3;
	[Export] public float Damage { get; set; } = 15;
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(Radius) || Radius <= 0 || !float.IsFinite(Damage) || Damage < 0) yield return "Radius / Damage：起跳范围必须大于零，伤害必须非负且有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId, new JumpPulseRule { Radius = Radius, Damage = Damage });
}
