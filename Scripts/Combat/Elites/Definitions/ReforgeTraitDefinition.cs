using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>破甲后可被打断地修甲，每只最多成功一次。</summary>
[Tool, GlobalClass]
public partial class ReforgeTraitDefinition : EliteTraitDefinition
{
	public override bool Supports(Unit owner) => base.Supports(owner)
		&& owner.GetNodeOrNull<CharacterMovement>("Movement")?.PhysicsProfile?.ControlImmune != true
		&& !owner.HasTraitControlImmunity;
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ReforgeTraitBehavior(this, owner, wave);
}
