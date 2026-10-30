using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>无敌与开放期交替，保护来源独立。</summary>
[Tool, GlobalClass]
public partial class ShieldingTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ShieldingTraitBehavior(this, owner, wave);
}
