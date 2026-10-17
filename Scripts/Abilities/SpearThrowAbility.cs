using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **武器 3「长枪」武技2「长虹贯日」**（设计文档第 151–153 行）：
///
/// <code>
/// 武技2[长虹贯日]{cd:0.5}:基础蓄力{1}s,朝准星方向抛掷长枪,路径呈现抛物线(有路径可视化指示器),
///   每蓄力{0.25}秒增强伤害{20%},弹速{20}%路径中线延长{20}%,最多蓄力1秒;
///   当长枪丢出去后可再次使用技能将长枪收回,回来时能再次对路径中的敌人造成伤害;
/// </code>
///
/// <list type="bullet">
/// <item><b>基础蓄力 1s + 后置蓄力每 0.25s 一档</b>：基类的三段状态机（
///   <c>ChargeBaseSeconds = 1</c> / <c>ChargeStepSeconds = 0.25</c> / <c>ChargeMaxSeconds = 2</c>）；
///   <b>松手才发射</b>（与角色 A 的能量掌武技2 同一条纪律，见 `docs/abilities.md` §1.3）。</item>
/// <item><b>每档 +20% 伤害 / +20% 弹速 / +20% 路径中线</b>：伤害与弹速走
///   <see cref="Ability.ChargePowerScale"/>（<c>ChargeStepScale = 1.2</c>）；
///   **"路径中线延长"是"非倍率维度"** —— 它是**距离**，所以水平速度按同一个倍率放大、
///   飞行时间不变（见 <see cref="ResolveFlightTime"/> 与注释），于是抛物线整体被拉长同样的比例。</item>
/// <item><b>抛物线 + 路径可视化</b>：<see cref="DashPathIndicator.SampleArcToDistance"/> 按
///   "到落点的抛物线"采样，画的线与真正飞的那条完全同源（同一组参数）。</item>
/// <item><b>收回再打一次</b>：<see cref="WeaponThrowAbility.DamageOnReturn"/> = true
///   （文档："回来时能再次对路径中的敌人造成伤害"）。</item>
/// </list>
/// </summary>
public partial class SpearThrowAbility : WeaponThrowAbility
{
	[ExportGroup("长虹贯日（抛物线抛掷 + 蓄力放大路径）")]
	/// <summary>抛物线重力（m/s²；越大弧越"拱"）。</summary>
	[Export] public float ArcGravity { get; set; } = 22f;

	/// <summary>
	/// **基准飞行时间**（秒）：水平距离 ÷ 这个时间 = 水平速度。
	/// "路径中线延长 20%/档"就是"同一个飞行时间里水平速度 ×1.2"。
	/// </summary>
	[Export] public float FlightSeconds { get; set; } = 0.8f;

	/// <summary>路径采样段数（表列 <c>PathSteps</c> 之外，这里给抛物线的细分）。</summary>
	[Export] public int ArcPathSteps { get; set; } = 32;

	[ExportGroup("运行期可读状态（自测）")]
	/// <summary>本次抛掷的水平速度（含蓄力放大）。</summary>
	public float LastHorizontalSpeed { get; private set; }

	/// <summary>本次抛掷的水平距离（含蓄力放大）。</summary>
	public float LastHorizontalDistance { get; private set; }

	/// <summary>本次抛掷的伤害倍率（= 蓄力倍率）。</summary>
	public float LastPowerScale { get; private set; } = 1f;

	/// <summary>画出路径时用了几点。</summary>
	public int LastPathPoints { get; private set; }

	private Vector3 _flatDirection;
	private float _referenceDistance;

	protected override void Launch(Vector3 flatDirection)
	{
		_flatDirection = flatDirection;
		LastPowerScale = ChargePowerScale;
		DamageScale = LastPowerScale;

		// 基准距离 = 行里的"最远抛出距离"；蓄力把**路径中线**按同一个倍率延长。
		_referenceDistance = MaxThrowDistance * LastPowerScale;
		LastHorizontalDistance = _referenceDistance;
		LastHorizontalSpeed = _referenceDistance / Mathf.Max(0.05f, FlightSeconds);
		FlightGravity = ArcGravity;

		// 抛物线：水平速度 × 飞行时间 = 距离；垂直初速让它在飞行时间末回到起点高度（对称抛物线）。
		float vertical = 0.5f * ArcGravity * FlightSeconds;
		FlightVelocity = (flatDirection * LastHorizontalSpeed) + (Vector3.Up * vertical);
		SettleHeight = HomePosition.Y - 0.05f;

		var points = DashPathIndicator.SampleArcToDistance(
			HomePosition,
			flatDirection,
			ArcGravity,
			LastHorizontalSpeed,
			FlightSeconds,
			ArcPathSteps);
		LastPathPoints = points.Count;
		DrawPath(points);

		GameLog.Write(
			"Combat",
			$"长虹贯日：{AbilityId} 蓄力 {ChargeTime:0.##}s（档 {ChargeSteps}/上限 {ChargeMaxStepsEffective}）"
			+ $" → 伤害 ×{LastPowerScale:0.###} 水平速度 {LastHorizontalSpeed:0.##}（基准 {ThrowSpeed:0.##}）"
			+ $" 路径中线 {LastHorizontalDistance:0.##}m（基准 {MaxThrowDistance:0.##}）｜抛物线重力 {ArcGravity:0.##} 飞行 {FlightSeconds:0.##}s 路径 {LastPathPoints} 点");
	}

	/// <summary>初始速度（≥ 基准）：基准速度本身也要走抛物线（表里 ThrowSpeed 是"水平速度"）。</summary>
	protected override float EffectiveThrowSpeed => LastHorizontalSpeed;

	protected override float EffectiveMaxDistance => _referenceDistance;

	/// <summary>蓄力标定：每档 +20% 的那一档位由基类给，本类只把它换算成"速度/距离"。</summary>
	protected override void OnThrowStarted()
	{
		// 蓄力放大在 Launch 里算（那时 ChargeSteps 还在 —— ChargeTime/ChargeSteps 在 Finish 之前不会被清）。
	}
}
