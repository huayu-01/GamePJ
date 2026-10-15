using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 异化「落日剑」的**头顶那把剑**（设计文档 `角色设计.txt` 第 187-188 行）：
///
/// <code>
/// 落日剑:武技1会对没有落日剑的敌人施加落日剑;
/// (落日剑:为目标头顶悬挂一柄剑,一段时间后剑会落下造成伤害)
/// </code>
///
/// <list type="bullet">
/// <item><b>悬挂</b>：作为**目标敌人的子节点**（<see cref="NodeName"/> = `SunsetSword`），所以
///   "这个敌人头上有没有落日剑"就是 <c>GetNodeOrNull&lt;SunsetSwordMarker&gt;(NodeName)</c> ——
///   O(1) 按名查，不需要给敌人挂状态、也不需要新效果表行。悬挂期间每帧把
///   <c>GlobalPosition</c> 钉在"目标头顶 + <see cref="HoverHeight"/>"（用全局坐标而不是局部偏移：
///   精英怪会整体缩放，局部偏移会被缩放带跑）。</item>
/// <item><b>落下</b>：<see cref="HoverSeconds"/> 秒后从当前位置加速落到**施放时那个敌人所在的地面点**
///   （落点冻结，目标之后跑掉也不会把剑带走）。</item>
/// <item><b>伤害</b>：落地那一刻按 <see cref="Radius"/> / <see cref="Damage"/> 对周围敌人打一圈，
///   归属仍是**玩家**（<see cref="Attacker"/>）—— 走的是与「能量护盾」「杀戮践踏」同一份
///   <c>PulseDamage.Around</c>（P6d 给它加了一个"圆心可以不是施法者"的重载）。</item>
/// <item><b>落圈特效</b>：直接复用 **P6c 的** <see cref="LandingShockwave"/>（它本来就同时服务
///   "下落攻击落地"与"落剑式"两处，P6d 给它加了 <c>PlayOnce(位置, 半径)</c> 这个外部入口），
///   所以圈只有一个实现、几何量只有一份。</item>
/// </list>
///
/// <para>⚠️ <b>纯表现 + 一次性逻辑，绝不进单位列表</b>（`docs/vfx.md` §3 的纪律）：
/// 树里没有任何 <c>CollisionObject3D</c>、不进任何分组、不是 <c>Unit</c> ——
/// 它就是敌人子树里的一个 <c>Node3D</c>（<see cref="DebugState"/> 会把这三个量打出来）。
/// 目标死亡 → 自己摘掉（剑落下打空没有意义）。</para>
///
/// <para>几何量 / 配色 / 时长都是 <c>[Export]</c>；**玩法数值**（半径 / 伤害 / 悬挂与落下时长 / 高度）
/// 由 <see cref="SunsetSwordRule"/> 从增幅行写进来。</para>
/// </summary>
public partial class SunsetSwordMarker : Node3D
{
	/// <summary>节点名 —— "这个敌人身上有没有落日剑"就靠按这个名字查。</summary>
	public const string NodeName = "SunsetSword";

	[ExportGroup("时长（玩法数值由增幅行覆盖）")]
	/// <summary>悬挂多久之后落下（秒）。</summary>
	[Export] public float HoverSeconds { get; set; } = 1.5f;

	/// <summary>从悬挂位置落到地面要多久（秒）。</summary>
	[Export] public float FallSeconds { get; set; } = 0.35f;

	[ExportGroup("几何 / 配色")]
	/// <summary>悬挂高度（相对目标原点，米）。</summary>
	[Export] public float HoverHeight { get; set; } = 3.2f;

	/// <summary>剑刃长度（米）。</summary>
	[Export] public float BladeLength { get; set; } = 1.6f;

	/// <summary>剑刃宽度（米）。</summary>
	[Export] public float BladeWidth { get; set; } = 0.14f;

	/// <summary>剑刃厚度（米）。</summary>
	[Export] public float BladeThickness { get; set; } = 0.06f;

	/// <summary>护手宽度（米）。</summary>
	[Export] public float GuardWidth { get; set; } = 0.55f;

	/// <summary>剑柄长度（米）。</summary>
	[Export] public float HiltLength { get; set; } = 0.3f;

	/// <summary>剑刃颜色（落日色）。</summary>
	[Export] public Color BladeColor { get; set; } = new(1f, 0.63f, 0.24f, 1f);

	/// <summary>护手 / 剑柄颜色。</summary>
	[Export] public Color HiltColor { get; set; } = new(0.4f, 0.26f, 0.16f, 1f);

	/// <summary>自发光强度（白色地板上要看得见）。</summary>
	[Export] public float EmissionEnergy { get; set; } = 2.2f;

	/// <summary>悬挂期间的自转速度（度/秒；0 = 不转）。</summary>
	[Export] public float SpinDegreesPerSecond { get; set; } = 110f;

	[ExportGroup("落地那一圈（复用 LandingShockwave）")]
	/// <summary>圈的扩散时长（秒）。</summary>
	[Export] public float RingDuration { get; set; } = 0.45f;

	/// <summary>环的颜色。</summary>
	[Export] public Color RingColor { get; set; } = new(1f, 0.72f, 0.35f, 0.95f);

	/// <summary>盘的颜色。</summary>
	[Export] public Color DiscColor { get; set; } = new(1f, 0.6f, 0.25f, 0.2f);

	[ExportGroup("玩法数值（由 SunsetSwordRule 从增幅行写进来）")]
	/// <summary>落地伤害的作用半径（米）。</summary>
	public float Radius { get; set; } = 2.5f;

	/// <summary>落地伤害。</summary>
	public float Damage { get; set; } = 40f;

	/// <summary>归属（= 施加落日剑的那个玩家）。</summary>
	public Unit? Attacker { get; set; }

	/// <summary>伤害归属用的技能/增幅 Id（日志与 <c>AttackInstance.Ability</c>）。</summary>
	public string AbilityId { get; set; } = "mut_sunset_sword";

	/// <summary>
	/// **拍摄台专用**：钉在悬挂状态不落下（`docs/vfx.md` §2.7 的 `DebugPreviewXxx` 那一类）。
	/// **判据与画法一个字都不变** —— 只是"什么时候落"由拍摄台决定；正式流程里永远是 false。
	/// </summary>
	public bool HoldHoverForDebug { get; set; }

	[ExportGroup("运行期可读状态（自测 / 探针）")]
	/// <summary>落下了几次（自测取证；正常只有 1）。</summary>
	public int FallCount { get; private set; }

	/// <summary>剑已经落地了吗。</summary>
	public bool HasFallen { get; private set; }

	/// <summary>最近一次落地打到了几个敌人。</summary>
	public int LastHits { get; private set; }

	/// <summary>距落下还剩几秒（悬挂阶段）。</summary>
	public float HoverRemaining => Mathf.Max(0f, HoverSeconds - _elapsed);

	/// <summary>目标（= 父节点那个单位；目标没了就返回 null）。</summary>
	public Unit? Target => _unit != null && IsInstanceValid(_unit) ? _unit : null;

	private Unit? _unit;
	private Node3D? _pivot;
	private LandingShockwave? _ring;
	private float _elapsed;
	private float _fallElapsed;
	private Vector3 _fallFrom;
	private Vector3 _landCenter;
	private Vector3 _landSword;
	private bool _falling;
	private bool _warnedNoAttacker;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		if (_unit == null)
		{
			GameLog.Write("Reward", $"⚠ 落日剑：{Name} 的父节点不是 Unit，剑无处可挂，已收掉。");
			QueueFree();
			return;
		}

		BuildSword();

		// 落圈：复用 P6c 的组件（它自己没有上游事件时什么都不做，只等我们调 `PlayOnce`）。
		_ring = new LandingShockwave
		{
			Name = "Shockwave",
			Duration = Mathf.Max(0.05f, RingDuration),
			RingColor = RingColor,
			DiscColor = DiscColor,
			ExtraHeight = 0.02f,
			FallbackRadius = Radius,
		};
		AddChild(_ring);

		SnapToHoverPoint();

		GameLog.Write(
			"Reward",
			$"落日剑：施加于 {_unit.Name}（头顶悬挂，{HoverSeconds:0.##}s 后落下；落地半径 {Radius:0.##}m 伤害 {Damage:0.##} 落点 {_unit.GlobalPosition}）");
	}

	/// <summary>把剑搭出来（纯 <c>MeshInstance3D</c>：没有任何碰撞体）。</summary>
	private void BuildSword()
	{
		_pivot = new Node3D { Name = "Pivot" };
		AddChild(_pivot);

		StandardMaterial3D blade = Unshaded(BladeColor);
		StandardMaterial3D hilt = Unshaded(HiltColor);

		// 剑刃：从原点上沿往下挂（原点 = 护手那一圈）。
		var bladeMesh = new BoxMesh
		{
			Size = new Vector3(Mathf.Max(0.01f, BladeWidth), Mathf.Max(0.1f, BladeLength), Mathf.Max(0.01f, BladeThickness)),
		};
		_pivot.AddChild(new MeshInstance3D
		{
			Name = "Blade",
			Mesh = bladeMesh,
			Position = new Vector3(0f, -bladeMesh.Size.Y * 0.5f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = blade,
		});

		var guardMesh = new BoxMesh { Size = new Vector3(Mathf.Max(0.05f, GuardWidth), 0.06f, 0.12f) };
		_pivot.AddChild(new MeshInstance3D
		{
			Name = "Guard",
			Mesh = guardMesh,
			Position = new Vector3(0f, 0.02f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = hilt,
		});

		var hiltMesh = new CylinderMesh
		{
			TopRadius = 0.045f,
			BottomRadius = 0.05f,
			Height = Mathf.Max(0.05f, HiltLength),
			RadialSegments = 12,
			Rings = 0,
		};
		_pivot.AddChild(new MeshInstance3D
		{
			Name = "Hilt",
			Mesh = hiltMesh,
			Position = new Vector3(0f, 0.05f + (hiltMesh.Height * 0.5f), 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = hilt,
		});
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// 目标没了（死了被回收 / 被 Free）→ 剑没必要再落，自己摘掉。
		if (_unit == null || !IsInstanceValid(_unit))
		{
			QueueFree();
			return;
		}

		if (HasFallen)
		{
			return;
		}

		if (!_falling)
		{
			// 悬挂：跟着目标的头顶走（全局坐标，缩放/旋转都不影响）。
			SnapToHoverPoint();
			if (_pivot != null && SpinDegreesPerSecond != 0f)
			{
				_pivot.RotateY(Mathf.DegToRad(SpinDegreesPerSecond) * dt);
			}

			// ⚠️ 拍摄台（`HoldHoverForDebug`）只挡"计时 → 落下"这一步，画法一个字没变。
			if (HoldHoverForDebug)
			{
				return;
			}

			_elapsed += dt;
			if (_elapsed >= HoverSeconds)
			{
				BeginFall();
			}

			return;
		}

		// 落下：从悬挂点加速砸到**施放时冻结的那个落点**。
		_fallElapsed += dt;
		float k = Mathf.Clamp(_fallElapsed / Mathf.Max(0.02f, FallSeconds), 0f, 1f);
		GlobalPosition = _fallFrom.Lerp(_landSword, k * k);
		if (k >= 1f)
		{
			Strike();
		}
	}

	private void SnapToHoverPoint()
	{
		if (_unit != null && IsInstanceValid(_unit))
		{
			GlobalPosition = _unit.GlobalPosition + (Vector3.Up * HoverHeight);
		}
	}

	/// <summary>开始落下：把落点**冻结**在这一刻（"剑会落下造成伤害"落在它当时站的地方）。</summary>
	private void BeginFall()
	{
		_falling = true;
		_fallElapsed = 0f;
		_fallFrom = GlobalPosition;
		_landCenter = _unit?.GlobalPosition ?? _fallFrom;
		_landSword = _landCenter + (Vector3.Up * (BladeLength + 0.06f));
		GameLog.Write("Reward", $"落日剑：{Name} 开始落下（落点 {_landCenter}）");
	}

	/// <summary>落地：打一圈 + 画一圈 + 自己摘掉。</summary>
	private void Strike()
	{
		HasFallen = true;
		FallCount++;
		GlobalPosition = _landSword;

		bool valid = Attacker != null && IsInstanceValid(Attacker);
		if (valid)
		{
			LastHits = PulseDamage.Around(this, Attacker!, _landCenter, Radius, Damage, AbilityId);
		}
		else if (!_warnedNoAttacker)
		{
			_warnedNoAttacker = true;
			GameLog.Write("Reward", $"⚠ 落日剑：{Name} 的归属单位已经没了 → 这一次只有特效、没有伤害。");
		}

		_ring?.PlayOnce(_landCenter, Radius);

		GameLog.Write(
			"Reward",
			$"落日剑：{Name} 落地（半径 {Radius:0.##}m 伤害 {Damage:0.##} → 命中 {LastHits} 个敌人；落圈半径与判定同一个数）");

		QueueFree();
	}

	/// <summary>自发光 + 不受光（与落圈 / 残影同一套写法：颜色只由导出的 tint 决定）。</summary>
	private StandardMaterial3D Unshaded(Color tint) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		AlbedoColor = tint,
		EmissionEnabled = true,
		Emission = new Color(tint.R, tint.G, tint.B, 1f),
		EmissionEnergyMultiplier = EmissionEnergy,
	};

	/// <summary>取证量①：子树里 <c>CollisionObject3D</c> 的个数（必须 0）。</summary>
	public int CountCollisions() => CountInSubtree<CollisionObject3D>(this);

	/// <summary>取证量②：子树里 <c>Unit</c> 的个数（必须 0 —— 剑绝不是一个"单位"）。</summary>
	public int CountUnits() => CountInSubtree<Unit>(this);

	private static int CountInSubtree<T>(Node root) where T : class
	{
		int count = 0;
		foreach (Node child in root.GetChildren())
		{
			if (child is T)
			{
				count++;
			}

			count += CountInSubtree<T>(child);
		}

		return count;
	}

	/// <summary>探针用：一行的实际状态（含"绝不进单位列表"的三个取证量）。</summary>
	public string DebugState()
		=> $"落日剑：目标={(Target != null ? Target.Name : "（没了）")} 悬挂剩余={HoverRemaining:0.##}s 已落下={HasFallen} 落点命中={LastHits}"
			+ $"｜数值 半径={Radius:0.##}m 伤害={Damage:0.##} 悬挂={HoverSeconds:0.##}s 落下={FallSeconds:0.##}s 高度={HoverHeight:0.##}m"
			+ $"｜纯表现 碰撞体={CountCollisions()} 单位={CountUnits()} 分组={GetGroups().Count}（三个都必须是 0）";
}
