using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>适用射击改为三发，总伤害受限。</summary>
[Tool, GlobalClass]
public partial class MultishotTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new MultishotTraitBehavior(this, owner, wave);
	public override bool Supports(Unit owner) => base.Supports(owner) && EliteProjectileRules.CanMultishot(owner);
}
