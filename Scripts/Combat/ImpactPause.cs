using Godot;

namespace ActRoguelike.Combat;

/// <summary>仅出手单位的短暂停顿。动作、命中盒、动画和运动共用此时钟；输入、冷却、外部世界继续。</summary>
public partial class ImpactPause : Node
{
	public bool IsHolding { get; private set; }
	public float Remaining { get; private set; }
	public int AcceptedCount { get; private set; }
	private float _cooldown;
	private Unit _unit = null!;
	public override void _Ready() { _unit = GetParent<Unit>(); ProcessPhysicsPriority = -1000; SetPhysicsProcess(false); }
	public void Request(float seconds, float cooldown)
	{
		if (_unit.IsDead || seconds <= 0 || _cooldown > 0) return;
		Remaining = seconds; IsHolding = true; _cooldown = Mathf.Max(seconds, cooldown); AcceptedCount++;
		SetPhysicsProcess(true);
		_unit.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.SetImpactHold(true);
	}
	public void Clear()
	{
		Remaining = 0; IsHolding = false;
		_unit.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.SetImpactHold(false);
	}
	public override void _PhysicsProcess(double delta)
	{
		_cooldown = Mathf.Max(0, _cooldown - (float)delta);
		if (_unit.IsDead || !CombatFeedback.Profile.Enabled || !CombatFeedback.Profile.LocalPauseEnabled) { Clear(); return; }
		// 整帧一致：到期帧统一恢复，最多有一个物理步的量化误差，不叠加命中数量。
		Remaining = Mathf.Max(0, Remaining - (float)delta);
		IsHolding = Remaining > 0;
		_unit.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.SetImpactHold(IsHolding);
		if (!IsHolding && _cooldown <= 0) SetPhysicsProcess(false);
	}
}
