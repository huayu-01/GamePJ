using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>非致死破甲后预警反冲，再短暂易伤。</summary>
[Tool, GlobalClass]
public partial class ArmorRecoilTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ArmorRecoilTraitBehavior(this, owner, wave);
}
