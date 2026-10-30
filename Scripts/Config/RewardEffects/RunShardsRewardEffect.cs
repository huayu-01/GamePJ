using System;
using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class RunShardsRewardEffect : RewardEffectDefinition
{
	[Export] public int Amount { get; set; } = 20;
	public override IEnumerable<string> Validate() { if (Amount < 1) yield return "Amount必须为正。"; }
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, Amount);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly int _amount; private RunController? _run; private bool _committed;
		public Instance(RewardContext context, string id, int amount) : base(context, id) => _amount = amount;
		protected override void ApplyCore() { _run = RunController.Instance; if (_run?.GameplayReady != true) throw new InvalidOperationException("资源强化需要进行中的冒险。"); }
		protected override void CommitCore() { if (_run?.CollectBonusShards(Context.Player, _amount) != true) throw new InvalidOperationException("资源强化的持有者不是本局玩家。"); _committed = true; }
		protected override void RollbackCore() { if (_committed && GodotObject.IsInstanceValid(_run)) _run!.RollbackBonusShards(_amount); _committed = false; }
		protected override void RemoveCore() { }
	}
}
