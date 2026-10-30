using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>体型与生命提高，最终身体参与导航净空。</summary>
[Tool, GlobalClass]
public partial class BruiserTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new BruiserTraitBehavior(this, owner, wave);
}
