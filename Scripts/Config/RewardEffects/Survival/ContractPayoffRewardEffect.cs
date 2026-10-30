using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ContractPayoffRewardEffect : SurvivalRewardEffect, IRewardRuntimeDetails
{
	[Export] public float DamagePerCompletion { get; set; } = 0.03f;
	[Export] public int MaxCompletions { get; set; } = 5;
	protected override IEnumerable<float> PositiveValues => new float[] { DamagePerCompletion, MaxCompletions };
	protected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new ContractPayoffRule(director, player, DamagePerCompletion, MaxCompletions);
	public string DescribeRuntime() => RunController.Instance?.Mode is SurvivalDirector { BuildRuntime: { } runtime } ? runtime.Stopped ? "试炼战斗已结束" : $"本局完成合约：{runtime.Find<ContractPayoffRule>()?.Completed ?? 0}／{MaxCompletions}" : string.Empty;
}
