using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>破甲获得一次护罩，或成功格挡后发出震荡；参数与持有者状态分离。</summary>
[Tool, GlobalClass]
public partial class DefenseReactionRewardEffect : RewardEffectDefinition
{
	public enum ReactionKind { ArmorBreakWard, BlockBurst }
	[Export] public ReactionKind Kind { get; set; }
	[Export] public float Duration { get; set; } = 10;
	[Export] public float Radius { get; set; } = 3;
	[Export] public float Damage { get; set; } = 20;
	[Export] public float Knockback { get; set; } = 8;
	public override IEnumerable<string> Validate()
	{
		if (!System.Enum.IsDefined(Kind)) yield return "Kind：无效防御反应。";
		foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error;
		if (Kind == ReactionKind.ArmorBreakWard && Duration <= 0) yield return "Duration：护罩必须有正持续时间。";
		if (Kind == ReactionKind.BlockBurst && (Radius <= 0 || Damage <= 0)) yield return "Radius/Damage：震荡需要正半径和伤害。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) =>
		new NodeRewardEffectInstance(context, rewardId, new DefenseReactionRule { Definition = this });
}
