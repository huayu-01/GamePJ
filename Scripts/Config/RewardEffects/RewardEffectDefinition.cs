using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>可组合、可校验的效果定义；运行状态只存在于Create返回的实例。</summary>
[Tool, GlobalClass]
public abstract partial class RewardEffectDefinition : Resource, IContentValidatable
{
	public abstract RewardEffectInstance Create(RewardContext context, string rewardId);
	public abstract IEnumerable<string> Validate();
}
