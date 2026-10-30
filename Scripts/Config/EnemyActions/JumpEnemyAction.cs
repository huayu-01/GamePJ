using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class JumpEnemyAction : EnemyActionDefinition
{
	[Export] public float Speed { get; set; } = 8;
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Jump;
	public override string[] OwnedStepProperties => new[] { "Kind", "Speed" };
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with { Speed = Speed };
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyJumpExecution(context);
	public override IEnumerable<string> Validate() { if (!float.IsFinite(Speed) || Speed <= 0) yield return "Speed：起跳速度必须大于零且有限。"; }
}
