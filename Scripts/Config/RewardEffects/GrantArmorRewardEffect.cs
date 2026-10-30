using System;
using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class GrantArmorRewardEffect : RewardEffectDefinition
{
	[Export] public float Armor { get; set; }
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Armor) || Armor < 0) yield return "Armor：必须为非负有限数值。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, Armor);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly float _amount; private Action? _rollback;
		public Instance(RewardContext context, string id, float amount) : base(context, id) => _amount = amount;
		protected override void ApplyCore() { }
		protected override void CommitCore() { _rollback = Context.Player.CaptureRewardTransaction(); Context.Player.GrantArmor(_amount); }
		protected override void RemoveCore() { }
		protected override void RollbackCore() => _rollback?.Invoke();
	}
}
