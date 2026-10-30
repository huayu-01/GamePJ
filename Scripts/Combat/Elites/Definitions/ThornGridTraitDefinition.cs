using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>地板接触触发荆棘效果，跳跃可以躲避。</summary>
[Tool, GlobalClass]
public partial class ThornGridTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ThornGridTraitBehavior(this, owner, wave);
}
