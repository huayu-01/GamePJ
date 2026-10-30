using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>三颗伤害球环绕本体。</summary>
[Tool, GlobalClass]
public partial class SpinnerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SpinnerTraitBehavior(this, owner, wave);
}
