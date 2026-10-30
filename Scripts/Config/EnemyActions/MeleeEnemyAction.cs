using ActRoguelike.Abilities;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class MeleeEnemyAction : EnemyHitboxAction
{
	[Export] public bool Spin { get; set; }
	public override Kind? SemanticKind => Spin ? Kind.Spin : Kind.Melee;
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyMeleeExecution(context);
}
