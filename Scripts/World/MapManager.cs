using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.World;

/// <summary>
/// 地图管理器（用户要求：所有跟地图相关的都归它管）。
///
/// 职责：
/// 1. **导航网格**：运行时烘焙 + 重烘（为后续程序化地图留接口）；烘焙会按
///    AgentRadius / AgentHeight / AgentMaxSlope 自动排除爬不上去的坡与不可行走区域。
/// 2. **点位供给**：刷怪点、交互节点预设点、降临点 —— 全都从这里取，
///    保证落在玩家能站、能交互的地方（不会卡进建筑、不会掉出地图）。
///
/// 其它系统只依赖本类的这几个方法，不直接碰 NavigationServer，
/// 以后换成程序化地图只要重烘一次、其它代码不用改。
/// </summary>
public partial class MapManager : Node3D
{
	public static MapManager? Instance { get; private set; }

	[Export] public float AgentRadius { get; set; } = 0.6f;
	[Export] public float AgentHeight { get; set; } = 1.8f;
	[Export] public float AgentMaxSlope { get; set; } = 30f;
	/// <summary>烘焙网格的水平精度必须与World3D导航地图一致。</summary>
	public float CellSize => IsInsideTree() ? NavigationServer3D.MapGetCellSize(GetWorld3D().NavigationMap) : 0.25f;

	/// <summary>参与烘焙的节点组名：把地面 / 建筑挂进这个组（默认关卡根节点上）。</summary>
	[Export] public string GeometryGroupName { get; set; } = "nav_geometry";

	/// <summary>参与烘焙的物理层（默认只吃 World 层）。</summary>
	[Export] public uint GeometryMask { get; set; } = CombatLayers.World;

	/// <summary>烘焙延迟：等关卡几何体都进入场景树。</summary>
	[Export] public float BakeDelaySeconds { get; set; } = 0.3f;

	private NavigationRegion3D? _region;
	private Rid _map;
	private bool _baked;
	private int _polygonCount;
	private SceneTreeTimer? _bakeTimer;
	private bool _ownsRegion;

	public override void _Ready()
	{
		Instance = this;
		_bakeTimer = GetTree().CreateTimer(BakeDelaySeconds, processAlways: true);
		_bakeTimer.Timeout += Bake;
	}

	public override void _ExitTree()
	{
		if (_bakeTimer != null && IsInstanceValid(_bakeTimer)) _bakeTimer.Timeout -= Bake;
		_bakeTimer = null;
		if (_ownsRegion && IsInstanceValid(_region)) _region!.QueueFree();
		_region = null;
		_baked = false;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	/// <summary>是否已经有可用的导航网格。</summary>
	public bool IsReady => _baked && _map.IsValid && _polygonCount > 0 && NavigationServer3D.MapGetIterationId(_map) > 0;

	/// <summary>烘焙出的多边形数量（诊断用）。</summary>
	public int PolygonCount => _polygonCount;

	/// <summary>
	/// 重新烘焙（程序化地图生成之后调用）。
	/// 注意：region 必须挂在**场景根**下 —— RootNodeChildren 模式是以 region 的父节点为扫描根，
	/// 挂在子节点里会扫到空场景（这个坑踩过：多边形数一直是 0）。
	/// </summary>
	public void Rebuild()
	{
		if (_ownsRegion && IsInstanceValid(_region)) _region!.Free();
		_region = null;
		_ownsRegion = false;
		_baked = false;
		Bake();
	}

	private void Bake()
	{
		if (!IsInsideTree()) return;
		if (_bakeTimer != null && IsInstanceValid(_bakeTimer)) _bakeTimer.Timeout -= Bake;
		_bakeTimer = null;
		Node root = GetTree().CurrentScene ?? GetTree().Root;
		EnsureKillZone();

		// 优先复用场景里**已经烘焙好**的 NavigationRegion3D（在编辑器里烘的结果会存进场景）。
		foreach (Node node in root.FindChildren("*", "", true, false))
		{
			if (node is NavigationRegion3D existing && existing.NavigationMesh != null)
			{
				_polygonCount = existing.NavigationMesh.GetPolygonCount();
				if (_polygonCount > 0)
				{
					_region = existing;
					_ownsRegion = false;
					_map = existing.GetNavigationMap();
					_baked = true;
					ActRoguelike.Core.GameLog.Write("Map", $"复用场景里已烘焙的导航网格：{_polygonCount} 个多边形");
					return;
				}
			}
		}


		float cellHeight = NavigationServer3D.MapGetCellHeight(GetWorld3D().NavigationMap);
		var mesh = new NavigationMesh
		{
			// Recast本来就会上取整；显式量化到地图体素，避免隐式精度告警。
			AgentRadius = Mathf.Ceil(AgentRadius / CellSize) * CellSize,
			AgentHeight = Mathf.Ceil(AgentHeight / cellHeight) * cellHeight,
			AgentMaxSlope = AgentMaxSlope,
			CellSize = CellSize,
			CellHeight = cellHeight,
			GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
			GeometryCollisionMask = GeometryMask,
			// 组标记的是场景根，必须递归收集子节点中的地形碰撞；Explicit只会解析根本身。
			GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.GroupsWithChildren,
			GeometrySourceGroupName = GeometryGroupName,
		};

		var region = new NavigationRegion3D { Name = "NavRegion" };
		region.NavigationMesh = mesh;
		root.AddChild(region);

		// 同步烘焙（onThread: true 是异步的，同一帧读多边形数只会是 0）。
		region.BakeNavigationMesh(false);
		_region = region;
		_ownsRegion = true;
		_map = region.GetNavigationMap();
		_polygonCount = mesh.GetPolygonCount();
		_baked = true;

		ActRoguelike.Core.GameLog.Write("Map", $"导航网格烘焙完成：{_polygonCount} 个多边形（半径 {AgentRadius} / 高度 {AgentHeight} / 最大坡 {AgentMaxSlope}°）");
		if (_polygonCount == 0)
		{
			GD.PushWarning("[MapManager] 导航网格为空：检查地面是否是 World 层（collision_layer 含 1）的 StaticBody3D。");
		}
	}

	/// <summary>
	/// 区域杀（用户要求：碰撞产生，进入才杀）。整张地图下方铺一层薄区域，
	/// 单位落进去立即死亡 —— 比"每帧遍历单位组查 Y"省得多，且对所有单位生效。
	/// </summary>
	private void EnsureKillZone()
	{
		if (GetTree().CurrentScene?.GetNodeOrNull<KillZone>("KillZone") != null)
		{
			return;
		}

		var zone = new KillZone
		{
			Name = "KillZone",
			CenterY = ActRoguelike.Config.ConfigIndex.Settings.KillPlaneY - 30f,
		};
		(GetTree().CurrentScene ?? GetTree().Root).AddChild(zone);
	}

	// ───────────────────────── 查询 ─────────────────────────

	/// <summary>把任意位置吸附到最近的可行走点。</summary>
	public bool TrySnap(Vector3 position, out Vector3 walkable)
	{
		walkable = position;
		if (!IsReady)
		{
			return false;
		}

		// 原点也可能是合法导航点；用所属区域判断是否查到，不能把零向量当失败。
		if (!NavigationServer3D.MapGetClosestPointOwner(_map, position).IsValid)
		{
			return false;
		}
		walkable = NavigationServer3D.MapGetClosestPoint(_map, position);
		return true;
	}

	/// <summary>判断一个位置是否在可行走区域内（容差内能找到导航点）。</summary>
	public bool IsWalkable(Vector3 position, float tolerance = 1.2f)
		=> TrySnap(position, out Vector3 walkable) && walkable.DistanceTo(position) <= tolerance;

	// ───────────────────── 点位供给（各系统统一入口）─────────────────────

	/// <summary>
	/// 在中心附近找一圈内的可行走点。刷怪点、降临点都用它。
	/// 导航网格可用时用它吸附；**不可用时退回"随机环点 + 地面射线 + 球形避障"**
	/// （<see cref="FallbackRingPoint"/>）；正常场景使用递归地形烘焙结果。
	/// </summary>
	public Vector3 RingPoint(Vector3 center, float minRadius, float maxRadius, int rings = 3, int samples = 12)
	{
		if (IsReady)
		{
			float startAngle = (float)GD.RandRange(0d, Mathf.Tau);
			for (int ring = 0; ring < rings; ring++)
			{
				float radius = rings == 1 ? minRadius : Mathf.Lerp(minRadius, maxRadius, ring / (float)(rings - 1));
				for (int i = 0; i < samples; i++)
				{
					float angle = startAngle + Mathf.Tau * i / samples;
					var probe = new Vector3(
						center.X + (Mathf.Cos(angle) * radius),
						center.Y + 1.5f,
						center.Z + (Mathf.Sin(angle) * radius));

					if (TrySnap(probe, out Vector3 walkable) && Mathf.Abs(walkable.Y - center.Y) < 6f
						&& new Vector2(walkable.X - center.X, walkable.Z - center.Z).Length() >= minRadius * 0.8f)
					{
						return walkable + (Vector3.Up * 0.3f);
					}
				}
			}

			return center + (Vector3.Up * 0.3f);
		}

		return FallbackRingPoint(center, minRadius, maxRadius);
	}

	/// <summary>
	/// 导航网格不可用时的兜底落点：随机方向 + 环上的随机半径 → 向下打一条地面射线 → 球形避障
	/// （与 `NodePlacement.PlaceSafely` 同一套探测参数，**不是第二套落点规则**）。
	///
	/// ⚠️ 以前这里是 `return center + Up * 0.3` —— 那等于"刷怪点 = 玩家脚下"，
	/// 用户需求 #6「场景里会定期在任意有效位置刷出怪物」里的"任意有效位置"就名存实亡
	/// （所有敌人、白板首领、降临物都贴在玩家身上出现）。采样全部失败时才退回原点。
	/// </summary>
	private Vector3 FallbackRingPoint(Vector3 center, float minRadius, float maxRadius, int attempts = 16)
	{
		PhysicsDirectSpaceState3D? space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
		{
			return center + (Vector3.Up * 0.3f);
		}

		for (int attempt = 0; attempt < attempts; attempt++)
		{
			float angle = (float)GD.RandRange(0d, Mathf.Tau);
			float radius = (float)GD.RandRange(minRadius, Mathf.Max(minRadius, maxRadius));
			var probe = new Vector3(
				center.X + (Mathf.Cos(angle) * radius),
				center.Y + 2f,
				center.Z + (Mathf.Sin(angle) * radius));

			// ① 找脚下地面（World 层 = 1）；够不着就换一个方向。
			var ray = PhysicsRayQueryParameters3D.Create(probe, probe + (Vector3.Down * 12f), CombatLayers.World);
			Godot.Collections.Dictionary hit = space.IntersectRay(ray);
			if (hit.Count == 0)
			{
				continue;
			}

			Vector3 ground = (Vector3)hit["position"];
			if (Mathf.Abs(ground.Y - center.Y) > 6f)
			{
				continue;
			}

			// ② 球形避障：不要落在墙里 / 斜坡里（球心要抬到半径之上，否则永远与脚下地面重叠）。
			var shape = new PhysicsShapeQueryParameters3D
			{
				Shape = new SphereShape3D { Radius = 1.3f },
				Transform = new Transform3D(Basis.Identity, ground + (Vector3.Up * 1.65f)),
				CollisionMask = CombatLayers.World,
			};

			if (space.IntersectShape(shape, 1).Count == 0)
			{
				return ground + (Vector3.Up * 0.3f);
			}
		}

		return center + (Vector3.Up * 0.3f);
	}

	/// <summary>刷怪点：玩家周围一圈里的可行走位置。</summary>
	public Vector3 SpawnPoint(Vector3 playerPosition, float minRadius, float maxRadius)
		=> RingPoint(playerPosition, minRadius, maxRadius);

	/// <summary>降临点（首领死亡后的传送节点）：贴玩家附近，保证走得到。</summary>
	public Vector3 DropPoint(Vector3 playerPosition)
		=> RingPoint(playerPosition, 2.2f, 3.4f, rings: 3, samples: 10);

	/// <summary>
	/// 交互节点的落点：优先吸附到可行走点，找不到就退回原来的探测结果。
	/// </summary>
	public Vector3 NodeAnchor(Vector3 preferred)
		=> TrySnap(preferred, out Vector3 walkable) ? walkable + (Vector3.Up * 0.25f) : preferred;
}
