using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class StatRewardEffect : RewardEffectDefinition
{
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();
	public override IEnumerable<string> Validate()
	{
		for (int i = 0; i < Modifiers.Length; i++)
			if (Modifiers[i] == null || string.IsNullOrWhiteSpace(Modifiers[i].Stat) || !float.IsFinite(Modifiers[i].Value))
				yield return $"Modifiers[{i}]：必须填写有效属性和有限数值。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, Modifiers);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly List<StatModifier> _modifiers = new();
		public Instance(RewardContext context, string id, StatOverride[] values) : base(context, id)
		{
			foreach (var value in values) _modifiers.Add(new StatModifier { Stat = value.Stat, Operation = value.Operation, Value = value.Value, Source = context.Build, OriginId = id });
		}
		protected override void ApplyCore() { foreach (var modifier in _modifiers) Context.Player.Stats.AddModifier(modifier); Context.Player.RefreshStats(); }
		protected override void RemoveCore()
		{
			if (!GodotObject.IsInstanceValid(Context.Player)) return;
			foreach (var modifier in _modifiers) Context.Player.Stats.RemoveModifier(modifier);
			Context.Player.RefreshStats();
		}
	}
}
