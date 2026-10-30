using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class BurrowEnemyAction : EnemyActionDefinition
{
	[Export] public float MinDistance { get; set; } = 4;
	[Export] public float MaxDistance { get; set; } = 8;
	[Export] public float Depth { get; set; } = 1.5f;
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Burrow;
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with { BurrowMinDistance = MinDistance, BurrowMaxDistance = MaxDistance, BurrowDepth = Depth };
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyBurrowExecution(context);
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(MinDistance) || MinDistance < 0 || !float.IsFinite(MaxDistance) || MaxDistance < MinDistance) yield return "MinDistance / MaxDistance：出土环必须有限且满足 0 ≤ 最小距离 ≤ 最大距离。";
		if (!float.IsFinite(Depth) || Depth < 0) yield return "Depth：表现下沉深度必须非负且有限。";
	}
}
