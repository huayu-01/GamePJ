using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>周期阻挡来自罩外的指定远程投射物。</summary>
[Tool, GlobalClass]
public partial class SuppressorTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SuppressorTraitBehavior(this, owner, wave);
}
