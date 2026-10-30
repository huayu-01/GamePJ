using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>本体直接攻击按实际生命伤害吸血。</summary>
[Tool, GlobalClass]
public partial class VampiricTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new VampiricTraitBehavior(this, owner, wave);
}
