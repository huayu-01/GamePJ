using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>攻击实际路径预警后产生一次余震。</summary>
[Tool, GlobalClass]
public partial class AftershockTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new AftershockTraitBehavior(this, owner, wave);
}
