using ActRoguelike.Config;
using ActRoguelike.Interaction;

namespace ActRoguelike.Rewards;

/// <summary>未声明收益资源的旧节点兼容；新节点直接绑定RewardBehavior。</summary>
internal static class NodeRewardCompatibility
{
	public static NodeRewardBehavior Behavior(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Shop => new ShopNodeReward(), RewardNodeKind.Resource => new ResourceNodeReward(),
		RewardNodeKind.Heal => new HealNodeReward(), _ => new ChoiceNodeReward()
	};
}
