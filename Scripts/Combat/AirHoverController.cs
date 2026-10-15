using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **「滞空」**（角色 A 技能 2「跃升」的效果，设计文档第 30/31 行）：
///
/// <list type="bullet">
/// <item>「角色进行一次强力的跳跃，且获得一段时间{3s}的**滞空**」—— 时长由授予方（技能行的表值）
///   通过 <see cref="Grant"/> 传进来，这个组件自己没有任何秒数。</item>
/// <item>「滞空：当角色处于空中时**按住跳跃键**可大幅减缓{90}%下降速度（有按住特效，**脚底出现一个光环**）；
///   **当有跳跃次数时会先执行跳跃**」—— 所以本组件只在"已经在空中 + 还按着跳跃键"时把下落速度按
///   <see cref="FallSlowPercent"/> 削掉；按下那一帧若还有空中跳跃次数，由 <c>PlayerController</c> 先消费掉
///   （<c>IsActionJustPressed</c> 那条路），这里刻意**不抢那一次按下**。</item>
/// </list>
///
/// <para><b>为什么单独一个组件而不是写在技能里</b>：跃升技能在"动画事件帧"那一刻就 <c>Finish()</c> 了，
/// 而滞空要持续 3s —— 状态必须挂在一个**常驻**节点上（挂在玩家场景里，与技能行解耦）。</para>
///
/// <para><b>执行顺序</b>：<see cref="ProcessPhysicsPriority"/> 设成一个较大的值，
/// 保证它在 <c>CharacterMovement</c>（默认 0）之后跑 —— 这样"本帧重力已经加过"，
/// 我们改的就是**这一帧交给物理的最终速度**（下一帧 <c>CharacterMovement</c> 读回来的起点也更低）。</para>
///
/// <para>光环是**代码生成的环**（<c>TorusMesh</c>，无着色 + 自发光），不依赖任何美术资源；
/// 半径/颜色/高度都在导出属性里（技能行可以覆盖）。</para>
/// </summary>
public partial class AirHoverController : Node3D
{
	/// <summary>滞空期间按住跳跃键时的**下落速度削减比例**（0.9 = 减缓 90%）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float FallSlowPercent { get; set; } = 0.9f;

	/// <summary>跳跃键的动作名（`project.godot` 的 `[input]` 名）。</summary>
	[Export] public string JumpAction { get; set; } = "jump";

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

	/// <summary>本帧有没有真的在"减缓下落"（自测取证用）。</summary>
	public bool IsSlowFalling { get; private set; }

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
		if (seconds <= 0f)
		{
			return;
		}

		HoverRemaining = Mathf.Max(HoverRemaining, seconds);
		if (fallSlowPercent >= 0f)
		{
			FallSlowPercent = Mathf.Clamp(fallSlowPercent, 0f, 1f);
		}

		GrantCount++;
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
		bool holding = DebugForceJumpHeld || (!string.IsNullOrEmpty(JumpAction)
			&& InputMap.HasAction(JumpAction)
			&& Input.IsActionPressed(JumpAction));

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
