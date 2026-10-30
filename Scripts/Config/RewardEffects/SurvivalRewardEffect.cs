using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

/// <summary>效果资源只冻结参数并安装行为；清理沿公共奖励事务执行。</summary>
public abstract partial class SurvivalRewardEffect : RewardEffectDefinition
{
	protected abstract SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player);
	protected virtual IEnumerable<float> PositiveValues => System.Array.Empty<float>();
	public override IEnumerable<string> Validate()
	{
		foreach (float value in PositiveValues) if (!float.IsFinite(value) || value <= 0) yield return "生存增幅参数必须是有限正数。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId)
	{
		if (RunController.Instance?.Mode is not SurvivalDirector director || director.Player != context.Player || !director.Active || director.BuildRuntime == null)
			throw new InvalidOperationException("生存增幅只能安装在当前试炼的玩家上。");
		return new Instance(context, rewardId, director.BuildRuntime, CreateRule(director, context.Player));
	}
	private sealed class Instance : RewardEffectInstance
	{
		private readonly SurvivalBuildRuntime _runtime; private readonly SurvivalAugmentRule _rule;
		public Instance(RewardContext context, string id, SurvivalBuildRuntime runtime, SurvivalAugmentRule rule) : base(context, id) { _runtime = runtime; _rule = rule; }
		protected override void ApplyCore() { _runtime.AddChild(_rule); }
		protected override void RemoveCore()
		{
			if (!GodotObject.IsInstanceValid(_rule)) return; _rule.Stop();
			if (_rule.IsInsideTree()) { if (!IsTreeExitCleanup) _rule.GetParent()?.RemoveChild(_rule); _rule.QueueFree(); } else _rule.Free();
		}
	}
}
