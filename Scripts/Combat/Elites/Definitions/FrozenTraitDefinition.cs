using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>冰晶延迟爆炸，短暂冻结范围内玩家。</summary>
[Tool, GlobalClass]
public partial class FrozenTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new FrozenTraitBehavior(this, owner, wave);
}
