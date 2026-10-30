using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ChainLightningRewardEffect : RewardEffectDefinition
{
	[Export] public float Damage { get; set; } = 14;
	[Export] public float Range { get; set; } = 7;
	[Export] public int Jumps { get; set; } = 3;
	[Export] public float Cooldown { get; set; } = .12f;
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(Damage) || Damage < 0 || !float.IsFinite(Range) || Range <= 0 || Jumps <= 0 || !float.IsFinite(Cooldown) || Cooldown < 0) yield return "连锁闪电：伤害／冷却须非负，距离／跳数须为正，数值须有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId, new ChainLightningRule { Damage = Damage, Range = Range, Jumps = Jumps, Cooldown = Cooldown });
}
