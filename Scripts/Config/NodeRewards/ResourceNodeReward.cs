using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ResourceNodeReward : NodeRewardBehavior
{
	[Export] public int Shards { get; set; } = 5;
	public override bool Begin(NodeRewardContext context) { if (!context.IsValid) return false; context.Refund(Shards); context.Complete(); return true; }
	public override IEnumerable<string> Validate() { if (Shards < 0) yield return "Shards：不能为负数。"; }
}
