using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 把增幅项表（<c>Config/Tables/Upgrades/upg_*.tres</c>）编译成运行期目录。
///
/// 这是设计文档 config-schema.md §2.2 的那一层"读表 → 编译"：
/// 磁盘行 <see cref="RewardConfig"/> → 运行期 <see cref="RewardDefinition"/>（带委托）。
///
/// <para>表为空 / 一行都编译不出来 → **空目录**（P5 起不再有 C# 兜底名单，理由见
/// <see cref="RewardCatalog.All"/>），并**在日志里大声留痕** —— 日志能直接证明这一局走的是
/// "表"还是"没有增幅内容"。</para>
/// </summary>
public static class RewardTable
{
	/// <summary>由 <c>ConfigIndex._Ready</c> 在读表之后调用一次。</summary>
	public static void Build(RewardConfig[]? rows)
	{
		if (rows == null || rows.Length == 0)
		{
			RewardCatalog.UseTable(null);
			GameLog.Write("Config", "增幅项表：0 行（来源：无）—— ConfigIndex 的 Upgrades 为空或未找到。P5 起**不再有 C# 兜底目录**（原来那 19 条与表 id 漂移、导致互斥静默失效，见 RewardCatalog.All 的注释），所以本局增幅候选为空。");
			return;
		}

		var compiled = new System.Collections.Generic.List<RewardDefinition>(rows.Length);
		foreach (RewardConfig row in rows)
		{
			RewardDefinition? definition = RewardEffectRegistry.Compile(row);
			if (definition != null)
			{
				compiled.Add(definition);
			}
		}

		if (compiled.Count == 0)
		{
			RewardCatalog.UseTable(null);
			GameLog.Write("Config", $"增幅项表：{rows.Length} 行但一条都没编译出来（来源：无）—— 检查 ApplyHandler 拼写（未注册的 handler 会在上一行警告里点名）。");
			return;
		}

		RewardCatalog.UseTable(compiled.ToArray());
		GameLog.Write("Config", $"增幅项表：{compiled.Count} 条（来源：表）—— 强化 {CountOf(RewardKind.Enhancement)} / 进化 {CountOf(RewardKind.Evolution)} / 异化 {CountOf(RewardKind.Mutation)}。");
	}

	private static int CountOf(RewardKind kind)
	{
		int count = 0;
		foreach (RewardDefinition definition in RewardCatalog.All)
		{
			if (definition.Kind == kind)
			{
				count++;
			}
		}

		return count;
	}
}
