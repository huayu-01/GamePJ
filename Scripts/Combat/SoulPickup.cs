using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 异化「灵魂收割」掉下来的那个**灵魂**（设计文档 `角色设计.txt` 第 184-185 行）：
///
/// <code>
/// 灵魂收割:击杀时敌人有概率掉落一个"灵魂";
/// (灵魂:玩家接触就拾取,恢复一定血量)
/// </code>
///
/// <list type="bullet">
/// <item><b>拾取判定 = 距离轮询</b>（不是 <c>Area3D</c>）：每帧看 <c>player_units</c> 组里
///   有没有单位进到 <see cref="PickupRadius"/> 米内 —— 「玩家**接触**就拾取」的"接触"就是一个半径，
///   用轮询实现**最省**：没有物理体、没有碰撞层、没有新 <c>.tscn</c>、不参与任何碰撞/伤害/清场链路
///   （见下面那条"为什么不建 Area3D"）。</item>
/// <item><b>回血</b>：<c>Unit.Heal(amount)</c> —— 这是本类**唯一**一次战斗层写入（其余全是表现）。</item>
/// <item><b>表现</b>：一颗自发光小球 + 地面上一圈"拾取范围"环，出场时从地面升起来，之后原地浮沉 + 自转，
///   超时自己消失（<see cref="LifetimeSeconds"/>）。</item>
/// </list>
///
/// <para><b>为什么不用 <c>Area3D</c>（P5b 当时猜的做法）</b>：<c>docs/vfx.md</c> §3 那条纪律要的是
/// "掉落物/特效不能进单位列表、不能吃伤害、不能被清场误伤"。<c>Area3D</c> 能做到（碰撞层 0 + 只掩码玩家），
/// 但它仍然**要进物理世界**：会被 <c>AttackHitbox</c> 的重叠查询看到、会被 <c>KillZone</c> 之类的区域逻辑
/// 当成"进入区域的东西"。轮询一个半径**一点都不碰物理世界**，行为更好预测，代码也更少
/// （省掉一个 <c>.tscn</c>、省掉预制体路径那一列）。</para>
///
/// <para><b>取证量</b>（<see cref="DebugState"/>）：<c>Unit</c> 数 = 0 · 分组 = 0 ·
/// <c>CollisionObject3D</c> 数 = 0 —— 三个都是"它绝不进单位列表"的硬证据。</para>
///
/// <para>几何量 / 配色 / 时长都是 <c>[Export]</c>；**玩法数值**（回血量 / 拾取半径 / 存活时长）
/// 由 <c>SoulHarvestRule</c> 从增幅行写进来。</para>
/// </summary>
public partial class SoulPickup : Node3D
{
	/// <summary>节点名（探针按名字计数用）。</summary>
	public const string NodeName = "Soul";

	[ExportGroup("拾取（玩法数值由增幅行写进来）")]
	/// <summary>拾取半径（米）—— 玩家进到这个距离内就被拾取。</summary>
	[Export] public float PickupRadius { get; set; } = 1.2f;

	/// <summary>拾取时回复的生命。</summary>
	[Export] public float HealAmount { get; set; } = 8f;

	/// <summary>存活多久（秒）后自己消失（0 = 不消失）。文档没写，预设值。</summary>
	[Export] public float LifetimeSeconds { get; set; } = 15f;

	/// <summary>掉出来之后多久允许被拾取（秒；给一点"掉出来"的观感，0 = 立刻可拾取）。</summary>
	[Export] public float PickupDelaySeconds { get; set; }

	[ExportGroup("表现 · 出场与浮沉")]
	/// <summary>升起的高度（米）。</summary>
	[Export] public float RiseHeight { get; set; } = 0.9f;

	/// <summary>升起耗时（秒）。</summary>
	[Export] public float RiseSeconds { get; set; } = 0.3f;

	/// <summary>浮沉幅度（米）。</summary>
	[Export] public float BobAmplitude { get; set; } = 0.12f;

	/// <summary>浮沉周期（秒）。</summary>
	[Export] public float BobSeconds { get; set; } = 1.1f;

	/// <summary>自转速度（度/秒）。</summary>
	[Export] public float SpinDegreesPerSecond { get; set; } = 90f;

	[ExportGroup("表现 · 几何与配色")]
	/// <summary>球半径（米）。</summary>
	[Export] public float OrbRadius { get; set; } = 0.22f;

	/// <summary>灵魂的颜色。</summary>
	[Export] public Color SoulColor { get; set; } = new(0.62f, 0.96f, 1f, 0.92f);

	/// <summary>自发光强度。</summary>
	[Export] public float EmissionEnergy { get; set; } = 2.6f;

	/// <summary>地面那圈"拾取范围"的颜色。</summary>
	[Export] public Color RingColor { get; set; } = new(0.55f, 0.9f, 1f, 0.5f);

	/// <summary>地面那一圈的高度（米，避免埋进地面）。</summary>
	[Export] public float RingGroundOffset { get; set; } = 0.05f;

	[ExportGroup("运行期可读状态（自测 / 探针）")]
	/// <summary>被拾取了吗。</summary>
	public bool PickedUp { get; private set; }

	/// <summary>被谁拾取的（自测取证）。</summary>
	public string PickedByName { get; private set; } = "（还没被拾取）";

	/// <summary>实际回了多少血（受生命上限夹住，自测取证）。</summary>
	public float HealedAmount { get; private set; }

	/// <summary>存活了几秒（自测取证）。</summary>
	public float Age => _age;

	/// <summary>超时消失了吗。</summary>
	public bool Expired { get; private set; }

	/// <summary>谁掉的（只用于日志）。</summary>
	public string SourceAbilityId { get; set; } = string.Empty;

	private float _age;
	private float _rise;
	private Node3D? _pivot;

	public override void _Ready()
	{
		Build();

		// 掉在**地面上**：从掉落点起一点高度开始升起。
		GlobalPosition += Vector3.Up * 0.15f;

		GameLog.Write(
			"Reward",
			$"灵魂收割：掉落灵魂（拾取半径 {PickupRadius:0.##}m / 回复 {HealAmount:0.##} 点 / 存活 {LifetimeSeconds:0.##}s，位置 {GlobalPosition}）");
	}

	/// <summary>搭出那颗球与地面那圈（纯 <c>MeshInstance3D</c>：**没有任何 <c>CollisionObject3D</c>**）。</summary>
	private void Build()
	{
		_pivot = new Node3D { Name = "Pivot" };
		AddChild(_pivot);

		StandardMaterial3D soul = Unshaded(SoulColor, EmissionEnergy);
		var orb = new SphereMesh
		{
			Radius = Mathf.Max(0.03f, OrbRadius),
			Height = Mathf.Max(0.06f, OrbRadius * 2f),
			RadialSegments = 16,
			Rings = 8,
		};
		_pivot.AddChild(new MeshInstance3D
		{
			Name = "Orb",
			Mesh = orb,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = soul,
		});

		// 内芯亮一点（两层叠加就有"发光"的观感，不需要后处理）。
		_pivot.AddChild(new MeshInstance3D
		{
			Name = "Core",
			Mesh = new SphereMesh
			{
				Radius = Mathf.Max(0.02f, OrbRadius * 0.5f),
				Height = Mathf.Max(0.04f, OrbRadius),
				RadialSegments = 12,
				Rings = 6,
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = Unshaded(new Color(1f, 1f, 1f, 0.95f), EmissionEnergy * 1.4f),
		});

		// 地面上那圈 = 拾取范围（玩家一眼能看出"走到圈里就捡到"）。
		var ringMesh = new TorusMesh
		{
			OuterRadius = Mathf.Max(0.1f, PickupRadius),
			InnerRadius = Mathf.Max(0.06f, PickupRadius * 0.92f),
			// ⚠️ Godot 的 TorusMesh：`Rings` = 主圈切几段（圆不圆），`RingSegments` = 管子横截面。
			// 写反了圆会画成菱形（docs/vfx.md §4 那条坑）。
			Rings = 64,
			RingSegments = 6,
		};
		var ring = new MeshInstance3D
		{
			Name = "PickupRing",
			Mesh = ringMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = Unshaded(RingColor, EmissionEnergy),
		};
		AddChild(ring);
		ring.Position = new Vector3(0f, -0.15f + RingGroundOffset, 0f);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;

		// 升起来 → 原地浮沉 + 自转（纯表现）。
		_rise = Mathf.Min(1f, _rise + (RiseSeconds <= 0f ? 1f : dt / RiseSeconds));
		float lift = RiseHeight * _rise;
		float bob = BobAmplitude * Mathf.Sin(_age * Mathf.Tau / Mathf.Max(0.05f, BobSeconds));
		if (_pivot != null)
		{
			_pivot.Position = new Vector3(0f, lift + bob, 0f);
			if (SpinDegreesPerSecond != 0f)
			{
				_pivot.RotateY(Mathf.DegToRad(SpinDegreesPerSecond) * dt);
			}
		}

		// 拾取：距离轮询（**不碰物理世界**）。
		if (!PickedUp && _age >= PickupDelaySeconds && GetTree() is { } tree)
		{
			foreach (Node node in tree.GetNodesInGroup("player_units"))
			{
				if (node is not Unit unit || !GodotObject.IsInstanceValid(unit) || unit.IsDead)
				{
					continue;
				}

				if (unit.GlobalPosition.DistanceTo(GlobalPosition) <= PickupRadius)
				{
					Collect(unit);
					return;
				}
			}
		}

		if (LifetimeSeconds > 0f && _age >= LifetimeSeconds)
		{
			Expired = true;
			GameLog.Write("Reward", $"灵魂收割：灵魂没人捡，{LifetimeSeconds:0.##}s 后自己消散（位置 {GlobalPosition}）。");
			QueueFree();
		}
	}

	/// <summary>拾取：回血 + 自己消失（本类**唯一**一次战斗层写入就是这一行 <c>Heal</c>）。</summary>
	private void Collect(Unit picker)
	{
		PickedUp = true;
		PickedByName = picker.Name;
		HealedAmount = picker.Heal(HealAmount);
		GameLog.Write(
			"Reward",
			$"灵魂收割：灵魂被 {picker.Name} 拾取（回复 {HealedAmount:0.##} / 配置 {HealAmount:0.##} 点，生命 {picker.Health:0.#}/{picker.MaxHealth:0.#}）");
		QueueFree();
	}

	/// <summary>自发光 + 半透明 + 不受光（与落圈 / 残影同一套写法）。</summary>
	private static StandardMaterial3D Unshaded(Color tint, float energy) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		AlbedoColor = tint,
		EmissionEnabled = true,
		Emission = new Color(tint.R, tint.G, tint.B, 1f),
		EmissionEnergyMultiplier = energy,
	};

	/// <summary>取证量①：子树里 <c>CollisionObject3D</c> 的个数（必须 0 —— 它不是物理体）。</summary>
	public int CountCollisions() => CountInSubtree<CollisionObject3D>();

	/// <summary>取证量②：子树里 <c>Unit</c> 的个数（必须 0 —— 灵魂绝不是一个"单位"）。</summary>
	public int CountUnits() => CountInSubtree<Unit>();

	private int CountInSubtree<T>() where T : class
	{
		int count = 0;
		Count(this, ref count);
		return count;

		static void Count(Node root, ref int total)
		{
			foreach (Node child in root.GetChildren())
			{
				if (child is T)
				{
					total++;
				}

				Count(child, ref total);
			}
		}
	}

	/// <summary>探针用：一行的实际状态（含"绝不进单位列表"的三个取证量）。</summary>
	public string DebugState()
		=> $"灵魂：存活 {_age:0.##}s 已拾取={PickedUp} 拾取者={PickedByName} 回复={HealedAmount:0.##} 超时消散={Expired}"
			+ $"｜数值 拾取半径={PickupRadius:0.##}m 回血={HealAmount:0.##} 存活上限={LifetimeSeconds:0.##}s 延迟={PickupDelaySeconds:0.##}s"
			+ $"｜纯表现 碰撞体={CountCollisions()} 单位={CountUnits()} 分组={GetGroups().Count}（三个都必须是 0）"
			+ "｜它**不是物理体**（没有 Area3D / 没有碰撞层；拾取是每帧的距离轮询）";
}
