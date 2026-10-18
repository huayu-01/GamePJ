using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>无情：向角色前方飞到射程后悬停旋转，持续周期命中，再次使用或靠近收回。</summary>
public partial class ScytheThrowAbility : WeaponThrowAbility
{
	[ExportGroup("镰刀（前方直线抛出）")]
	/// <summary>抛出的垂直初速（0 = 纯水平，"扔向前方"）。</summary>
	[Export] public float UpwardSpeed { get; set; }
	/// <summary>周期索敌间隔（秒）；默认0不启用，保持原飞镰行为。</summary>
	[Export] public float SeekIntervalSeconds { get; set; }
	[Export] public float SeekTargetHeight { get; set; } = 0.9f;
	public int SeekTickCount { get; private set; }
	public int RedirectCount { get; private set; }
	public string LastSeekTarget { get; private set; } = string.Empty;
	public float LastSeekSeconds { get; private set; }
	private float _seekTimer, _flightSeconds;
	public void ConfigureSeeking(float interval, float targetHeight)
	{
		SeekIntervalSeconds = Mathf.Max(0f, interval);
		SeekTargetHeight = targetHeight;
		_seekTimer = 0f;
	}
	protected override void OnLoadoutReset()
	{
		SeekIntervalSeconds = 0f;
		SeekTargetHeight = 0.9f;
		_seekTimer = _flightSeconds = LastSeekSeconds = 0f;
		SeekTickCount = RedirectCount = 0;
		LastSeekTarget = string.Empty;
	}
	protected override void OnThrowStarted()
	{
		_seekTimer = _flightSeconds = LastSeekSeconds = 0f;
		SeekTickCount = RedirectCount = 0;
		LastSeekTarget = string.Empty;
	}
	protected override void Tick(float delta)
	{
		base.Tick(delta);
		if (!IsActive || !InFlight || IsReturning || SeekIntervalSeconds <= 0f || OwnerUnit == null) return;
		_flightSeconds += delta;
		_seekTimer += delta;
		if (_seekTimer + 0.00001f < SeekIntervalSeconds) return;
		_seekTimer = Mathf.Max(0f, _seekTimer - SeekIntervalSeconds);
		SeekTickCount++;
		LastSeekSeconds = _flightSeconds;
		// 原文是离玩家最近，不能从武器当前位置挑最近的敌人。
		var target = Targeting.FindNearestHostile(OwnerUnit, OwnerUnit);
		LastSeekTarget = target?.Name.ToString() ?? string.Empty;
		if (target != null && RedirectFlightTowards(target.GlobalPosition + Vector3.Up * SeekTargetHeight)) RedirectCount++;
	}

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
