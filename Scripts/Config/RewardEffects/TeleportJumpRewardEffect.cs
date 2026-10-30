using System;
using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class TeleportJumpRewardEffect : RewardEffectDefinition
{
	[Export] public float HeightMultiplier { get; set; } = 3;
	[Export] public float HoverSeconds { get; set; } = .5f;
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(HeightMultiplier) || HeightMultiplier <= 0 || !float.IsFinite(HoverSeconds) || HoverSeconds < 0)
			yield return "跳高倍率必须为正数，浮空秒数必须非负且有限。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, HeightMultiplier, HoverSeconds);
	private sealed class Instance : RewardEffectInstance
	{
		private IDisposable? _lease;
		private readonly float _height, _hover;
		public Instance(RewardContext context, string id, float height, float hover) : base(context, id) { _height = height; _hover = hover; }
		protected override void ApplyCore() => _lease = Context.Player.Movement?.AcquireTeleportJump(_height, _hover);
		protected override void RemoveCore() => _lease?.Dispose();
	}
}
