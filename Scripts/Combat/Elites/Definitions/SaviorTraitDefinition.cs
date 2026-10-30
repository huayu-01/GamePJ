using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>周期授予附近友军一次性挡伤护罩。</summary>
[Tool, GlobalClass]
public partial class SaviorTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SaviorTraitBehavior(this, owner, wave);
}
