namespace ActRoguelike.Rewards;

/// <summary>
/// 本局结算规则（从 RunController 抽出的纯逻辑）。
/// 收益公式与文案集中在这里，改数值不用翻流程代码。
/// </summary>
public static class RunSettlement
{
	/// <summary>每推进一关的基础收益。</summary>
	public const int ShardsPerStage = 10;

	/// <summary>每次节点挑战的收益。</summary>
	public const int ShardsPerChallenge = 3;

	/// <summary>通关额外奖励。</summary>
	public const int ShardsForVictory = 50;

	/// <summary>本局收益 = 关卡进度 + 挑战次数 + 通关奖励。</summary>
	public static int ShardsFor(int stageIndex, int challengesUsed, bool victory)
		=> (ShardsPerStage * stageIndex) + (ShardsPerChallenge * challengesUsed) + (victory ? ShardsForVictory : 0);

	/// <summary>结算正文（展示给玩家的那段文字）。</summary>
	public static string Describe(bool victory, string stageName, string buildSummary, int earned, int total, float seconds)
	{
		string headline = victory ? "通关！三关全部清理" : $"倒在了 {stageName}";
		return $"{headline}，用时 {seconds:0} 秒\n" +
			   $"{buildSummary}\n" +
			   $"获得碎片 +{earned}（累计 {total}）";
	}
}
