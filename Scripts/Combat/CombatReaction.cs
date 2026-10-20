using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>普通受击与格挡者自身硬直独立；护甲仅保护普通受击，不把护罩变成持盾动作。</summary>
public partial class CombatReaction : Node
{
	[Export] public float HitStunSeconds { get; set; } = 0.18f;
	public float Remaining { get; private set; }
	public bool IsStaggered => Remaining > 0;
	public int HitStunCount { get; private set; }
	public int BlockStunCount { get; private set; }
	private Unit _unit = null!;
	public override void _Ready() => _unit = GetParent<Unit>();
	public override void _PhysicsProcess(double delta) => Remaining = Mathf.Max(0, Remaining - (float)delta);
	public void Reset() => Remaining = 0;
	public void BlockRecoil(float seconds)
	{
		if (_unit.IsDead || seconds <= 0) return;
		BlockStunCount++; Remaining = Mathf.Max(Remaining, seconds); _unit.Movement?.Stop();
		_unit.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.PlayHitReaction();
	}
	public void ReceiveDamage(DamageResult result, HitInstance hit, bool hadArmor)
	{
		if (_unit.Faction != Faction.Enemy || _unit.IsDead || hadArmor || result.FinalDamage <= 0 || result.Blocked
			|| hit.Attack?.IsDamageOverTime == true || _unit.Movement?.IsControlImmune == true) return;
		HitStunCount++; Remaining = Mathf.Max(Remaining, HitStunSeconds);
		var abilities = new System.Collections.Generic.List<Ability>(); NodeSearch.CollectInSubtree(_unit, abilities);
		foreach (var ability in abilities) if (ability.IsBusy && ability.IsInterruptibleNow) ability.InterruptByControl();
		_unit.Movement?.EndDash(); _unit.Movement?.Stop();
		_unit.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.PlayHitReaction();
	}
}
