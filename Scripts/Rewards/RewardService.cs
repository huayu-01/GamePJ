using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Interaction;

namespace ActRoguelike.Rewards;

/// <summary>
/// 奖励分派规则（从 RunController 抽出的纯逻辑，不持有状态、不碰界面）。
///
/// 职责只有三件：节点类型 → 池过滤 / 界面标题 / 商店报价。
/// "开哪个界面、选完干什么"仍然留在 RunController，这样规则和编排分开。
///
/// **配置表阶段 2**：三件事都改成**优先读节点表**（`Config/Tables/Nodes/node_*.tres`），
/// 读不到行时回退到下面代码里原来的 switch —— 这就是"读不到表就用自身默认值"在节点表上的落地。
/// </summary>
public static class RewardService
{
	/// <summary>节点类型 → 奖励池过滤（null = 不按类型过滤，走全池）。</summary>
	public static RewardKind? FilterFor(RewardNodeKind kind)
	{
		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
		if (row != null)
		{
			return row.AnyRewardKind ? null : row.RewardKind;
		}

		return FallbackFilter(kind);
	}

	/// <summary>节点类型 → 界面标题。</summary>
	public static string TitleFor(RewardNodeKind kind)
	{
		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
		if (row != null && !string.IsNullOrEmpty(row.Title))
		{
			return row.Title;
		}

		return FallbackTitle(kind);
	}

	/// <summary>商店三项报价（标题里会带上当前碎片数）。价目来自节点表的 <c>node_shop</c> 行。</summary>
	public static List<string> ShopOffers(int shards) => new()
	{
		$"恢复全部生命与护甲（{ShopCost(0)} 碎片，现有 {shards}）",
		$"随机一件强化奖励（{ShopCost(1)} 碎片）",
		$"随机一件进化（改攻击方式）（{ShopCost(2)} 碎片）",
	};

	/// <summary>商店价目（下标 0/1/2 = 恢复 / 强化 / 进化）。节点表读不到时用分表前的 5 / 12 / 18。</summary>
	public static int ShopCost(int index)
	{
		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(RewardNodeKind.Shop);
		return index switch
		{
			0 => row?.ShopHealCost ?? 5,
			1 => row?.ShopRewardCost ?? 12,
			_ => row?.ShopWeaponCost ?? 18,
		};
	}

	/// <summary>商店标题（带碎片余额）。</summary>
	public static string ShopTitle(int shards) => $"补给站 · 当前碎片 {shards}";

	/// <summary>节点表读不到时的兜底（= 分表前的那份 switch，数值逐条一致）。</summary>
	private static RewardKind? FallbackFilter(RewardNodeKind kind) => kind switch
	{
		// 没有「武器池」这个类型（用户 A3 拍板）：那个叫法指的就是**进化池** ——
		// 武器（= 角色的攻击方式）的限定池 = 改造攻击方式（初始武器选定后不可更换）。
		RewardNodeKind.Weapon => RewardKind.Evolution,
		RewardNodeKind.Enhancement => RewardKind.Enhancement,
		RewardNodeKind.Evolution => RewardKind.Evolution,
		RewardNodeKind.Mutation => RewardKind.Mutation,
		_ => null,
	};

	/// <summary>节点表读不到时的兜底标题。</summary>
	private static string FallbackTitle(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Weapon => "进化池",
		RewardNodeKind.Enhancement => "强化池",
		RewardNodeKind.Evolution => "进化池",
		RewardNodeKind.Mutation => "异化池",
		RewardNodeKind.Resource => "局外资源",
		RewardNodeKind.Heal => "恢复状态",
		RewardNodeKind.Special => "特殊奖励",
		RewardNodeKind.Shop => "补给站",
		_ => "奖励",
	};
}
