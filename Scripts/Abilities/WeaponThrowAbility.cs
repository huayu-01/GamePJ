using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **武器抛出 / 收回的公共地基**（P4 新增）—— 镰刀「无情」与长枪「长虹贯日」共用这一段：
/// "手上那把武器飞出去 → 对它范围内的敌人造成伤害 → 收回"。
///
/// <list type="bullet">
/// <item><b>飞出/收回两个速度</b>（<see cref="ThrowSpeed"/> / <see cref="ReturnSpeed"/>）；</item>
/// <item><b>旋转</b>（<see cref="SpinDegreesPerSecond"/>，"武器会不断旋转"）；</item>
/// <item><b>周期性伤害</b>（<see cref="DamageInterval"/> = 文档写的 0.5s，"周期性对武器内的敌人
///   造成伤害"）：只有一个计时器，每个间隔开一次命中盒；</item>
/// <item><b>收回条件</b>：再次按键（<see cref="TryPress"/>）/ 玩家靠近武器（<see cref="RecallDistance"/>）
///   / 飞太远（<see cref="MaxThrowDistance"/>）—— 文档写的是前两条，第三条是"别把武器丢出地图"的兜底。</item>
/// </list>
///
/// <para><b>"武器飞出去这期间武技1无法使用"</b>：本类把"飞出中"这个状态通过
/// <see cref="InFlightChanged"/> 广播出去，<c>ComboGate</c>（同一把武器的武技1 节点）订阅它 ——
/// 于是"能不能用武技1"由**武器在不在手上**决定，而不是各写一份开关。</para>
///
/// <para>视觉由 <see cref="WeaponVisual"/> 承担（场景里那个可选的 <c>WeaponVisual</c> 子节点）：
/// 抛出期间它被摆到武器的位置上、按 <see cref="SpinDegreesPerSecond"/> 自转；收回后放回手上。
/// 没有这个子节点时逻辑照常（只是没有模型）。</para>
/// </summary>
public abstract partial class WeaponThrowAbility : Ability
{
	[ExportGroup("抛出 / 收回")]
	/// <summary>抛出速度（m/s）。</summary>
	[Export] public float ThrowSpeed { get; set; } = 18f;

	/// <summary>收回速度（m/s）。</summary>
	[Export] public float ReturnSpeed { get; set; } = 26f;

	/// <summary>**最远抛出距离**（米；到点自动收回，兜底防止武器丢出地图）。</summary>
	[Export] public float MaxThrowDistance { get; set; } = 12f;

	/// <summary>**再次按键就收回**（文档：「再次使用…时收回武器」）。</summary>
	[Export] public bool RecallOnSecondPress { get; set; } = true;

	/// <summary>**靠近就收回**的距离（米；≤0 = 不靠靠近收回）。文档：「或靠近武器时收回武器」。</summary>
	[Export] public float RecallDistance { get; set; } = 1.8f;

	[ExportGroup("旋转与周期性伤害")]
	/// <summary>自转速度（度/秒）。0 = 不转。</summary>
	[Export] public float SpinDegreesPerSecond { get; set; } = 720f;

	/// <summary>**周期性伤害的间隔**（秒）。文档：镰刀 0.5。</summary>
	[Export] public float DamageInterval { get; set; } = 0.5f;

	/// <summary>每次判定的持续（秒）。</summary>
	[Export] public float DamageActiveTime { get; set; } = 0.12f;

	/// <summary>收回途中是否**再一次**对路径中的敌人造成伤害（文档：长枪「回来时能再次对路径中的敌人造成伤害」）。</summary>
	[Export] public bool DamageOnReturn { get; set; } = true;

	[ExportGroup("可视化路径（可选）")]
	/// <summary>抛出时画不画路径线。</summary>
	[Export] public bool ShowPath { get; set; }

	/// <summary>线的颜色。</summary>
	[Export] public Color PathColor { get; set; } = new(0.9f, 0.85f, 0.5f, 0.8f);

	/// <summary>采样段数。</summary>
	[Export] public int PathSteps { get; set; } = 24;

	[ExportGroup("运行期可读状态（自测 / UI / 武技1 的门）")]
	/// <summary>武器现在在飞吗（"飞出去这期间武技1无法使用"读的就是它）。</summary>
	public bool InFlight { get; private set; }

	/// <summary>武器现在的世界坐标（自测 / 表现）。</summary>
	public Vector3 WeaponPosition { get; private set; }

	/// <summary>累计造成了几次周期性伤害（自测取证）。</summary>
	public int DamageTickCount { get; private set; }

	/// <summary>累计收回几次。</summary>
	public int RecallCount { get; private set; }

	/// <summary>**在飞 / 回手** 的状态变化（武技1 的门订阅它）。</summary>
	public event System.Action<bool>? InFlightChanged;

	protected AttackHitbox? Hitbox { get; private set; }

	/// <summary>场景里那个可选的武器模型（抛出去时摆到武器位置上自转）。</summary>
	protected Node3D? WeaponVisual { get; private set; }

	private DashPathIndicator? _path;
	private float _spin;
	private float _intervalTimer;
	private float _activeTimer;
	private bool _damageWindowOpen;
	private bool _wasPressed;

	/// <summary>抛出时的起点（"手上"的位置：胸口高度 + 前方 0.6m）—— 子类画路径/算落地高度要用。</summary>
	protected Vector3 HomePosition { get; private set; }

	protected override void OnReady()
	{
		Hitbox = GetNodeOrNull<AttackHitbox>("Attack");
		WeaponVisual = GetNodeOrNull<Node3D>("WeaponVisual");
		_path = GetNodeOrNull<DashPathIndicator>("Path");
		if (_path != null)
		{
			_path.Configure(PathColor, 0.06f, 0.06f);
		}
	}

	/// <summary>抛出方向（默认朝准星；子类可改）。</summary>
	protected virtual Vector3 ResolveThrowDirection() => ResolveAimDirection();

	/// <summary>抛出时的垂直初速（≥0；子类给抛物线用）。</summary>
	protected virtual float LaunchUpwardSpeed => 0f;

	/// <summary>本次抛出的水平距离上限（子类可以按蓄力改）。</summary>
	protected virtual float EffectiveMaxDistance => MaxThrowDistance;

	/// <summary>本次抛出的水平速度（子类可以按蓄力改）。</summary>
	protected virtual float EffectiveThrowSpeed => ThrowSpeed;

	/// <summary>子类在开始抛出时做点自己的事（记录档位等）。</summary>
	protected virtual void OnThrowStarted()
	{
	}

	/// <summary>子类在武器落地/停下时做点自己的事（长枪的抛物线落地）。</summary>
	protected virtual void OnThrowSettled(Vector3 position)
	{
	}

	protected override bool CanActivate(Unit? target) => OwnerUnit != null;

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		_wasPressed = false;
		_intervalTimer = 0f;
		_activeTimer = 0f;
		_damageWindowOpen = false;
		_spin = 0f;
		DamageTickCount = 0;

		Vector3 direction = ResolveThrowDirection();
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.0001f)
		{
			direction = -OwnerUnit.GlobalTransform.Basis.Z;
			direction.Y = 0f;
		}

		direction = direction.Normalized();
		HomePosition = OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f) + (direction * 0.6f);
		WeaponPosition = HomePosition;
		SetInFlight(true);
		OnThrowStarted();

		Launch(direction);

		GameLog.Write(
			"Combat",
			$"抛掷：{AbilityId} 朝 {direction} 出手（水平速 {EffectiveThrowSpeed:0.##} 垂直初速 {LaunchUpwardSpeed:0.##}，"
			+ $"最远 {EffectiveMaxDistance:0.##}m，每 {DamageInterval:0.##}s 一次周期伤害）");
	}

	/// <summary>子类实现这一步：设置 <see cref="FlightVelocity"/> 与 <see cref="FlightGravity"/>。</summary>
	protected abstract void Launch(Vector3 flatDirection);

	/// <summary>当前飞行速度（子类在 <see cref="Launch"/> 里设）。</summary>
	protected Vector3 FlightVelocity { get; set; }

	/// <summary>飞行重力（0 = 直线）。</summary>
	protected float FlightGravity { get; set; }

	/// <summary>落到这个高度以下就算"落地"（0 = 不判落地）。</summary>
	protected float SettleHeight { get; set; }

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (InFlight)
		{
			TickFlight(delta);
			return;
		}

		TickReturning(delta);
	}

	/// <summary>飞出阶段：直线/抛物线 + 旋转 + 周期性伤害 + 收回判定。</summary>
	private void TickFlight(float delta)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		if (FlightGravity != 0f)
		{
			FlightVelocity = FlightVelocity with { Y = FlightVelocity.Y - (FlightGravity * delta) };
		}

		WeaponPosition += FlightVelocity * delta;
		_spin += SpinDegreesPerSecond * delta;

		// 落地（抛物线打完）→ 停下旋转，等收回。
		if (SettleHeight > 0f && WeaponPosition.Y <= SettleHeight)
		{
			WeaponPosition = WeaponPosition with { Y = SettleHeight };
			FlightVelocity = Vector3.Zero;
			OnThrowSettled(WeaponPosition);
		}

		PushVisual();

		// 周期性伤害（文档：每隔 DamageInterval 秒对武器内的敌人造成伤害）。
		_intervalTimer += delta;
		bool window = _intervalTimer >= DamageInterval;
		if (window && !_damageWindowOpen)
		{
			OpenDamageWindow();
			_intervalTimer = 0f;
		}

		if (_damageWindowOpen)
		{
			_activeTimer -= delta;
			if (_activeTimer <= 0f)
			{
				_damageWindowOpen = false;
				Hitbox?.Deactivate();
			}
		}

		float travelled = HomePosition.DistanceTo(WeaponPosition);
		float nearOwner = OwnerUnit.GlobalPosition.DistanceTo(WeaponPosition);
		if ((RecallOnSecondPress && _wasPressed)
			|| (RecallDistance > 0f && nearOwner <= RecallDistance)
			|| (EffectiveMaxDistance > 0f && travelled >= EffectiveMaxDistance))
		{
			BeginReturn();
		}
	}

	/// <summary>收回阶段：朝玩家飞 + （可选）路径伤害 + 到手上就收招。</summary>
	private void TickReturning(float delta)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		Vector3 hand = HandPosition();
		Vector3 toHand = hand - WeaponPosition;
		float step = ReturnSpeed * delta;
		if (toHand.Length() <= step)
		{
			WeaponPosition = hand;
			PushVisual();
			FinishReturn();
			return;
		}

		Vector3 direction = toHand.Normalized();
		WeaponPosition += direction * step;
		_spin += SpinDegreesPerSecond * delta;
		PushVisual();

		if (DamageOnReturn)
		{
			// 回来途中也开一次伤害（文档：长枪「回来时能再次对路径中的敌人造成伤害」）。
			_intervalTimer += delta;
			if (_intervalTimer >= DamageInterval)
			{
				_intervalTimer = 0f;
				OpenDamageWindow();
			}

			if (_damageWindowOpen)
			{
				_activeTimer -= delta;
				if (_activeTimer <= 0f)
				{
					_damageWindowOpen = false;
					Hitbox?.Deactivate();
				}
			}
		}
	}

	private void OpenDamageWindow()
	{
		if (Hitbox == null || OwnerUnit == null)
		{
			return;
		}

		// 命中盒跟着武器走（它是本能力的子节点，能力节点本身不动 → 每帧把命中盒搬到武器位置）。
		Hitbox.GlobalPosition = WeaponPosition;
		Hitbox.Activate(OwnerUnit, new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = Hitbox.Damage * DamageScale,
			CritChance = Hitbox.CritChance,
			CritMultiplier = Hitbox.CritMultiplier,
			Knockback = Hitbox.Knockback,
			Policy = HitPolicy.OncePerTarget,
			HitInterval = Hitbox.HitInterval,
			Tags = Hitbox.AttackTags,
			AbilityKind = AbilityId,
		});

		_damageWindowOpen = true;
		_activeTimer = Mathf.Max(0.02f, DamageActiveTime);
		DamageTickCount++;
	}

	/// <summary>伤害倍率（子类按蓄力/档位改）。</summary>
	protected float DamageScale { get; set; } = 1f;

	/// <summary>开始收回（再次按键 / 靠近 / 到最远距离）。</summary>
	protected void BeginReturn()
	{
		if (!InFlight)
		{
			return;
		}

		SetInFlight(false);
		FlightVelocity = Vector3.Zero;
		_intervalTimer = 0f;
		if (Hitbox != null)
		{
			Hitbox.Deactivate();
		}

		_damageWindowOpen = false;
		GameLog.Write("Combat", $"抛掷：{AbilityId} 开始收回（累计周期伤害 {DamageTickCount} 次）");
	}

	private void FinishReturn()
	{
		RecallCount++;
		Hitbox?.Deactivate();
		_damageWindowOpen = false;
		ReturnVisualToHand();
		GameLog.Write("Combat", $"抛掷：{AbilityId} 武器已回到手上（第 {RecallCount} 次收回，累计周期伤害 {DamageTickCount} 次）");
		Finish();
	}

	/// <summary>玩家的"手"的位置（胸口高度 + 前方 0.35m）。</summary>
	protected Vector3 HandPosition()
	{
		if (OwnerUnit == null)
		{
			return HomePosition;
		}

		Vector3 forward = -OwnerUnit.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		return OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f) + (forward.Normalized() * 0.35f);
	}

	private void PushVisual()
	{
		if (WeaponVisual == null || !IsInstanceValid(WeaponVisual))
		{
			return;
		}

		WeaponVisual.Visible = true;
		WeaponVisual.GlobalPosition = WeaponPosition;
		WeaponVisual.RotationDegrees = new Vector3(0f, 0f, _spin);
	}

	private void ReturnVisualToHand()
	{
		if (WeaponVisual == null || !IsInstanceValid(WeaponVisual))
		{
			return;
		}

		WeaponVisual.Visible = false;
		WeaponVisual.RotationDegrees = Vector3.Zero;
	}

	private void SetInFlight(bool inFlight)
	{
		if (InFlight == inFlight)
		{
			return;
		}

		InFlight = inFlight;
		InFlightChanged?.Invoke(inFlight);
	}

	/// <summary>
	/// **再次按键**（文档：「再次使用…时收回武器」）。
	/// 在飞 → 记一个"这一帧按过了"（下一个 Tick 里收回）；不在飞 → 走基类的默认（新的一次施法）。
	/// </summary>
	public override bool TryPress(Unit? target = null)
	{
		if (IsBusy && InFlight)
		{
			_wasPressed = true;
			return true;
		}

		return base.TryPress(target);
	}

	/// <summary>画出抛掷路径（子类在 <see cref="Launch"/> 里按自己的弹道调用）。</summary>
	protected void DrawPath(IReadOnlyList<Vector3> points)
	{
		if (!ShowPath || _path == null)
		{
			return;
		}

		_path.Show(points);
	}

	protected override void OnFinishOrCancel()
	{
		Hitbox?.Deactivate();
		_damageWindowOpen = false;
		SetInFlight(false);
		ReturnVisualToHand();
		_path?.HidePath();
	}

	public override void _ExitTree()
	{
		Hitbox?.Deactivate();
		SetInFlight(false);
		base._ExitTree();
	}
}
