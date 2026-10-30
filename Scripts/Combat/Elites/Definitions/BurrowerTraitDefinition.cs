using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>预警后生成短暂限制行动的藤蔓。</summary>
[Tool, GlobalClass]
public partial class BurrowerTraitDefinition : EliteTraitDefinition
{
	public override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new BurrowerTraitBehavior(this, owner, wave);
}
