using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>内圈与外环交替爆发，保留安全带。</summary>
[Tool, GlobalClass]
public partial class TidalTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new TidalTraitBehavior(this, owner, wave);
}
