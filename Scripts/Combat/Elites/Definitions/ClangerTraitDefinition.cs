using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>固定同波保护对象死亡后解除无敌。</summary>
[Tool, GlobalClass]
public partial class ClangerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ClangerTraitBehavior(this, owner, wave);
}
