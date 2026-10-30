using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>光束绕自身缓慢旋转。</summary>
[Tool, GlobalClass]
public partial class SlicerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SlicerTraitBehavior(this, owner, wave);
}
