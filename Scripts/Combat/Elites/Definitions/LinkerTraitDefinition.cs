using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>与同波友军形成有预警的伤害连线。</summary>
[Tool, GlobalClass]
public partial class LinkerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new LinkerTraitBehavior(this, owner, wave);
}
