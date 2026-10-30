using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>护甲独立节点，与恢复节点共用收益会话和单次完成规则。</summary>
[Tool, GlobalClass]
public partial class ArmorNodeReward : NodeRewardBehavior
{
	[Export] public float Armor { get; set; } = 30;
	public override bool Begin(ActRoguelike.Rewards.NodeRewardContext context)
	{
		if (!context.IsValid) return false;
		context.Player.GrantArmor(Armor); context.Complete(); return true;
	}
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Armor) || Armor <= 0) yield return "Armor：护甲奖励必须为有限正值。"; }
}
