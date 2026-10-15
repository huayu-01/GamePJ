using ActRoguelike.Abilities;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **落圈特效**（P6c 表现层；设计文档 `角色设计.txt` 第 112 行 + 第 99 行）：
///
/// <blockquote>
/// 「武技3[落剑式]{被动,cd:5}:你的下落攻击在落地时会造成额外的大范围攻击
/// （不改变原有攻击，就是**多一圈攻击判定 + 特效**）;」
/// </blockquote>
///
/// <para><b>"一圈"的几何量来自战斗层，不写死</b>：半径直接读**同一个攻击命中体的
/// <c>CollisionShape3D</c>**（<see cref="ShapePath"/> 默认 <c>Attack/Shape</c>）——
/// 也就是 <c>RewardCatalog.PulseDamage.Around</c> / <c>AttackHitbox</c> 真正用的那个
/// <see cref="SphereShape3D.Radius"/>。表/场景把范围调大，圈跟着变大；读不到形状才退回
/// <see cref="FallbackRadius"/>（只读，绝不改判定）。</para>
///
/// <para><b>挂在哪</b>：作为**技能节点的子节点**，两种上游都能挂（同一个组件）：</para>
/// <list type="bullet">
/// <item>父节点是 <see cref="FallAttackAbility"/>（被动「落」的下落攻击）→ 订阅它公开的
///   <see cref="FallAttackAbility.Landed"/> 事件，落地那一帧就画。</item>
/// <item>父节点是 <see cref="LandingBurstAbility"/>（灵剑 武技3「落剑式」的额外那圈）→
///   轮询它公开的只读计数 <see cref="LandingBurstAbility.TriggerCount"/> 的上升沿
///   （**只读**；那个类没有事件，不加事件到战斗层）。</item>
/// </list>
///
/// <para><b>怎么画</b>：在落地点的地面高度上放一个临时 <c>Node3D</c>（挂到 <c>CurrentScene</c>），
/// 里面一个 <c>TorusMesh</c> 地面环 + 一个很薄的 <c>CylinderMesh</c> 冲击波盘；整体从
/// <see cref="StartScale"/> 放大到 1 同时淡出，<see cref="Duration"/> 秒后自回收 ——
/// 与 <c>ArmorBreakFx</c>（破甲特效）同一套写法。</para>
///
/// <para>⚠️ <b>纯表现</b>：没有任何 <c>CollisionObject3D</c>、不进任何分组、
/// 不改伤害 / 不改判定（那一圈伤害是 <see cref="LandingBurstAbility"/> 自己开的命中盒）。</para>
///
/// <para>所有几何量 / 颜色 / 时长都是 <c>[Export]</c>。</para>
/// </summary>
public partial class LandingShockwave : Node3D
{
	[ExportGroup("范围（从战斗层读，不写死）")]
	/// <summary>去哪个节点读"这一圈多大"（相对本节点的父技能节点）。默认 = 那个技能的命中体形状。</summary>
	[Export] public NodePath ShapePath { get; set; } = "Attack/Shape";

	/// <summary>读不到形状时用的半径（米；只是兜底，正常路径走上面的形状）。</summary>
	[Export] public float FallbackRadius { get; set; } = 3f;

	[ExportGroup("时长 / 几何")]
	/// <summary>整圈扩散完的总时长（秒）。</summary>
	[Export] public float Duration { get; set; } = 0.38f;

	/// <summary>起始缩放（1 = 一出现就是完整半径；小一点才有"扩散"感）。</summary>
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float StartScale { get; set; } = 0.15f;

	/// <summary>环的粗细（占半径的比例）。</summary>
	[Export(PropertyHint.Range, "0.01,0.6,0.01")] public float RingThickness { get; set; } = 0.12f;

	/// <summary>离地抬升（米；避免 z-fighting 与埋进地面）。</summary>
	[Export] public float GroundOffset { get; set; } = 0.07f;

	/// <summary>额外抬升（米）——「落剑式」那一圈可以画得比下落攻击高一点点，两圈就分得开。</summary>
	[Export] public float ExtraHeight { get; set; }

	/// <summary>再画一层薄薄的"冲击波盘"（地面上的亮盘）。</summary>
	[Export] public bool DrawDisc { get; set; } = true;

	/// <summary>盘的厚度（米）。</summary>
	[Export] public float DiscHeight { get; set; } = 0.05f;

	[ExportGroup("配色")]
	/// <summary>环的颜色（饱和一点的青蓝 —— 白地板上淡色看不见）。</summary>
	[Export] public Color RingColor { get; set; } = new(0.42f, 0.86f, 1f, 0.95f);

	/// <summary>盘的颜色（alpha 建议比环低）。</summary>
	[Export] public Color DiscColor { get; set; } = new(0.35f, 0.8f, 1f, 0.2f);

	/// <summary>自发光强度。</summary>
	[Export] public float EmissionEnergy { get; set; } = 2.4f;

	/// <summary>
	/// 加法混合。⚠️ **默认关**：竞技场地面是白的，加法混合会把环和盘一起冲成一片白
	/// （实测截图里只剩一个发白的圆，看不出是"环"）。普通半透明才有颜色。
	/// 深色地面想要发光感再打开。
	/// </summary>
	[Export] public bool AdditiveBlend { get; set; }

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>一共画了几圈（自测取证）。</summary>
	public int SpawnCount { get; private set; }

	/// <summary>最近一次画的半径（米）—— 应该与上游命中体的半径一致。</summary>
	public float LastRadius { get; private set; }

	/// <summary>最近一次半径的来源说明。</summary>
	public string LastRadiusSource { get; private set; } = "（还没画过）";

	/// <summary>现在是不是挂在"下落攻击"上（另一个分支是"落剑式"）。</summary>
	public bool WatchingFallAttack { get; private set; }

	private FallAttackAbility? _fall;
	private LandingBurstAbility? _burst;
	private int _lastTriggerCount = -1;
	private bool _warnedNoShape;
	private bool _hooked;

	public override void _Ready()
	{
		_fall = GetParentOrNull<FallAttackAbility>();
		_burst = GetParentOrNull<LandingBurstAbility>();
		WatchingFallAttack = _fall != null;

		if (_fall != null)
		{
			_fall.Landed += OnLanded;
			_hooked = true;
		}
	}

	public override void _ExitTree()
	{
		if (_hooked && _fall != null && IsInstanceValid(_fall))
		{
			_fall.Landed -= OnLanded;
		}

		_hooked = false;
	}

	/// <summary>下落攻击落地（<see cref="FallAttackAbility.Landed"/>）。</summary>
	private void OnLanded() => Spawn();

	public override void _Process(double delta)
	{
		// 「落剑式」那边没有事件，只有公开的只读计数 —— 走上升沿（**只读，不改战斗层**）。
		if (_burst == null || !IsInstanceValid(_burst))
		{
			return;
		}

		int count = _burst.TriggerCount;
		if (_lastTriggerCount < 0)
		{
			_lastTriggerCount = count;   // 进场景时先对齐基线，不补画历史
			return;
		}

		if (count != _lastTriggerCount)
		{
			_lastTriggerCount = count;
			Spawn();
		}
	}

	/// <summary>在落地点的地面上画一圈（自建临时节点，画完自己回收）。</summary>
	private void Spawn()
	{
		Unit? owner = LandingUnit();
		if (owner == null || !IsInstanceValid(owner))
		{
			return;
		}

		Node? host = GetTree()?.CurrentScene;
		if (host == null || !IsInstanceValid(host))
		{
			return;
		}

		float radius = ResolveRadius();
		LastRadius = radius;

		var holder = new Node3D { Name = "LandingShockwaveFx" };
		host.AddChild(holder);
		holder.GlobalPosition = owner.GlobalPosition + (Vector3.Up * (GroundOffset + ExtraHeight));
		holder.Scale = Vector3.One * Mathf.Clamp(StartScale, 0.01f, 1f);

		StandardMaterial3D ringMaterial = Unshaded(RingColor);
		StandardMaterial3D? discMaterial = DrawDisc ? Unshaded(DiscColor) : null;

		MeshInstance3D ring = RingMesh(radius, ringMaterial);
		holder.AddChild(ring);
		if (discMaterial != null)
		{
			holder.AddChild(DiscMesh(radius, discMaterial));
		}

		Tween tween = holder.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(holder, "scale", Vector3.One, Mathf.Max(0.05f, Duration))
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		tween.TweenProperty(ringMaterial, "albedo_color:a", 0f, Mathf.Max(0.05f, Duration));
		tween.TweenProperty(ringMaterial, "emission_energy_multiplier", 0.2f, Mathf.Max(0.05f, Duration));

		if (discMaterial != null)
		{
			tween.TweenProperty(discMaterial, "albedo_color:a", 0f, Mathf.Max(0.05f, Duration) * 0.8f);
		}

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(holder))
			{
				holder.QueueFree();
			}
		}));

		SpawnCount++;
		GameLog.Write(
			"Combat",
			$"落圈特效：{(_fall != null ? "下落攻击" : "落剑式")} 落地画一圈 —— 半径 {radius:0.##}m（{LastRadiusSource}），"
			+ $"扩散 {Duration:0.##}s（第 {SpawnCount} 次；纯视觉，不改判定）");
	}

	/// <summary>
	/// 半径 = **上游命中体真正的那个形状**的半径（球体）。这就是
	/// `<c>AttackHitbox</c>` / <c>PulseDamage.Around</c> 用的几何量，表现层只是照它画。
	/// </summary>
	private float ResolveRadius()
	{
		Node? parent = GetParent();
		if (parent != null && !ShapePath.IsEmpty && parent.GetNodeOrNull<CollisionShape3D>(ShapePath) is { } shape)
		{
			if (shape.Shape is SphereShape3D sphere && sphere.Radius > 0f)
			{
				LastRadiusSource = $"命中体 {ShapePath}/SphereShape3D.Radius";
				return sphere.Radius;
			}

			if (shape.Shape is CapsuleShape3D capsule && capsule.Radius > 0f)
			{
				LastRadiusSource = $"命中体 {ShapePath}/CapsuleShape3D.Radius";
				return capsule.Radius;
			}

			if (shape.Shape is BoxShape3D box)
			{
				Vector3 half = box.Size * 0.5f;
				float longest = Mathf.Max(half.X, Mathf.Max(half.Y, half.Z));
				if (longest > 0f)
				{
					LastRadiusSource = $"命中体 {ShapePath}/BoxShape3D 最长半边";
					return longest;
				}
			}
		}

		if (!_warnedNoShape)
		{
			_warnedNoShape = true;
			GameLog.Write("Combat", $"⚠ 落圈特效：{ShapePath} 读不到球体/胶囊/盒形状 → 用兜底半径 {FallbackRadius:0.##}m。");
		}

		LastRadiusSource = $"兜底（读不到 {ShapePath}）";
		return Mathf.Max(0.1f, FallbackRadius);
	}

	/// <summary>圈画在谁的脚下：技能节点的父单位（<c>Player</c>）。</summary>
	private Unit? LandingUnit()
	{
		Node? ability = GetParent();
		if (ability?.GetParent() is Unit unit)
		{
			return unit;
		}

		return _fall?.OwnerUnit ?? _burst?.OwnerUnit;
	}

	private MeshInstance3D RingMesh(float radius, Material material)
	{
		float outer = Mathf.Max(0.05f, radius);
		float thickness = Mathf.Clamp(RingThickness, 0.01f, 0.9f);
		var mesh = new TorusMesh
		{
			OuterRadius = outer,
			InnerRadius = Mathf.Max(0.01f, outer * (1f - thickness)),
			// ⚠️ Godot 的 TorusMesh：`Rings` = **主圈切几段**（就是"圆不圆"），
			// `RingSegments` = 管子横截面几段。**两个写反了就是菱形**（实测踩过：Rings=4 画出来是个方块）。
			Rings = 64,
			RingSegments = 6,
		};

		return new MeshInstance3D
		{
			Name = "Ring",
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = material,
		};
	}

	private MeshInstance3D DiscMesh(float radius, Material material)
	{
		var mesh = new CylinderMesh
		{
			TopRadius = Mathf.Max(0.05f, radius),
			BottomRadius = Mathf.Max(0.05f, radius),
			Height = Mathf.Max(0.01f, DiscHeight),
			RadialSegments = 48,
			Rings = 0,
		};

		return new MeshInstance3D
		{
			Name = "Disc",
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = material,
		};
	}

	/// <summary>自发光 + 半透明 + 不受光：颜色只由导出的 tint 决定，不看场景光照。</summary>
	private StandardMaterial3D Unshaded(Color tint) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		BlendMode = AdditiveBlend ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		AlbedoColor = tint,
		EmissionEnabled = true,
		Emission = new Color(tint.R, tint.G, tint.B, 1f),
		EmissionEnergyMultiplier = EmissionEnergy,
	};

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"落圈特效：挂={(WatchingFallAttack ? "下落攻击（订阅 Landed 事件）" : "落剑式（轮询 TriggerCount 上升沿）")}"
			+ $" 已画={SpawnCount} 圈 最近半径={LastRadius:0.##}m（{LastRadiusSource}）"
			+ $"（时长 {Duration:0.##}s 起始缩放 {StartScale:0.##} 环粗 {RingThickness:0.##} 离地 {GroundOffset:0.##} 盘={DrawDisc}）"
			+ $"｜只读源 命中体={ShapePath}"
			+ $"｜纯表现 碰撞体=0 分组=0";
}
