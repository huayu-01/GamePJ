using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>投掷共用状态机：动画事件出手、飞出、真实地形／射程停留、回手。视觉独立只读。</summary>
public abstract partial class WeaponThrowAbility : Ability
{
	[ExportGroup("抛出 / 收回")]
	/// <summary>抛出速度（m/s）。</summary>
	[Export] public float ThrowSpeed { get; set; } = 18f;

	/// <summary>收回速度（m/s）。</summary>
	[Export] public float ReturnSpeed { get; set; } = 26f;

	/// <summary>**最远抛出距离**（米；到点停留）。</summary>
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
	/// <summary>飞出与回程各持续判定一次路径；关闭时使用周期伤害。</summary>
	[Export] public bool ContinuousPathDamage { get; set; }
	[Export] public bool AutoRecallOnSettle { get; set; }
	[Export] public float ThrowEventTimeout { get; set; } = 2f;

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
	/// <summary>InFlight表示尚未回手，包含停留与回程，供武技1锁门使用。</summary>
	public bool IsReturning { get; private set; }
	public bool HasSettled { get; private set; }
	public Vector3 WeaponVelocity => FlightVelocity;
	public Vector3 LastFlightDirection { get; private set; } = Vector3.Forward;
	protected override bool OccupiesAction => !InFlight;

	/// <summary>武器现在的世界坐标（自测 / 表现）。</summary>
	public Vector3 WeaponPosition { get; private set; }

	/// <summary>累计造成了几次周期性伤害（自测取证）。</summary>
	public int DamageTickCount { get; private set; }

	/// <summary>累计收回几次。</summary>
	public int RecallCount { get; private set; }

	/// <summary>**在飞 / 回手** 的状态变化（武技1 的门订阅它）。</summary>
	public event System.Action<bool>? InFlightChanged;

	protected AttackHitbox? Hitbox { get; private set; }


	private DashPathIndicator? _path;
	private float _intervalTimer;
	private float _activeTimer;
	private bool _damageWindowOpen;
	private bool _waitingForThrowEvent;
	private bool _waitingForCharge;
	private bool _leftRecallRange;

	/// <summary>抛出时的起点（"手上"的位置：胸口高度 + 前方 0.6m）—— 子类画路径/算落地高度要用。</summary>
	protected Vector3 HomePosition { get; private set; }

	protected override void OnReady()
	{
		Hitbox = GetNodeOrNull<AttackHitbox>("Attack");
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

	protected override bool CanActivate(Unit? target) => OwnerUnit != null && !string.IsNullOrEmpty(AnimName) && !string.IsNullOrEmpty(AnimEvent);

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		_waitingForThrowEvent = !HoldToCharge;
		_intervalTimer = 0f;
		_activeTimer = 0f;
		_damageWindowOpen = false;
		DamageTickCount = 0;
		_leftRecallRange = false;
		_waitingForCharge = HoldToCharge;
		IsReturning = false;
		HasSettled = false;
		PlaySkillAnimation();
	}

	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		if (!_waitingForCharge) return;
		_waitingForCharge = false;
		_waitingForThrowEvent = true;
		StateTime = 0f;
		PlaySkillAnimation();
	}
	protected override void OnAnimEvent(string name)
	{
		if (!_waitingForThrowEvent || _waitingForCharge || InFlight) return;
		_waitingForThrowEvent = false;
		BeginThrow();
	}
	public override void OnInputHeld(float delta)
	{
		if (_waitingForCharge) base.OnInputHeld(delta);
	}
	protected virtual void PreviewPath() { }

	protected override void OnChargeAborted() => Cancel();

	private void BeginThrow()
	{
		if (OwnerUnit == null) { Cancel(); return; }

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
		// 只在自身仍占动作时交还动画；回手收尾不能停止其他技能的动画。
		StopSkillAnimation();
		SetInFlight(true);
		OnThrowStarted();

		Launch(direction);
		LastFlightDirection = FlightVelocity.Normalized();
		if (ContinuousPathDamage) OpenDamageWindow();

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

	protected override void Tick(float delta)
	{
		if (!IsActive) return;
		if (_waitingForCharge) { PreviewPath(); return; }
		if (_waitingForThrowEvent)
		{
			if (StateTime > ThrowEventTimeout) Cancel();
			return;
		}
		if (!InFlight || OwnerUnit == null) return;
		if (IsReturning) TickReturning(delta);
		else TickFlight(delta);
	}

	private void TickFlight(float delta)
	{
		Vector3 previous = WeaponPosition;
		if (!HasSettled)
		{
			Vector3 next = previous + FlightVelocity * delta + Vector3.Down * (0.5f * FlightGravity * delta * delta);
			FlightVelocity += Vector3.Down * (FlightGravity * delta);
			var query = PhysicsRayQueryParameters3D.Create(previous, next, CombatLayers.World);
			var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0)
			{
				WeaponPosition = hit["position"].AsVector3();
				Settle();
			}
			else
			{
				Vector3 flat = next - HomePosition;
				flat.Y = 0;
				if (EffectiveMaxDistance > 0 && flat.Length() >= EffectiveMaxDistance)
				{
					Vector3 limited = HomePosition + flat.Normalized() * EffectiveMaxDistance;
					WeaponPosition = new Vector3(limited.X, next.Y, limited.Z);
					// 镰刀在前方悬停；抛物线武器到水平上限后仍须落到真实World。
					if (FlightGravity <= 0) Settle();
					else FlightVelocity = new Vector3(0, FlightVelocity.Y, 0);
				}
				else WeaponPosition = next;
			}
			if (FlightVelocity.LengthSquared() > 0.001f) LastFlightDirection = FlightVelocity.Normalized();
		}
		TickDamage(delta, previous);
		if (HasSettled && ContinuousPathDamage) Hitbox?.Deactivate();
		float nearOwner = OwnerUnit!.GlobalPosition.DistanceTo(WeaponPosition);
		if (nearOwner > RecallDistance) _leftRecallRange = true;
		if ((AutoRecallOnSettle && HasSettled)
			|| (RecallDistance > 0 && _leftRecallRange && nearOwner <= RecallDistance)) BeginReturn();
	}

	private void Settle()
	{
		if (HasSettled) return;
		HasSettled = true;
		FlightVelocity = Vector3.Zero;
		OnThrowSettled(WeaponPosition);
	}

	private void TickDamage(float delta, Vector3 previous)
	{
		if (Hitbox == null) return;
		Hitbox.GlobalPosition = WeaponPosition;
		if (ContinuousPathDamage)
		{
			if (!HasSettled || IsReturning || previous != WeaponPosition) Hitbox.SweepBox(previous, WeaponPosition);
			return;
		}
		_intervalTimer += delta;
		if (_intervalTimer >= Mathf.Max(0.01f, DamageInterval))
		{
			_intervalTimer = 0;
			OpenDamageWindow();
		}
		if (_damageWindowOpen)
		{
			Hitbox.SweepBox(previous, WeaponPosition);
			_activeTimer -= delta;
			if (_activeTimer <= 0) { Hitbox.Deactivate(); _damageWindowOpen = false; }
		}
	}

	private void TickReturning(float delta)
	{
		if (OwnerUnit == null) { Cancel(); return; }
		Vector3 previous = WeaponPosition;
		Vector3 hand = HandPosition();
		Vector3 direction = hand - previous;
		WeaponPosition = previous.MoveToward(hand, ReturnSpeed * delta);
		if (direction.LengthSquared() > 0.001f) LastFlightDirection = direction.Normalized();
		if (DamageOnReturn) TickDamage(delta, previous);
		if (WeaponPosition.IsEqualApprox(hand)) FinishReturn();
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

	/// <summary>开始回手；锁门直到实际到手才解除。</summary>
	protected void BeginReturn()
	{
		if (!InFlight || IsReturning)
		{
			return;
		}

		IsReturning = true;
		FlightVelocity = Vector3.Zero;
		_intervalTimer = 0f;
		if (Hitbox != null)
		{
			Hitbox.Deactivate();
		}

		_damageWindowOpen = false;
		if (DamageOnReturn && ContinuousPathDamage) OpenDamageWindow();
		GameLog.Write("Combat", $"抛掷：{AbilityId} 开始收回（累计周期伤害 {DamageTickCount} 次）");
	}

	private void FinishReturn()
	{
		RecallCount++;
		Hitbox?.Deactivate();
		_damageWindowOpen = false;
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
	/// 武器在外时尊重手动召回配置；已回手时走基类启动新施法。
	/// </summary>
	public override bool TryPress(Unit? target = null)
	{
		if (IsActive && InFlight)
		{
			if (!Enabled || IsReturning || !RecallOnSecondPress) return false;
			BeginReturn();
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
		_waitingForCharge = false;
		_waitingForThrowEvent = false;
		Hitbox?.Deactivate();
		_damageWindowOpen = false;
		SetInFlight(false);
		IsReturning = false;
		HasSettled = false;
		_path?.HidePath();
	}

	public override void _ExitTree()
	{
		_waitingForCharge = false;
		Hitbox?.Deactivate();
		SetInFlight(false);
		base._ExitTree();
	}
}
