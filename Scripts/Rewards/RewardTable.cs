using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 把增幅项表（<c>Config/Tables/Upgrades/upg_*.tres</c>）编译成运行期目录。
///
/// 这是设计文档 config-schema.md §2.2 的那一层"读表 → 编译"：
/// 磁盘行 <see cref="RewardConfig"/> → 运行期 <see cref="RewardDefinition"/>（带委托）。
/// 表为空 / 一行都编译不出来 → 回到 <see cref="RewardCatalog.Fallback"/>，并**在日志里留痕**
/// （日志能直接证明这一局走的是"表"还是"代码默认值"）。
/// </summary>
public static class RewardTable
{
	/// <summary>由 <c>ConfigIndex._Ready</c> 在读表之后调用一次。</summary>
	public static void Build(RewardConfig[]? rows)
	{
		if (rows == null || rows.Length == 0)
		{
			RewardCatalog.UseTable(null);
			GameLog.Write("Config", $"增幅项表：0 条（来源：代码默认值）—— ConfigIndex 的 Upgrades 为空或未找到，使用 RewardCatalog.Fallback 的 {RewardCatalog.All.Length} 条。");
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
			GameLog.Write("Config", $"增幅项表：{rows.Length} 行但一条都没编译出来（来源：代码默认值）—— 检查 ApplyHandler 拼写。");
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
