using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class SupplyDropRewardEffect : RewardEffectDefinition, IRewardRuntimeDetails
{
	[Export] public float IntervalSeconds { get; set; } = 10;
	[Export] public float MinRadius { get; set; } = 6;
	[Export] public float MaxRadius { get; set; } = 10;
	[Export] public float PickupLifetime { get; set; } = 30;
	[Export] public int MaxOutstanding { get; set; } = 5;
	[Export] public SupplyOptionDefinition[] Options { get; set; } = System.Array.Empty<SupplyOptionDefinition>();
	public override IEnumerable<string> Validate()
	{
		foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error;
		if (IntervalSeconds <= 0 || PickupLifetime <= 0 || MaxRadius < MinRadius || MaxOutstanding < 1 || Options.Length == 0) yield return "空投间隔、留场、数量、范围与内容必须有效。";
		foreach (var option in Options) foreach (var error in option.Validate()) yield return error;
		if (Options.Sum(o => o.AdventureWeight) <= 0 || Options.Sum(o => o.SurvivalWeight) <= 0) yield return "两个模式都必须有可抽取的补给。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId,
		new SupplyDropRule { IntervalSeconds = IntervalSeconds, MinRadius = MinRadius, MaxRadius = MaxRadius, PickupLifetime = PickupLifetime, MaxOutstanding = MaxOutstanding, Options = Options });
	public string DescribeRuntime() => string.Join("\n", Options.Select(option => option.Status != null
		? option.LocalizedName + "：" + option.Status.LocalizedDescription
		: option.LocalizedName + " +" + option.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
			+ (option.Kind == SupplyKind.Experience ? "（" + ContentText.Get("ui.supply.survival_only", "死战限定") + "）" : "")));
}
