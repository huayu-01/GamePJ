using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>同组合法对象分担伤害，转移不再递归。</summary>
[Tool, GlobalClass]
public partial class HealthLinkTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new HealthLinkTraitBehavior(this, owner, wave);
}
