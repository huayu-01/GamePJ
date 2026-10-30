using Godot;

namespace ActRoguelike.Config;

/// <summary>下落攻击独立技能定义，仍可由技能槽、武器和增幅引用。</summary>
[Tool, GlobalClass]
public partial class FallAbilityConfig : AbilityConfig
{
	[ExportGroup("下落攻击")]
	[Export] public override FallAttackTuning? FallAttack { get; set; }
}
