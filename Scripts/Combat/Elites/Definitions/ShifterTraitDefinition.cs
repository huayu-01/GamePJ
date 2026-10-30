using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>周期性预警换位，忙动作和空中阶段不触发。</summary>
[Tool, GlobalClass]
public partial class ShifterTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ShifterTraitBehavior(this, owner, wave);
}
