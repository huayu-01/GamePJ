using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class HealNodeReward : NodeRewardBehavior
{
	/// <summary>负一沿用全局回复比例，非负值覆盖。</summary>
	[Export] public float HealFraction { get; set; } = -1;
	public override bool Begin(NodeRewardContext context)
	{
		if (!context.IsValid) return false;
		context.Player.Heal(context.Player.MaxHealth * (HealFraction < 0 ? ConfigIndex.Settings.NodeHealRatio : HealFraction)); context.Complete(); return true;
	}
	public override IEnumerable<string> Validate() { if (!float.IsFinite(HealFraction) || HealFraction < -1 || HealFraction > 1) yield return "HealFraction：必须为0到1，或-1沿用全局比例。"; }
}
