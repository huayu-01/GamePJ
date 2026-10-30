using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>短暂标记玩家经过处，延迟产生有限爆点。</summary>
[Tool, GlobalClass]
public partial class TrailBrandTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new TrailBrandTraitBehavior(this, owner, wave);
}
