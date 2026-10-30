using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>免费召唤有限一批普通随从，不补员。</summary>
[Tool, GlobalClass]
public partial class SummonerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SummonerTraitBehavior(this, owner, wave);
}
