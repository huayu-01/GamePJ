using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ChargeEnemyAction : EnemyHitboxAction
{
	[Export] public bool Homing { get; set; }
	[Export] public float TravelDistance { get; set; } = 14;
	[Export] public float TurnDegreesPerSecond { get; set; } = 100;
	public override string[] OwnedStepProperties => new[] { "Kind", "Damage", "Radius", "Speed", "Centered", "TravelDistance", "TurnDegreesPerSecond" };
	public override Kind? SemanticKind => Homing ? Kind.HomingCharge : Kind.Charge;
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with { TravelDistance = TravelDistance, TurnDegreesPerSecond = TurnDegreesPerSecond };
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyChargeExecution(context);
	public override IEnumerable<string> Validate()
	{
		foreach (string error in base.Validate()) yield return error;
		if (Speed <= 0 || !float.IsFinite(TravelDistance) || TravelDistance <= 0) yield return "TravelDistance / Speed：冲锋距离和速度必须大于零且有限。";
		if (!float.IsFinite(TurnDegreesPerSecond) || TurnDegreesPerSecond < 0) yield return "TurnDegreesPerSecond：转向率必须非负且有限。";
	}
}
