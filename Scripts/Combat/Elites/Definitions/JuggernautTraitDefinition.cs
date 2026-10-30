using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>免普通控制，移速降低，保留持盾自身硬直。</summary>
[Tool, GlobalClass]
public partial class JuggernautTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new JuggernautTraitBehavior(this, owner, wave);
}
