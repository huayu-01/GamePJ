using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>周期铺设短暂地面火焰。</summary>
[Tool, GlobalClass]
public partial class BurnerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new BurnerTraitBehavior(this, owner, wave);
}
