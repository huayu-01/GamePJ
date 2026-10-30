using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>本体直接攻击间歇附加击退。</summary>
[Tool, GlobalClass]
public partial class KnockbackTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new KnockbackTraitBehavior(this, owner, wave);
}
