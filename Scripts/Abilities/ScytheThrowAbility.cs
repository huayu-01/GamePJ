using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>无情：向角色前方飞到射程后悬停旋转，持续周期命中，再次使用或靠近收回。</summary>
public partial class ScytheThrowAbility : WeaponThrowAbility
{
	[ExportGroup("镰刀（前方直线抛出）")]
	/// <summary>抛出的垂直初速（0 = 纯水平，"扔向前方"）。</summary>
	[Export] public float UpwardSpeed { get; set; }

	protected override float LaunchUpwardSpeed => UpwardSpeed;

	protected override void Launch(Vector3 flatDirection)
	{
		FlightGravity = 0f;
		FlightVelocity = (flatDirection * EffectiveThrowSpeed) + (Vector3.Up * UpwardSpeed);

		// 到前方水平射程时由基类停留，地形阻挡使用真实World扫线。
		DrawPath(DashPathIndicator.SampleLine(HomePosition, FlightVelocity.Normalized(), EffectiveMaxDistance, PathSteps));
	}

	protected override void OnThrowSettled(Vector3 position)
	{
		// 停在地面上继续旋转、继续周期伤害（文档："武器会不断旋转并周期性的对武器内的敌人造成伤害"）。
	}
}
