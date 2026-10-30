using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>预警后放置固定节点旋转激光。</summary>
[Tool, GlobalClass]
public partial class ArcaneTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new ArcaneTraitBehavior(this, owner, wave);
}
