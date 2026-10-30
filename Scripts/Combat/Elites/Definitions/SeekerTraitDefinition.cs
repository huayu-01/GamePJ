using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>可改造直射弹先直飞，再有限追踪。</summary>
[Tool, GlobalClass]
public partial class SeekerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SeekerTraitBehavior(this, owner, wave);
	public override bool Supports(Unit owner) => base.Supports(owner) && EliteProjectileRules.CanSeek(owner);
}
