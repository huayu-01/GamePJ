using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>特化与其他增幅共用持有、事务和逐来源撤销入口。</summary>
[Tool, GlobalClass]
public partial class SpecializationRewardEffect : RewardEffectDefinition
{
	[Export] public SpecializationBehaviorDefinition? Definition { get; set; }
	public override IEnumerable<string> Validate()
	{
		if (Definition == null) { yield return "Definition：必须引用特化定义。"; yield break; }
		foreach (string error in Definition.Validate()) yield return error;
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, Definition!);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly SpecializationBehaviorDefinition _definition;
		private System.IDisposable? _lease;
		private System.Action? _rollback;
		public Instance(RewardContext context, string id, SpecializationBehaviorDefinition definition) : base(context, id) => _definition = definition;
		protected override void ApplyCore()
		{
			if (_definition.Kind == SpecializationKind.BloodArmor) _rollback = Context.Player.CaptureRewardTransaction();
			var row = ConfigRows.ById<RewardConfig>(RewardId) ?? new RewardConfig { Id = RewardId, DisplayName = RewardId, Specialization = _definition.Kind };
			_lease = SpecializationRuntime.Ensure(Context.Player).Acquire(row, _definition);
		}
		protected override void RemoveCore() { _lease?.Dispose(); _lease = null; }
		protected override void RollbackCore() => _rollback?.Invoke();
	}
}
