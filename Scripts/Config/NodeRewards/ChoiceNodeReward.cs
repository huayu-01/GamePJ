using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ChoiceNodeReward : NodeRewardBehavior
{
	public override bool Begin(NodeRewardContext context) => context.Offer(context.Config, context.Complete);
	public override IEnumerable<string> Validate() { yield break; }
}
