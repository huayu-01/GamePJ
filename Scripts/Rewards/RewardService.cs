using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Interaction;

namespace ActRoguelike.Rewards;

/// <summary>
/// 奖励分派规则（从 RunController 抽出的纯逻辑，不持有状态、不碰界面）。
///
/// 职责只有三件：节点类型 → 池过滤 / 界面标题 / 商店报价。
/// "开哪个界面、选完干什么"仍然留在 RunController，这样规则和编排分开。
/// </summary>
public static class RewardService
{
	/// <summary>节点类型 → 奖励池过滤（null = 不按类型过滤，走全池）。</summary>
	public static RewardKind? FilterFor(RewardNodeKind kind) => kind switch
	{
		// 武器池 = 改造武器的攻击方式（用户澄清：初始武器选定后不可更换）。
		RewardNodeKind.Weapon => RewardKind.Evolution,
		RewardNodeKind.Enhancement => RewardKind.Enhancement,
		RewardNodeKind.Evolution => RewardKind.Evolution,
		RewardNodeKind.Mutation => RewardKind.Mutation,
		_ => null,
	};

	/// <summary>节点类型 → 界面标题。</summary>
	public static string TitleFor(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Weapon => "武器池",
		RewardNodeKind.Enhancement => "强化池",
		RewardNodeKind.Evolution => "进化池",
		RewardNodeKind.Mutation => "异化池",
		RewardNodeKind.Resource => "局外资源",
		RewardNodeKind.Heal => "恢复状态",
		RewardNodeKind.Special => "特殊奖励",
		RewardNodeKind.Shop => "补给站",
		_ => "奖励",
	};

	/// <summary>商店三项报价（标题里会带上当前碎片数）。</summary>
	public static List<string> ShopOffers(BalanceConfig config, int shards) => new()
	{
		$"恢复全部生命与护甲（{config.ShopHealCost} 碎片，现有 {shards}）",
		$"随机一件强化奖励（{config.ShopRewardCost} 碎片）",
		$"随机一件进化（改攻击方式）（{config.ShopWeaponCost} 碎片）",
	};

	/// <summary>商店标题（带碎片余额）。</summary>
	public static string ShopTitle(int shards) => $"补给站 · 当前碎片 {shards}";
}
