using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Config;

/// <summary>追踪弹、抛射、炸弹与陨石共用的发射参数；弹体拥有发射后的生命周期。</summary>
[Tool, GlobalClass]
public partial class ProjectileEnemyAction : EnemyActionDefinition
{
	public enum ProjectileMode { HomingShot, ArcShot, Bomb, ArcBomb, Meteor }
	[Export] public ProjectileMode Mode { get; set; }
	/// <summary>可替换弹体场景，根节点必须为 Projectile；留空沿用现有球形样件。</summary>
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public float Damage { get; set; } = 12;
	[Export] public float Radius { get; set; } = 1.5f;
	[Export] public float Speed { get; set; } = 8;
	[Export] public float TravelDistance { get; set; } = 14;
	[Export] public float FlightSeconds { get; set; } = 1;
	[Export] public float TurnDegreesPerSecond { get; set; } = 100;
	public override bool RequiresAttackEvent => true;
	public override Kind? SemanticKind => Mode switch
	{
		ProjectileMode.HomingShot => Kind.Shot, ProjectileMode.ArcShot => Kind.ArcShot,
		ProjectileMode.Bomb => Kind.Bomb, ProjectileMode.ArcBomb => Kind.ArcBomb, _ => Kind.Meteor
	};
	public override string[] OwnedStepProperties => new[] { "Kind", "Damage", "Radius", "Speed", "TravelDistance", "FlightSeconds", "TurnDegreesPerSecond" };
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with
	{ Damage = Damage, Radius = Radius, Speed = Speed, TravelDistance = TravelDistance, FlightSeconds = FlightSeconds, TurnDegreesPerSecond = TurnDegreesPerSecond, ProjectileScene = ProjectileScene };
	public override EnemyActionExecution Create(EnemyActionContext context) => new EnemyProjectileExecution(context);
	public override IEnumerable<string> Validate()
	{
		if (!System.Enum.IsDefined(Mode)) yield return "Mode：必须选择有效的发射方式。";
		if (ProjectileScene is { } scene)
		{
			if (!scene.CanInstantiate()) yield return "ProjectileScene：必须可实例化。";
			else
			{
				Node root = scene.Instantiate(); bool compatible = root is ActRoguelike.Combat.Projectile; root.Free();
				if (!compatible) yield return "ProjectileScene：场景根节点必须使用 Projectile。";
			}
		}
		if (!float.IsFinite(Damage) || Damage < 0) yield return "Damage：伤害必须非负且有限。";
		if (!float.IsFinite(Radius) || Radius <= 0 || !float.IsFinite(Speed) || Speed <= 0) yield return "Radius / Speed：半径和速度必须大于零且有限。";
		if (!float.IsFinite(TravelDistance) || TravelDistance <= 0 || !float.IsFinite(FlightSeconds) || FlightSeconds <= 0) yield return "TravelDistance / FlightSeconds：航程和飞行时间必须大于零且有限。";
		if (!float.IsFinite(TurnDegreesPerSecond) || TurnDegreesPerSecond < 0) yield return "TurnDegreesPerSecond：转向率必须非负且有限。";
	}
}
