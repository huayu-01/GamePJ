using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>进入暗影罩内才可伤害本体。</summary>
[Tool, GlobalClass]
public partial class SpillerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SpillerTraitBehavior(this, owner, wave);
}
