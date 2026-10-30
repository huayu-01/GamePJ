using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>节点收益策略；定义无本局状态，取消与单次完成由会话持有。</summary>
[Tool, GlobalClass]
public abstract partial class NodeRewardBehavior : Resource, IContentValidatable
{
	public abstract bool Begin(NodeRewardContext context);
	public abstract IEnumerable<string> Validate();
}
