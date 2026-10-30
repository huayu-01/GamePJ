using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WaitEnemyAction : EnemyActionDefinition
{
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Wait;
	public override EnemyActionExecution Create(EnemyActionContext context) => new Execution(context);
	private sealed class Execution : EnemyActionExecution { public Execution(EnemyActionContext context) : base(context) { } }
}
