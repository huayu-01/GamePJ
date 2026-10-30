using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>移动和恢复加快，保留攻击预警。</summary>
[Tool, GlobalClass]
public partial class SpeederTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new SpeederTraitBehavior(this, owner, wave);
}
