using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>仅消费武技1已发生的实际损耗，治疗不进入派生攻击链预算。</summary>
public partial class WeaponLeechRule : Node, IStoppableRewardRule
{
	public float Ratio { get; set; } = .01f;
	public float TotalRequestedHealing { get; private set; }
	private Unit? _owner;
	public override void _Ready() { _owner = GetParent<Unit>(); GameplayEventBus.Raised += OnFact; SetProcess(false); SetPhysicsProcess(false); }
	private void OnFact(GameplayEvent fact)
	{
		if (_owner is not { IsDead: false } || fact.Kind != GameplayEventKind.Damaged || fact.Source != _owner
			|| fact.Target == null || !_owner.IsHostileTo(fact.Target) || fact.DamageApplied <= 0 || fact.IsDamageOverTime
			|| fact.Attack is not { } attack || !(attack.IsManualWeaponAttack || attack.IsWeaponSkillComponent)
			|| ConfigRows.ById<AbilityConfig>(attack.AbilityKind)?.InputAction != "weapon_skill_1") return;
		float amount = fact.DamageApplied * Ratio; TotalRequestedHealing += amount; _owner.Heal(amount);
	}
	public void Stop() { GameplayEventBus.Raised -= OnFact; _owner = null; }
	public override void _ExitTree() => Stop();
}
