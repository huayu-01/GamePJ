using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>预警后沿真实可通行路线移动至侧面。</summary>
[Tool, GlobalClass]
public partial class FlankerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new FlankerTraitBehavior(this, owner, wave);
}
