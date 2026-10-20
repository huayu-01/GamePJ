using ActRoguelike.Abilities;
using ActRoguelike.Combat;

namespace ActRoguelike.Testing;

/// <summary>输入隔离探针：即使启用且同键，仓库技能也不应被输入层调用。</summary>
public partial class SlotCounterAbility : Ability
{
	public int PressCount { get; private set; }
	protected override void OnActivate(Unit? target) { PressCount++; Finish(); }
}
