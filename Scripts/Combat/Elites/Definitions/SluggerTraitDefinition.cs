using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>攻击伤害提高。</summary>
[Tool, GlobalClass]
public partial class SluggerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SluggerTraitBehavior(this, owner, wave);
}
