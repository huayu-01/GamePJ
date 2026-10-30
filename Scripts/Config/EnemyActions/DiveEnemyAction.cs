using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class DiveEnemyAction : EnemyHitboxAction
{
	[Export] public float LandingSeconds { get; set; } = .12f;
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Dive;
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with { LandingSeconds = LandingSeconds };
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyDiveExecution(context);
	public override IEnumerable<string> Validate()
	{
		foreach (string error in base.Validate()) yield return error;
		if (Speed <= 0 || !float.IsFinite(LandingSeconds) || LandingSeconds <= 0) yield return "Speed / LandingSeconds：俯冲速度和落地伤害窗口必须大于零且有限。";
	}
}
