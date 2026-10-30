using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class KillAirJumpRewardEffect : RewardEffectDefinition
{
	[Export] public int ExtraAirJumps { get; set; } = 1;
	[Export] public int JumpCap { get; set; } = 3;
	public override IEnumerable<string> Validate() { if (ExtraAirJumps <= 0 || JumpCap <= 0) yield return "ExtraAirJumps / JumpCap：每次补充量与容量上限必须大于零。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId, new KillAirJumpRule { ExtraAirJumps = ExtraAirJumps, JumpCap = JumpCap });
}
