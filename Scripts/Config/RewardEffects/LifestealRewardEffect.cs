using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class LifestealRewardEffect : RewardEffectDefinition
{
	[Export] public float Ratio { get; set; } = .12f;
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Ratio) || Ratio < 0) yield return "Ratio：吸血比例必须是非负有限数值。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId, new LifestealRule { Ratio = Ratio });
}
