using Godot;

namespace ActRoguelike.Combat;

/// <summary>统一浮空状态：授予后在空中自动减缓下落，持续时间耗尽或按可重绑的下落动作取消。
/// 上升不受影响，主动跳跃优先；Ctrl取消不吞输入，角色B可在同一次按键中发动下落攻击。
/// 光环由组件管理，授予方只提供时长与表现参数。</summary>
public partial class AirHoverController : Node3D
{
	/// <summary>滞空自动保持期间的下落速度削减比例（0.9 = 减缓 90%）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float FallSlowPercent { get; set; } = 0.9f;

	/// <summary>额外跳跃优先判定使用的动作名；不再用于维持浮空。</summary>
	[Export] public string JumpAction { get; set; } = "jump";
	/// <summary>共用可重绑的下落动作；取消浮空不吞掉按键，角色B仍可开始下落攻击。</summary>
	[Export] public string CancelAction { get; set; } = "fall_attack";
	public bool Maintaining { get; private set; }

	[ExportGroup("脚底光环（表现）")]
	/// <summary>光环外半径（米）。</summary>
	[Export] public float RingRadius { get; set; } = 1.15f;

	/// <summary>光环粗细（米）。</summary>
	[Export] public float RingThickness { get; set; } = 0.07f;

	/// <summary>光环离脚底的高度（米）。</summary>
	[Export] public float RingHeight { get; set; } = 0.12f;

	/// <summary>光环颜色（自发光）。</summary>
	[Export] public Color RingColor { get; set; } = new(0.45f, 0.85f, 1f, 0.9f);

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>还剩多少秒滞空（只在该角色**处于空中**时才会走表）。</summary>
	public float HoverRemaining { get; private set; }

	/// <summary>是不是处在滞空状态（还剩时间）。</summary>
	public bool IsHovering => HoverRemaining > 0f;
	/// <summary>只延长已经存在的浮空，不把普通跳跃变成浮空。</summary>
	public bool Extend(float seconds)
	{
		if (!IsHovering || !Maintaining || !float.IsFinite(seconds) || seconds <= 0 || _unit.IsDead) return false;
		HoverRemaining += seconds; return true;
	}

	/// <summary>本帧有没有真的在"减缓下落"（自测取证用）。</summary>
	public bool IsSlowFalling { get; private set; }

	/// <summary>
	/// **浮空时长倍率**（默认 1，不改变任何既有行为）。
	/// 风之领域按用户口径「角色获得的**所有**浮空 +50%」在启用期间设为 1.5，撤销时还原。
	/// </summary>
	public float DurationScale { get; set; } = 1f;

	/// <summary>累计授予过几次滞空（自测取证用）。</summary>
	public int GrantCount { get; private set; }

	/// <summary>
	/// 自测专用：强制当作"正按住跳跃键"。
	///
	/// <para>为什么需要它：自测环境**注入不了输入事件**（`Input.ParseInputEvent` 会让进程崩，
	/// 见 docs/anim-pipeline.md §6 与 SelfTest 里那条注释），所以"按住跳跃键才减缓下落"这条
	/// 在自测里没法靠真按键复现 —— 真人手测那一路照旧走真 <c>Input.IsActionPressed</c>。</para>
	/// </summary>
	public bool DebugForceJumpHeld { get; set; }

	private Unit _unit = null!;
	private CharacterBody3D _body = null!;
	private MeshInstance3D? _ring;
	private StandardMaterial3D? _ringMaterial;
	private float _pulse;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_body = _unit;
		// 比 CharacterMovement（默认 0）晚跑：改的是"这一帧最终交给物理的速度"。
		ProcessPhysicsPriority = 50;
		BuildRing();
	}

	/// <summary>
	/// 授予一段滞空（秒）。**取较大值**（重复授予不会缩短已有的滞空）。
	/// 时长是**表值** —— 由技能行（<c>HoverSeconds</c>）传给本方法。
	/// </summary>
	public void Grant(float seconds, float fallSlowPercent = -1f)
	{
		// 倍率只放大"新授予"的时长，默认 1 时与改动前逐位一致（风之领域启用期间为 1.5）。
		if (float.IsFinite(DurationScale) && DurationScale > 0f)
		{
			seconds *= DurationScale;
		}

		if (seconds <= 0f)
		{
			return;
		}

		HoverRemaining = Mathf.Max(HoverRemaining, seconds);
		Maintaining = true;
		if (fallSlowPercent >= 0f)
		{
			FallSlowPercent = Mathf.Clamp(fallSlowPercent, 0f, 1f);
		}

		GrantCount++;
	}

	/// <summary>更换装备时移除旧角色授予的滞空。</summary>
	public void ResetForLoadout()
	{
		HoverRemaining = 0;
		Maintaining = false;
		IsSlowFalling = false;
		SetRingVisible(false);
	}

	/// <summary>
	/// 用**表值**重设光环样式（半径 / 粗细 / 高度 / 颜色）。由授予滞空的技能行在那一刻调一次
	/// （技能行的 Params 是权威；组件上的默认值只是"没人配时"的兜底）。
	/// </summary>
	public void ApplyRingStyle(float radius, float thickness, float height, Color color)
	{
		if (radius > 0f)
		{
			RingRadius = radius;
		}

		if (thickness > 0f)
		{
			RingThickness = thickness;
		}

		RingHeight = height;
		RingColor = color;

		if (_ring?.Mesh is TorusMesh torus)
		{
			torus.InnerRadius = Mathf.Max(0.05f, RingRadius - RingThickness);
			torus.OuterRadius = Mathf.Max(0.06f, RingRadius);
		}

		if (_ringMaterial != null)
		{
			_ringMaterial.AlbedoColor = RingColor;
			_ringMaterial.Emission = new Color(RingColor.R, RingColor.G, RingColor.B);
		}

		if (_ring != null)
		{
			_ring.Position = new Vector3(0f, RingHeight, 0f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		IsSlowFalling = false;

		if (_body == null || _unit.IsDead)
		{
			SetRingVisible(false);
			return;
		}

		bool airborne = !_body.IsOnFloor();
		if (IsHovering && !string.IsNullOrEmpty(CancelAction) && InputMap.HasAction(CancelAction) && Input.IsActionJustPressed(CancelAction)) ResetForLoadout();
		bool holding = Maintaining || DebugForceJumpHeld;

		if (!airborne)
		{
			// 站在地上不消耗滞空（否则 3s 会在起跳前就烧掉），也不减缓。
			SetRingVisible(false);
			return;
		}

		if (HoverRemaining <= 0f)
		{
			SetRingVisible(false);
			return;
		}

		HoverRemaining = Mathf.Max(0f, HoverRemaining - dt);
		if (HoverRemaining <= 0) { Maintaining = false; SetRingVisible(false); return; }

		if (!holding)
		{
			SetRingVisible(false);
			return;
		}

		// 「当有跳跃次数时会先执行跳跃」：还有空中跳跃次数 + 这一帧刚按下 → 那一次按下归跳跃，
		// 本组件这一帧不插手（下一帧仍然按住时才进入减缓）。
		bool jumpConsumed = !DebugForceJumpHeld
			&& _unit.Movement is { AirJumpsLeft: > 0 }
			&& Input.IsActionJustPressed(JumpAction);
		if (jumpConsumed)
		{
			SetRingVisible(true);
			return;
		}

		Vector3 velocity = _body.Velocity;
		if (velocity.Y < 0f)
		{
			// 只削**下落**（上升段不动，否则"强力的跳跃"会被吃掉）。
			velocity.Y *= 1f - Mathf.Clamp(FallSlowPercent, 0f, 1f);
			_body.Velocity = velocity;
			IsSlowFalling = true;
		}

		SetRingVisible(true);
	}

	/// <summary>代码生成脚底光环（无美术依赖）：一圈扁平的自发光环。</summary>
	private void BuildRing()
	{
		_ring = new MeshInstance3D { Name = "HoverRing" };
		_ringMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = RingColor,
			EmissionEnabled = true,
			Emission = new Color(RingColor.R, RingColor.G, RingColor.B),
			EmissionEnergyMultiplier = 2.4f,
		};

		_ring.Mesh = new TorusMesh
		{
			InnerRadius = Mathf.Max(0.05f, RingRadius - RingThickness),
			OuterRadius = Mathf.Max(0.06f, RingRadius),
			Rings = 24,
			RingSegments = 12,
		};
		_ring.MaterialOverride = _ringMaterial;
		_ring.Position = new Vector3(0f, RingHeight, 0f);
		_ring.Visible = false;
		AddChild(_ring);
	}

	private void SetRingVisible(bool visible)
	{
		if (_ring == null || _ring.Visible == visible)
		{
			return;
		}

		_ring.Visible = visible;
		_pulse = 0f;
	}

	public override void _Process(double delta)
	{
		if (_ring == null || !_ring.Visible)
		{
			return;
		}

		// 轻微呼吸 + 自转：让"按住键在减速"这件事看得出来。
		_pulse += (float)delta;
		_ring.RotateY((float)delta * 1.6f);
		float breath = 1f + (Mathf.Sin(_pulse * 6f) * 0.06f);
		_ring.Scale = new Vector3(breath, 1f, breath);
	}
}
