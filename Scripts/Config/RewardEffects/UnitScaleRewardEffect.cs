using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>可撤销的整单位体型变化；模型、身体与攻击体同步，TopLevel镜头保持独立。</summary>
[Tool, GlobalClass]
public partial class UnitScaleRewardEffect : RewardEffectDefinition
{
	[Export] public float SizeIncrease { get; set; } = .2f;
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(SizeIncrease) || SizeIncrease <= -.95f || SizeIncrease > 5)
			yield return "SizeIncrease：必须为大于-0.95且不超过5的有限数值。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, 1 + SizeIncrease);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly float _factor;
		private bool _applied;
		public Instance(RewardContext context, string id, float factor) : base(context, id) => _factor = factor;
		protected override void ApplyCore() { Context.Player.Scale *= _factor; _applied = true; }
		protected override void RemoveCore()
		{
			if (_applied && GodotObject.IsInstanceValid(Context.Player)) Context.Player.Scale /= _factor;
			_applied = false;
		}
	}
}
