using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>固定同波成员死亡后增强，最多三次。</summary>
[Tool, GlobalClass]
public partial class AvengerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new AvengerTraitBehavior(this, owner, wave);
}
