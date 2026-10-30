using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class HappyRunnerRewardEffect : RewardEffectDefinition
{
	[Export] public float DamageFreeSeconds { get; set; } = 10;
	[Export] public float HealPerSecond { get; set; } = 1;
	[Export] public float PushSpeed { get; set; } = 5;
	[Export] public float PushCooldown { get; set; } = .5f;
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(DamageFreeSeconds) || DamageFreeSeconds < 0 || !float.IsFinite(HealPerSecond) || HealPerSecond < 0) yield return "DamageFreeSeconds / HealPerSecond：无伤时长和恢复速度必须非负且有限。";
		if (!float.IsFinite(PushSpeed) || PushSpeed < 0 || !float.IsFinite(PushCooldown) || PushCooldown <= 0) yield return "PushSpeed / PushCooldown：推力必须非负，间隔必须大于零且有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new NodeRewardEffectInstance(context, rewardId,
		new HappyRunnerRule { DamageFreeSeconds = DamageFreeSeconds, HealPerSecond = HealPerSecond, PushSpeed = PushSpeed, PushCooldown = PushCooldown });
}
