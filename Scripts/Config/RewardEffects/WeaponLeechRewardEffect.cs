using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WeaponLeechRewardEffect : RewardEffectDefinition
{
	[Export] public float Ratio { get; set; } = .01f;
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Ratio) || Ratio < 0 || Ratio > 1) yield return "Ratio必须在0至1内。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId, new WeaponLeechRule { Ratio = Ratio });
}
