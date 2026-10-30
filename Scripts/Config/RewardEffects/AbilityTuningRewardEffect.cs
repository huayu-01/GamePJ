using System;
using System.Collections.Generic;
using System.Reflection;
using ActRoguelike.Abilities;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>可复用的技能参数效果；行为由具体强类型参数资源实现。</summary>
[Tool, GlobalClass]
public partial class AbilityTuningRewardEffect : RewardEffectDefinition
{
	[Export] public AbilityRewardTuning? Tuning { get; set; }
	public override IEnumerable<string> Validate()
	{
		if (Tuning is not IAbilityRewardTuning { TargetAbility: { } target } tuning || string.IsNullOrWhiteSpace(target.Id)) { yield return "Tuning：必须提供支持技能增强的参数资源及目标技能。"; yield break; }
		foreach (var property in Tuning.GetType().GetProperties())
			if (property.GetCustomAttribute<ExportAttribute>() != null && property.GetValue(Tuning) is float number && !float.IsFinite(number)) yield return $"Tuning.{property.Name}：必须为有限数值。";
		if (Tuning is IContentValidatable validatable) foreach (string error in validatable.Validate()) yield return error;
		var type = typeof(Ability).Assembly.GetType($"ActRoguelike.Abilities.{target.ClassHint}");
		if (type != null && !type.IsAbstract && typeof(Ability).IsAssignableFrom(type))
		{
			if (!Tuning.Supports(type)) yield return "Tuning.TargetAbility：目标技能类型不支持此增强。";
		}
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId)
	{
		var copy = (Resource)Tuning!.Duplicate(true);
		return new Instance(context, rewardId, copy);
	}
	private sealed class Instance : RewardEffectInstance
	{
		private readonly Resource _resource; private IDisposable? _lease;
		public Instance(RewardContext context, string id, Resource resource) : base(context, id) => _resource = resource;
		protected override void ApplyCore()
		{
			var tuning = (IAbilityRewardTuning)_resource;
			var ability = Ability.FindByAbilityId(Context.Player, tuning.TargetAbility!.Id);
			if (ability == null || !tuning.Supports(ability)) throw new InvalidOperationException($"{RewardId}：当前角色没有支持此增强的目标技能 {tuning.TargetAbility.Id}。");
			_lease = ability.AcquireRewardParameters(tuning.Apply);
		}
		protected override void RemoveCore() { _lease?.Dispose(); _resource.Dispose(); }
	}
}
