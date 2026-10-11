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

	/// <summary>落地后可用的额外空中跳跃次数（0 = 不能二段跳）。</summary>
	[Export] public int ExtraAirJumps { get; set; }

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

	public bool IsGrounded => _body.IsOnFloor();
	public Vector3 PlanarVelocity { get { Vector3 v = _body.Velocity; v.Y = 0f; return v; } }

	/// <summary>当前的移动意图方向（未归一化前的原样），位移技能用它决定冲刺方向。</summary>
	public Vector3 IntentDirection => _intentDirection;

	public bool IsDashing => _dashSpeed > 0f;

	/// <summary>位移技能用：在持续时间内以固定速度沿方向移动，忽略常规加速度。</summary>
	public void BeginDash(Vector3 direction, float speed)
	{
		_dashDirection = direction.LengthSquared() < 0.0001f ? -_body.GlobalTransform.Basis.Z : direction.Normalized();
		_dashSpeed = speed;
	}

	public void EndDash() => _dashSpeed = 0f;

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
		}
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

	public bool TryJump()
	{
		if (_body.IsOnFloor())
		{
			_body.Velocity = _body.Velocity with { Y = JumpVelocity };
			return true;
		}

		if (_airJumpsLeft > 0)
		{
			_airJumpsLeft--;
			_body.Velocity = _body.Velocity with { Y = JumpVelocity };
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
		}
		else
		{
			velocity.Y = Mathf.Max(velocity.Y - _gravity * GravityScale * dt, -MaxFallSpeed);
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
