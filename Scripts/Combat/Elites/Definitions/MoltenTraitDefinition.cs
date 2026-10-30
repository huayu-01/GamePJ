using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>移动留火路，自然死亡后预警爆炸。</summary>
[Tool, GlobalClass]
public partial class MoltenTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new MoltenTraitBehavior(this, owner, wave);
}
