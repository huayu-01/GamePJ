using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 单位移动能力组件（执行文档 §4.4）。挂在 Unit 节点下，只负责物理与朝向，不做任何决策：
/// 玩家控制器、敌人 Behavior 都通过 <see cref="SetIntent"/> 表达“想怎么走”。
/// 具备跳跃能力不代表 AI 会主动使用它 —— 是否调用 <see cref="TryJump"/> 由行为决定。
/// </summary>
public partial class CharacterMovement : Node
{
	[Export] public float GravityScale { get; set; } = 1.8f;
	[Export] public float MaxFallSpeed { get; set; } = 60f;
	[Export] public float GroundAcceleration { get; set; } = 60f;
	[Export] public float AirAcceleration { get; set; } = 15f;
	[Export] public float JumpVelocity { get; set; } = 6.5f;
	/// <summary>额外空中跳的高度比例，玩家从角色表覆盖；其它单位保留原跳高。</summary>
	[Export] public float AirJumpHeightRatio { get; set; } = 1f;
	public float StandardJumpHeight => JumpVelocity * JumpVelocity / (2 * Mathf.Max(0.001f, _gravity * GravityScale));
	public float EffectiveAirJumpVelocity => EffectiveJumpVelocity * Mathf.Sqrt(Mathf.Max(0, AirJumpHeightRatio));

	/// <summary>落地后可用的额外空中跳跃次数（0 = 不能二段跳）。</summary>
	[Export] public int ExtraAirJumps { get; set; }

	/// <summary>
	/// 当前**还剩几次空中跳跃**（落地时会被重置成 <see cref="ExtraAirJumps"/>）。
	/// 滞空（角色 A 技能 2「跃升」）要判"当有跳跃次数时会先执行跳跃"，读的就是它。
	/// </summary>
	public int AirJumpsLeft => _airJumpsLeft;

	/// <summary>
	/// **额外跳跃次数的接入点**（被动「灵动」/ 异化「践踏死亡」这类"在空中额外跳跃"的效果走这里）。
	///
	/// <para>用户的设计文档（角色 A 被动「灵动」）：「角色能在空中额外跳跃一次（正常情况下所有角色都
	/// 只会有 1 次额外的获取方法，**上限最多 3 次**）」—— 所以上限是参数、不是常量：
	/// 上限值由调用方（表驱动的那条被动/异化）传进来。</para>
	///
	/// <para>⚠️ 它是**幂等的写入**：调用方每帧算出的目标值一样时这里什么都不做（不会每次重置空中次数），
	/// 落地时仍然由 <see cref="_PhysicsProcess"/> 那行按 <see cref="ExtraAirJumps"/> 回满。</para>
	/// </summary>
	public void SetExtraAirJumps(int value, int cap)
	{
		int wanted = Mathf.Clamp(value, 0, Mathf.Max(0, cap));
		if (wanted == ExtraAirJumps)
		{
			return;
		}

		ExtraAirJumps = wanted;
		_airJumpsLeft = Mathf.Min(_airJumpsLeft, wanted);
	}

	/// <summary>击退等外力的衰减速度（1/秒）。</summary>
	[Export] public float ImpulseDamping { get; set; } = 6f;

	[Export] public bool FaceMovementDirection { get; set; } = true;
	[Export] public float TurnSharpness { get; set; } = 14f;

	private CharacterBody3D _body = null!;
	private Unit? _unit;
	private float _gravity;

	private Vector3 _intentDirection;
	private float _intentSpeed;
	private Vector3 _inputVelocity;
	private Vector3 _externalVelocity;
	private Vector3 _facing;
	private bool _hasFacing;
	private int _airJumpsLeft;
	private Vector3 _dashDirection;
	private float _dashSpeed;
	private object? _velocityOverrideSource;
	private Vector3 _overrideVelocity;
	public bool HasVelocityOverride => _velocityOverrideSource != null;

	/// <summary>动作持有完整三维速度；结束时只能释放自己持有的覆盖。</summary>
	public bool BeginVelocityOverride(object source, Vector3 velocity)
	{
		if (_velocityOverrideSource != null && !ReferenceEquals(_velocityOverrideSource, source)) return false;
		_velocityOverrideSource = source;
		_overrideVelocity = velocity;
		return true;
	}
	public void EndVelocityOverride(object source)
	{
		if (!ReferenceEquals(_velocityOverrideSource, source)) return;
		_velocityOverrideSource = null;
		_overrideVelocity = Vector3.Zero;
		_inputVelocity = Vector3.Zero;
		if (IsInstanceValid(_body)) _body.Velocity = Vector3.Zero;
	}

	public bool IsGrounded => _body.IsOnFloor();
	public Vector3 PlanarVelocity { get { Vector3 v = _body.Velocity; v.Y = 0f; return v; } }

	/// <summary>
	/// **本段滞空已经"净下落"了多少米**（P4 新增，只加不删）：从这次离地开始记录**最高点**，
	/// 之后每帧算 <c>最高点.Y - 当前.Y</c>（夹在 ≥0）。落到地面时最后一次 <see cref="_PhysicsProcess"/>
	/// 里仍然是"落地前那一帧"的值，所以"落地时的下落高度"读得到。
	///
	/// <para>该量保留给实际下落距离的观察者；共通落已按用户裁定改读起手脚下到World地面高度。</para>
	///
	/// <para>⚠️ 默认（没有任何技能读它）行为逐位不变：它只在一处多算了两个浮点量，
	/// 不参与 <c>MoveAndSlide</c> 的任何输入。</para>
	/// </summary>
	public float FallHeight { get; private set; }

	private float _apexY;
	private bool _apexTracking;

	public bool IsAirborne => _body != null && !_body.IsOnFloor();

	/// <summary>当前的移动意图方向（未归一化前的原样），位移技能用它决定冲刺方向。</summary>
	public Vector3 IntentDirection => _intentDirection;

	public bool IsDashing => _dashSpeed > 0f;

	private readonly HashSet<object> _facingLocks = new(ReferenceEqualityComparer.Instance);
	/// <summary>技能按自身所有权锁定转身；例如后撤期间转镜头也不改变起手朝向。</summary>
	public bool IsFacingLocked => _facingLocks.Count > 0;
	public void LockFacing(object source) => _facingLocks.Add(source);
	public void UnlockFacing(object source) => _facingLocks.Remove(source);

	/// <summary>位移技能用：在持续时间内以固定速度沿方向移动，忽略常规加速度。</summary>
	public void BeginDash(Vector3 direction, float speed)
	{
		_dashDirection = direction.LengthSquared() < 0.0001f ? -_body.GlobalTransform.Basis.Z : direction.Normalized();
		_dashSpeed = speed;
	}

	public void EndDash()
	{
		if (_dashSpeed <= 0) return;
		_dashSpeed = 0f;
		// 强制冲刺结束后归还普通移动，不能把上一帧冲刺速度当作输入惯性继续滑行。
		_inputVelocity = Vector3.Zero;
	}

	/// <summary>位移/扑击技能用：直接设置垂直速度。</summary>
	public void SetVerticalVelocity(float velocity)
	{
		Vector3 v = _body.Velocity;
		v.Y = velocity;
		_body.Velocity = v;
	}

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_unit = _body as Unit;
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		if (_unit != null)
		{
			_unit.KnockbackReceived += OnKnockback;

			// ⚠️ **必须 defer 一帧**：归属组（`player_units` / `enemy_units`）是在 `Unit._Ready` 里
			// `AddToGroup` 的，而**子节点的 `_Ready` 早于父节点** —— 在 `_Ready` 里问 `IsInGroup`
			// 拿到的永远是 false（首轮实测：12 个单位全是 `player_units 组=False`，跳跃配置一次都没生效）。
			// defer 的调用排在当前帧所有 `_Ready` 之后，这时组已经登记好了。
			CallDeferred(nameof(ApplyJumpConfigDeferred));
		}
	}

	/// <summary>
	/// 跳跃参数来自**角色表**（`Config/Tables/Characters/chr_*.tres` 的「跳跃」两组）——
	/// 玩家预制体（`Player.tscn` 的 <c>Movement</c>）只保留 <c>JumpVelocity</c> / <c>GravityScale</c> 作为**回退值**。
	///
	/// ⚠️ **只有玩家读表**：表里那两列写的是"玩家手感"。敌人 / 召唤物的跳跃（如 `JumpingMelee.tscn`
	/// 的 <c>JumpVelocity = 9</c>）留在各自场景上，否则调玩家的跳跃会连带改掉 9 个敌种的扑击高度。
	/// 组判定按 `player_units`（`Unit._Ready` 里登记）。
	///
	/// ⚠️ 与 <see cref="UnitConfig.ApplyTo"/> 那条路径（实例化 → ApplyTo → AddChild）不同：
	/// 这里就地解析（按玩家 <c>RunSetup.Character</c> 选行、读不到行就什么都不做）。同理，
	/// 玩家在中途换角色不会重新算 —— <c>RunController.ApplyBalance</c> 换角色时会重建这一份数值，
	/// 而那属于它的文件边界（本轮不碰）。
	///
	/// <b>2026-10-12 用户拍板（需求 A2）：去掉倍率运算，改成"直接改固定值"</b> ——
	/// 原来这里是 <c>JumpHeightMultiplier</c>（按平方根换算高度）+ <c>JumpSpeedMultiplier</c>
	/// （乘在 <c>AirAcceleration</c> / <c>MaxFallSpeed</c> 上）两个旋钮，用户要的是
	/// 「**直接改固定值**…**起跳速度也要快**」。现在表里给的就是绝对值：
	/// 起跳速度直接写进 <see cref="JumpVelocity"/>、重力缩放直接写进 <see cref="GravityScale"/>，
	/// 空中加速与最大下落**不再被乘任何倍率**（保持场景里的值）。
	/// </summary>
	private void ApplyJumpConfigDeferred()
	{
		if (_unit == null || !IsInstanceValid(_unit))
		{
			return;
		}

		// 只有玩家读表（按组判定）。
		if (!_unit.IsInGroup("player_units"))
		{
			return;
		}

		if (ConfigRows.BySlot<CharacterConfig>((int)RunSetup.Character + 1) is not { } row)
		{
			// 表读不到 → 保留场景里的值（"读不到则使用自身的默认值"），但必须留痕，否则"跳跃没变"会查不出来。
			ActRoguelike.Core.GameLog.Write(
				"Config",
				$"跳跃：角色行读不到（RunSetup.Character={RunSetup.Character} / 角色表 {ConfigRows.Count<CharacterConfig>()} 行）→ 起跳速度保持场景值 {JumpVelocity:0.##} m/s / 重力缩放 {GravityScale:0.##}");
			return;
		}

		// 直接改固定值（用户需求 A2：不要倍率、不要"逻辑运算"）。
		row.ApplyJumpTo(this);

		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"跳跃：角色行 {row.Id} → 起跳速度 {JumpVelocity:0.##} m/s / 重力缩放 {GravityScale:0.##}（两列都是**固定值**、直接套用，没有任何倍率运算）｜ 空中加速 {AirAcceleration:0.#} / 最大下落 {MaxFallSpeed:0.#} 保持场景值（来源：表 Characters，读不到则用场景导出值）");
	}

	public override void _ExitTree()
	{
		if (_unit != null)
		{
			_unit.KnockbackReceived -= OnKnockback;
		}
	}

	/// <summary>设置水平移动意图。direction 不需要归一化，speed 为 0 即停下。</summary>
	public void SetIntent(Vector3 direction, float speed)
	{
		_intentDirection = direction.LengthSquared() > 1f ? direction.Normalized() : direction;
		_intentSpeed = speed;
	}

	public void Stop()
	{
		_intentDirection = Vector3.Zero;
		_intentSpeed = 0f;
	}

	/// <summary>强制朝向（例如瞄准方向）；传零向量即恢复按移动方向转身。</summary>
	public void SetFacing(Vector3 direction)
	{
		_facing = direction;
		_hasFacing = direction.LengthSquared() > 0.0001f;
	}

	/// <summary>
	/// **实际起跳速度** = 表里的固定值 × 单位级 <c>JumpVelocityMultiplier</c>（强化「攀高」给单位挂这个修正器）。
	///
	/// <para>⚠️ 键没有基础值时按 **1** 算（`Unit.SeedStats` 里已经给了基础值 1；这里再兜一层，
	/// 免得少一个基础值就变成"跳不起来"）—— 这是 `architecture.md` 第 8 条那条纪律。
	/// 没人配置时逐位等于 <see cref="JumpVelocity"/>，所以 P6 之前的跳跃行为一位没变。</para>
	/// </summary>
	public float EffectiveJumpVelocity
	{
		get
		{
			float multiplier = _unit?.Stats.Get("JumpVelocityMultiplier") ?? 1f;
			return JumpVelocity * (multiplier > 0f ? multiplier : 1f);
		}
	}

	public bool TryJump()
	{
		if (HasVelocityOverride) return false;
		if (_body.IsOnFloor())
		{
			_body.Velocity = _body.Velocity with { Y = EffectiveJumpVelocity };
			return true;
		}

		if (_airJumpsLeft > 0)
		{
			_airJumpsLeft--;
			_body.Velocity = _body.Velocity with { Y = EffectiveAirJumpVelocity };
			return true;
		}

		return false;
	}

	public void AddImpulse(Vector3 impulse) => _externalVelocity += impulse;

	private void OnKnockback(Vector3 impulse) => AddImpulse(impulse * (1f - (_unit?.KnockbackResistance ?? 0f)));

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_unit is { IsDead: true })
		{
			return;
		}

		Vector3 velocity = _body.Velocity;

		if (_body.IsOnFloor())
		{
			_airJumpsLeft = ExtraAirJumps;
			// 落地：这次的"最高点"追踪结束（FallHeight 保留最后一次的值，落地那一帧仍然读得到）。
			_apexTracking = false;
		}
		else
		{
			velocity.Y = Mathf.Max(velocity.Y - _gravity * GravityScale * dt, -MaxFallSpeed);
			// P4：滞空期间维护"最高点"（离地即开始，落地结束）。
			float nowY = _body.GlobalPosition.Y;
			if (!_apexTracking)
			{
				_apexTracking = true;
				_apexY = nowY;
			}
			else
			{
				_apexY = Mathf.Max(_apexY, nowY);
			}

			FallHeight = Mathf.Max(0f, _apexY - nowY);
		}

		if (HasVelocityOverride)
		{
			_externalVelocity *= Mathf.Max(0, 1 - ImpulseDamping * dt);
			_body.Velocity = _overrideVelocity;
			_body.MoveAndSlide();
			if (FaceMovementDirection) FaceDirection(_hasFacing ? _facing : _overrideVelocity, dt);
			return;
		}

		// 输入速度单独维护：不能从刚体速度里读回来，否则击退会叠加上一帧的击退形成正反馈。
		Vector3 planar;
		if (_dashSpeed > 0f)
		{
			planar = _dashDirection * _dashSpeed;
			_inputVelocity = planar;
		}
		else
		{
			float speedMultiplier = _unit?.Stats.Get("MoveSpeedMultiplier") ?? 1f;
			Vector3 desired = _intentDirection * (_intentSpeed * speedMultiplier);
			float acceleration = (_body.IsOnFloor() ? GroundAcceleration : AirAcceleration) * dt;
			_inputVelocity = _inputVelocity.MoveToward(desired, acceleration);
			planar = _inputVelocity;
		}

		_externalVelocity *= Mathf.Max(0f, 1f - ImpulseDamping * dt);

		velocity.X = planar.X + _externalVelocity.X;
		velocity.Z = planar.Z + _externalVelocity.Z;
		velocity.Y += _externalVelocity.Y;
		_body.Velocity = velocity;
		_body.MoveAndSlide();

		if (!FaceMovementDirection)
		{
			return;
		}

		Vector3 face = _hasFacing ? _facing : planar;
		FaceDirection(face, dt);
	}

	public void FaceDirection(Vector3 direction, float dt)
	{
		if (IsFacingLocked) return;
		direction.Y = 0f;
		if (direction.LengthSquared() < 0.04f)
		{
			return;
		}

		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		float weight = 1f - Mathf.Exp(-TurnSharpness * dt);
		_body.Rotation = new Vector3(0f, Mathf.LerpAngle(_body.Rotation.Y, targetYaw, weight), 0f);
	}
}
