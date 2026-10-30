using System.Collections.Generic;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>一次购买只推进一次节点；收益窗口未打开或冒险取消时退还本次花费。</summary>
[Tool, GlobalClass]
public partial class ShopNodeReward : NodeRewardBehavior
{
	public override IEnumerable<string> Validate() { yield break; }
	public override bool Begin(NodeRewardContext context)
	{
		if (!context.IsValid) return false;
		bool selected = false;
		var row = context.Config;
		int healCost = row.ShopHealCost, rewardCost = row.ShopRewardCost, weaponCost = row.ShopWeaponCost;
		return context.Choice(RewardService.ShopTitle(context.Balance()), new[]
		{
			$"恢复全部生命与护甲（{healCost} 碎片，现有 {context.Balance()}）",
			$"随机一件强化奖励（{rewardCost} 碎片）", $"随机一件进化（改攻击方式）（{weaponCost} 碎片）"
		}, index =>
		{
			if (selected || !context.IsValid) return; selected = true;
			if (index < 0 || index > 2) { context.Complete(); return; }
			int cost = index == 0 ? healCost : index == 1 ? rewardCost : weaponCost;
			if (!context.Spend(cost)) { context.Complete(); return; }
			if (!context.IsValid) { context.Refund(cost); return; }
			bool paid = true;
			void Refund() { if (!paid) return; paid = false; context.Refund(cost); }
			context.RefundOnCancel(Refund);
			try
			{
				if (index == 0) { context.Player.FillVitals(); paid = false; context.Complete(); return; }
				var kind = index == 1 ? RewardNodeKind.Enhancement : RewardNodeKind.Evolution;
				var reward = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind) ?? new NodeConfig { Kind = kind, RewardKind = index == 1 ? RewardKind.Enhancement : RewardKind.Evolution };
				if (!context.Offer(reward, () => { if (!context.IsValid) return; paid = false; context.Complete(); })) { Refund(); context.Complete(); }
			}
			catch { Refund(); context.Cancel(); throw; }
		});
	}
}
