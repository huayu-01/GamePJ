using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>来源限定的命中标记与按已有标记加伤；同一个组件服务不同技能组合。</summary>
[Tool, GlobalClass]
public partial class MarkedBonusRewardEffect : RewardEffectDefinition
{
	[Export] public EffectConfig? Status { get; set; }
	[Export] public AbilityConfig? MarkAbility { get; set; }
	[Export] public AbilityConfig? BonusAbility { get; set; }
	[Export] public float Duration { get; set; } = 6;
	[Export] public float Cooldown { get; set; }
	[Export] public float FlatBonus { get; set; }
	[Export] public float PerStackBonus { get; set; }
	internal string LegacyStatusId { get; set; } = string.Empty;
	public override IEnumerable<string> Validate()
	{
		if (Status is not { IsStatus: true } && string.IsNullOrEmpty(LegacyStatusId)) yield return "Status：必须引用可附加的状态资源。";
		if (!float.IsFinite(Duration) || Duration <= 0) yield return "Duration：持续时间必须大于零且有限。";
		if (!float.IsFinite(Cooldown) || Cooldown < 0 || !float.IsFinite(FlatBonus) || FlatBonus < 0 || !float.IsFinite(PerStackBonus) || PerStackBonus < 0) yield return "Cooldown / Bonus：冷却与加成必须非负且有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId, new MarkedBonusRule
	{
		StatusId = Status?.Id ?? LegacyStatusId, MarkAbilityId = MarkAbility?.Id ?? string.Empty, BonusAbilityId = BonusAbility?.Id ?? string.Empty,
		Duration = Duration, Cooldown = Cooldown, FlatBonus = FlatBonus, PerStackBonus = PerStackBonus
	});
}
