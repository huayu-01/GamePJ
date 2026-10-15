using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **武器 2「镰刀」武技2「无情」**（设计文档第 135 行）：
///
/// <code>
/// 武技2[无情]{cd:1}:将武器扔向前方,武器会不断旋转并周期性{0.5}秒的对武器内的敌人造成伤害,
///                  武器飞出去这期间武技1无法使用,再次使用或靠近武器时收回武器.
/// </code>
///
/// <list type="bullet">
/// <item><b>扔向前方</b>：直线飞行（<c>ThrowSpeed</c>），飞到 <c>MaxThrowDistance</c> 停下；
///   <c>Aim = Facing</c>（"前方"= 角色朝向，与文档逐字一致）。</item>
/// <item><b>不断旋转 + 周期性伤害</b>：<see cref="WeaponThrowAbility"/> 那一段（0.5s 一次）。</item>
/// <item><b>飞出去这期间武技1无法使用</b>：<c>WeaponGate</c> 订阅 <c>InFlightChanged</c>
///   去锁同一个武器的连招能力（见 <c>WeaponGate.cs</c> 与 `docs/abilities.md` §8）。</item>
/// <item><b>再次使用 / 靠近收回</b>：<see cref="WeaponThrowAbility.TryPress"/> 与
///   <see cref="WeaponThrowAbility.RecallDistance"/>。</item>
/// </list>
/// </summary>
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

		// 落地就停在那（不穿地）：它从"手上"（约 0.9m 高）抛出，低于起点就算贴地了。
		// ⚠️ 不能用"朝下打射线找地面"—— 反推出来的地面高度**高于**当前武器位置时（例如站在斜坡下），
		// 第一帧就判"落地"、武器根本没飞出去。
		SettleHeight = HomePosition.Y;
		DrawPath(DashPathIndicator.SampleLine(HomePosition, FlightVelocity.Normalized(), EffectiveMaxDistance, PathSteps));
	}

	protected override void OnThrowSettled(Vector3 position)
	{
		// 停在地面上继续旋转、继续周期伤害（文档："武器会不断旋转并周期性的对武器内的敌人造成伤害"）。
	}
}
