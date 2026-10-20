using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>当前配装技能索引：输入先查槽，再执行唯一实例；技能仓库与槽位分离。</summary>
public sealed class AbilitySlots
{
	public sealed record Entry(AbilityConfig Row, Ability Ability, string InputSlot);
	private readonly Dictionary<string, Entry> _inputs = new();
	private readonly List<Entry> _equipped = new();
	public IReadOnlyDictionary<string, Entry> Inputs => _inputs;
	public IReadOnlyList<Entry> Equipped => _equipped;
	public bool IsConfigured { get; private set; }
	public int Revision { get; private set; }
	private bool _captured;
	public UnitPhysicsProfile? BasePhysicsProfile { get; private set; }
	public int BaseAirJumps { get; private set; }
	public void CaptureDefaults(Unit unit)
	{
		if (_captured) return;
		_captured = true; BasePhysicsProfile = unit.Movement?.PhysicsProfile; BaseAirJumps = unit.Movement?.ExtraAirJumps ?? 0;
	}
	public void Clear() { IsConfigured = true; Revision++; _inputs.Clear(); _equipped.Clear(); }
	public void Assign(Unit unit, IEnumerable<AbilityConfig> rows)
	{
		foreach (AbilityConfig row in rows)
		{
			if (row != null) AssignToSlot(unit, row.InputAction, row);
		}
	}
	/// <summary>显式装入槽位；技能行的InputAction只是默认槽，装槽后不再决定输入路由。</summary>
	public bool AssignToSlot(Unit unit, string inputSlot, AbilityConfig row)
	{
		if (unit.GetNodeOrNull<Ability>(row.NodeName) is not { } ability || ability.AbilityId != row.Id) return false;
		var entry = new Entry(row, ability, inputSlot);
		if (!string.IsNullOrEmpty(inputSlot) && !_inputs.TryAdd(inputSlot, entry))
			throw new System.InvalidOperationException($"技能槽{inputSlot}重复装入：{_inputs[inputSlot].Row.Id}／{row.Id}");
		_equipped.Add(entry); IsConfigured = true; Revision++; return true;
	}
	public Ability? Resolve(string action)
		=> _inputs.TryGetValue(action, out Entry? entry) && GodotObject.IsInstanceValid(entry.Ability)
			&& entry.Ability.Enabled && entry.Ability.AbilityId == entry.Row.Id ? entry.Ability : null;
	public bool Owns(Ability ability) => _equipped.Exists(entry => entry.Ability == ability && ability.AbilityId == entry.Row.Id);
	public bool Press(string action, Unit? target = null) => Resolve(action)?.TryPress(target) ?? false;
}
