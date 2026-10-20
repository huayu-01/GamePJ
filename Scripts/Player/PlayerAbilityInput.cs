using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Player;

/// <summary>按输入顺序执行当前槽位唯一技能；不扫描技能仓库的InputAction。</summary>
public partial class PlayerAbilityInput : Node
{
	private Unit _unit = null!;
	private readonly List<string> _pendingPresses = new();
	private readonly HashSet<string> _handledPresses = new();
	private readonly HashSet<string> _blockedUntilReleased = new();
	private readonly Dictionary<string, Ability> _inputOwners = new();
	private int _revision;
	public int PendingPressCount => _pendingPresses.Count;
	public override void _Ready() { _unit = GetParent<Unit>(); _revision = _unit.AbilitySlots.Revision; }
	/// <summary>换装清理旧输入；旧按住状态必须先松开，不能变成新技能的蓄力或释放。</summary>
	public void ResetForLoadout()
	{
		var queued = new HashSet<string>(_pendingPresses);
		_pendingPresses.Clear(); _handledPresses.Clear(); _blockedUntilReleased.Clear(); _inputOwners.Clear();
		foreach (string action in _unit.AbilitySlots.Inputs.Keys)
			if (InputMap.HasAction(action) && (Input.IsActionPressed(action) || Input.IsActionJustPressed(action) || queued.Contains(action))) _blockedUntilReleased.Add(action);
		_revision = _unit.AbilitySlots.Revision;
	}
	public override void _Input(InputEvent inputEvent)
	{
		if (inputEvent.IsEcho() || _unit.IsDead) return;
		if (_revision != _unit.AbilitySlots.Revision) ResetForLoadout();
		foreach (string action in _unit.AbilitySlots.Inputs.Keys)
			if (!_blockedUntilReleased.Contains(action) && InputMap.HasAction(action)
				&& Input.IsActionJustPressedByEvent(action, inputEvent)) _pendingPresses.Add(action);
	}
	public override void _Process(double delta)
	{
		if (_revision != _unit.AbilitySlots.Revision) ResetForLoadout();
		if (_unit.IsDead)
		{
			_pendingPresses.Clear();
			foreach (var entry in _unit.AbilitySlots.Equipped) entry.Ability.UpdateInputPreview(false);
			return;
		}
		_handledPresses.Clear();
		var pending = _pendingPresses.ToArray(); _pendingPresses.Clear();
		foreach (string action in pending)
		{
			if (!_blockedUntilReleased.Contains(action)) { _handledPresses.Add(action); Activate(action); }
		}
		foreach (var slot in _unit.AbilitySlots.Inputs)
		{
			string action = slot.Key;
			if (!InputMap.HasAction(action)) continue;
			if (_blockedUntilReleased.Contains(action))
			{
				_unit.AbilitySlots.Resolve(action)?.UpdateInputPreview(false);
				if (!Input.IsActionPressed(action) && !Input.IsActionJustPressed(action)) _blockedUntilReleased.Remove(action);
				continue;
			}
			Ability? ability = _unit.AbilitySlots.Resolve(action);
			if (ability == null) continue;
			if (Input.IsActionJustPressed(action) && !_handledPresses.Contains(action)) Activate(action);
			ability.UpdateInputPreview(Input.IsActionPressed(action));
			if (!ability.HoldToCharge && !ability.HoldToMaintain) continue;
			if (ability.IsActive && Input.IsActionPressed(action)) ability.OnInputHeld((float)delta);
			// 暂停期间松手也可收尾；同帧重按以当前按住状态为准。
			if (ability.IsActive && !Input.IsActionPressed(action)
				&& (Input.IsActionJustReleased(action) || ability.IsMaintaining
					|| (ability.IsCharging && _inputOwners.GetValueOrDefault(action) == ability)))
			{
				ability.OnInputReleased(); _inputOwners.Remove(action);
			}
			if (!ability.IsActive) _inputOwners.Remove(action);
		}
	}
	public static Ability? FindByInputAction(Unit unit, string action) => unit.AbilitySlots.Resolve(action);
	private void Activate(string action)
	{
		Unit? victim = Targeting.FindNearestHostile(_unit, _unit, 200f);
		Ability? ability = _unit.AbilitySlots.Resolve(action);
		if (_unit.AbilitySlots.Press(action, victim) && ability is { HoldToCharge: true } or { HoldToMaintain: true }) _inputOwners[action] = ability;
	}
}
