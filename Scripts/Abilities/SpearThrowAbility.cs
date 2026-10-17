using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>长虹贯日：准星选择地面落点，按蓄力射程截断；预览与飞行共用弹道求解，真实World碰撞停留。</summary>
public partial class SpearThrowAbility : WeaponThrowAbility
{
	[ExportGroup("长虹贯日（抛物线抛掷 + 蓄力放大路径）")]
	/// <summary>抛物线重力（m/s²；越大弧越"拱"）。</summary>
	[Export] public float ArcGravity { get; set; } = 22f;

	/// <summary>
	/// **基准飞行时间**（秒）：水平距离 ÷ 这个时间 = 水平速度。
	/// 满射程速度与距离同倍率放大；选择较近落点时缩短飞行时间。
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

	[Export] public float MaxAimDistance { get; set; } = 120f;
	public Vector3 TargetPoint { get; private set; }
	public float LastMaxDistance { get; private set; }
	private float _referenceDistance;

	private Vector3 PickLandingPoint(Vector3 start, Vector3 flat)
	{
		float range = MaxThrowDistance * ChargePowerScale;
		Vector3 point = AimUtil.TryGetCrosshairPoint(GetViewport(), OwnerUnit!, MaxAimDistance, CombatLayers.World, out var aimed)
			? aimed : start + flat * range;
		Vector3 planar = point - start;
		planar.Y = 0;
		if (planar.Length() > range) planar = planar.Normalized() * range;
		Vector3 target = start + planar;
		float top = Mathf.Max(start.Y, point.Y) + 1f;
		var query = PhysicsRayQueryParameters3D.Create(new Vector3(target.X, top, target.Z), new Vector3(target.X, top - MaxAimDistance, target.Z), CombatLayers.World);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector3() : target + Vector3.Down;
	}

	private Vector3 Solve(Vector3 start, Vector3 target, out float seconds)
	{
		Vector3 planar = target - start;
		planar.Y = 0;
		float speed = MaxThrowDistance / Mathf.Max(0.05f, FlightSeconds) * ChargePowerScale;
		seconds = Mathf.Max(0.05f, planar.Length() / Mathf.Max(0.01f, speed));
		return planar / seconds + Vector3.Up * ((target.Y - start.Y) / seconds + 0.5f * ArcGravity * seconds);
	}

	private void Path(Vector3 start, Vector3 velocity, float seconds)
	{
		var points = new System.Collections.Generic.List<Vector3> { start };
		for (int i = 1; i <= Mathf.Max(2, ArcPathSteps); i++)
		{
			float time = seconds * i / Mathf.Max(2, ArcPathSteps);
			Vector3 next = start + velocity * time + Vector3.Down * (0.5f * ArcGravity * time * time);
			var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(points[^1], next, CombatLayers.World));
			if (hit.Count > 0) { points.Add(hit["position"].AsVector3()); break; }
			points.Add(next);
		}
		LastPathPoints = points.Count;
		DrawPath(points);
	}

	protected override void PreviewPath()
	{
		if (OwnerUnit == null) return;
		Vector3 flat = ResolveThrowDirection(); flat.Y = 0;
		flat = flat.LengthSquared() > 0.001f ? flat.Normalized() : Vector3.Forward;
		Vector3 start = OwnerUnit.GlobalPosition + Vector3.Up * 0.9f + flat * 0.6f;
		Vector3 target = PickLandingPoint(start, flat);
		Path(start, Solve(start, target, out float seconds), seconds);
	}

	protected override void Launch(Vector3 flatDirection)
	{
		LastPowerScale = ChargePowerScale;
		DamageScale = LastPowerScale;
		_referenceDistance = MaxThrowDistance * LastPowerScale;
		LastMaxDistance = _referenceDistance;
		TargetPoint = PickLandingPoint(HomePosition, flatDirection);
		FlightVelocity = Solve(HomePosition, TargetPoint, out float seconds);
		LastHorizontalDistance = new Vector2(TargetPoint.X - HomePosition.X, TargetPoint.Z - HomePosition.Z).Length();
		LastHorizontalSpeed = new Vector2(FlightVelocity.X, FlightVelocity.Z).Length();
		FlightGravity = ArcGravity;
		Path(HomePosition, FlightVelocity, seconds);
		GameLog.Write("Combat", $"长虹贯日：{ChargeSteps}档 加算倍率{LastPowerScale:0.###}，准星落点{TargetPoint}，水平距离{LastHorizontalDistance:0.###}/{LastMaxDistance:0.###}m，水平速度{LastHorizontalSpeed:0.###}");
	}

	protected override float EffectiveThrowSpeed => LastHorizontalSpeed;
	protected override float EffectiveMaxDistance => _referenceDistance;
}
