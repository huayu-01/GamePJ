using ActRoguelike.Interaction;
using ActRoguelike.Run;

namespace ActRoguelike.UI;

/// <summary>只读当前挑战规则；不抽取奖励，不改变挑战状态。</summary>
public static class NodeChallengeInfo
{
	public static string Describe(RewardNode node, StageController stage)
	{
		if (node.Kind == RewardNodeKind.Portal) return stage.ExitPrompt;
		if (stage.BossOpened) return "本关普通节点已关闭，请完成首领战并离场。";
		if (!node.SpawnsEncounter) return "领取后无需战斗，不消耗免费挑战次数。";
		string cost = stage.ChallengesUsed < stage.FreeChallengeQuota
			? "本次免费挑战 · 不增加首领压力或词条"
			: "本次超额挑战 · 首领开场压力 +1 · 额外词条规则见 B";
		return $"{NodePlacement.WavesFor(node.Kind)} 波挑战 · {cost}";
	}
}
