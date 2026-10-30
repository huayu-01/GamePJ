using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class StatusOnHitRewardEffect : RewardEffectDefinition
{
	[Export] public EffectConfig? Status { get; set; }
	[Export] public float Duration { get; set; } = 3;
	[Export] public float Cooldown { get; set; }
	/// <summary>只供旧行适配，保留旧StatusPresets缺表回退；新资源必须填写强类型Status。</summary>
	internal string LegacyStatusId { get; set; } = string.Empty;
	public override IEnumerable<string> Validate()
	{
		if (Status is not { IsStatus: true } && string.IsNullOrEmpty(LegacyStatusId)) yield return "Status：必须引用可附加的状态资源。";
		if (!float.IsFinite(Duration) || Duration <= 0) yield return "Duration：持续时间必须大于零且有限。";
		if (!float.IsFinite(Cooldown) || Cooldown < 0) yield return "Cooldown：冷却必须非负且有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId,
		new BurnOnHitRule { StatusId = Status?.Id ?? LegacyStatusId, Duration = Duration, Cooldown = Cooldown });
}
