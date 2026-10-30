using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>累计有效伤害后预警释放冲击波。</summary>
[Tool, GlobalClass]
public partial class PressureReboundTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new PressureReboundTraitBehavior(this, owner, wave);
}
