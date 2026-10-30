using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>只消费本人真实击杀事实，不产生攻击，不受连锁触发次数或深度门限制。</summary>
public partial class KillHealthGrowthRule : Node, IStoppableRewardRule
{
	public int KillsPerTrigger { get; set; } = 5;
	public float HealthPerTrigger { get; set; } = 1;
	public float BossBonusHealth { get; set; } = 20;
	public bool FillNewHealth { get; set; } = true;
	public int KillTally { get; private set; }
	public int BossTally { get; private set; }
	private Unit? _owner;
	private bool _subscribed;
	public override void _Ready()
	{
		_owner = GetParent<Unit>(); SetProcess(false); SetPhysicsProcess(false);
		GameplayEventBus.Raised += OnKill; _subscribed = true;
	}
	private void OnKill(GameplayEvent fact)
	{
		if (fact.Kind != GameplayEventKind.Kill || !IsInstanceValid(_owner) || _owner!.IsDead
			|| fact.Source != _owner || !IsInstanceValid(fact.Target) || !_owner.IsHostileTo(fact.Target!)) return;
		KillTally++;
		float amount = KillTally % Mathf.Max(1, KillsPerTrigger) == 0 ? HealthPerTrigger : 0;
		if (fact.Target!.HasTag("Boss")) { BossTally++; amount += BossBonusHealth; }
		if (amount > 0) _owner.GrantMaximumHealthGrowth(amount, FillNewHealth);
	}
	public void Stop()
	{
		if (_subscribed) GameplayEventBus.Raised -= OnKill;
		_subscribed = false; _owner = null;
	}
	public override void _ExitTree() => Stop();
}
