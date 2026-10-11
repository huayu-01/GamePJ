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
	[Export] public float CellSize { get; set; } = 0.3f;

	/// <summary>参与烘焙的节点组名：把地面 / 建筑挂进这个组（默认关卡根节点上）。</summary>
	[Export] public string GeometryGroupName { get; set; } = "nav_geometry";

	/// <summary>参与烘焙的物理层（默认只吃 World 层）。</summary>
	[Export] public uint GeometryMask { get; set; } = 1;

	/// <summary>烘焙延迟：等关卡几何体都进入场景树。</summary>
	[Export] public float BakeDelaySeconds { get; set; } = 0.3f;

	private NavigationRegion3D? _region;
	private Rid _map;
	private bool _baked;
	private int _polygonCount;

	public override void _Ready()
	{
		Instance = this;
		GetTree().CreateTimer(BakeDelaySeconds, processAlways: true).Timeout += Bake;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	/// <summary>是否已经有可用的导航网格。</summary>
	public bool IsReady => _baked && _map.IsValid && _polygonCount > 0;

	/// <summary>烘焙出的多边形数量（诊断用）。</summary>
	public int PolygonCount => _polygonCount;

	/// <summary>
	/// 重新烘焙（程序化地图生成之后调用）。
	/// 注意：region 必须挂在**场景根**下 —— RootNodeChildren 模式是以 region 的父节点为扫描根，
	/// 挂在子节点里会扫到空场景（这个坑踩过：多边形数一直是 0）。
	/// </summary>
	public void Rebuild()
	{
		_region?.QueueFree();
		_region = null;
		_baked = false;
		Bake();
	}

	private void Bake()
	{
		Node root = GetTree().CurrentScene ?? GetTree().Root;

		// 优先复用场景里**已经烘焙好**的 NavigationRegion3D（在编辑器里烘的结果会存进场景）。
		foreach (Node node in root.FindChildren("*", "", true, false))
		{
			if (node is NavigationRegion3D existing && existing.NavigationMesh != null)
			{
				_polygonCount = existing.NavigationMesh.GetPolygonCount();
				if (_polygonCount > 0)
				{
					_region = existing;
					_map = existing.GetNavigationMap();
					_baked = true;
					ActRoguelike.Core.GameLog.Write("Map", $"复用场景里已烘焙的导航网格：{_polygonCount} 个多边形");
					return;
				}
			}
		}


		var mesh = new NavigationMesh
		{
			AgentRadius = AgentRadius,
			AgentHeight = AgentHeight,
			AgentMaxSlope = AgentMaxSlope,
			CellSize = CellSize,
			CellHeight = CellSize * 0.5f,
			GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
			GeometryCollisionMask = GeometryMask,
			// 用「按组收集」而不是 RootNodeChildren：后者以 region 父节点为扫描根，实测扫不到实例化的关卡几何体。
			GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.GroupsExplicit,
			GeometrySourceGroupName = GeometryGroupName,
		};

		var region = new NavigationRegion3D { Name = "NavRegion" };
		region.NavigationMesh = mesh;
		root.AddChild(region);

		// 同步烘焙（onThread: true 是异步的，同一帧读多边形数只会是 0）。
		region.BakeNavigationMesh(false);
		_region = region;
		_map = region.GetNavigationMap();
		_polygonCount = mesh.GetPolygonCount();
		_baked = true;

		EnsureKillZone();

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
			CenterY = ActRoguelike.Config.Balance.Settings.KillPlaneY - 30f,
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

		Vector3 closest = NavigationServer3D.MapGetClosestPoint(_map, position);
		if (closest.IsZeroApprox())
		{
			return false;
		}

		walkable = closest;
		return true;
	}

	/// <summary>判断一个位置是否在可行走区域内（容差内能找到导航点）。</summary>
	public bool IsWalkable(Vector3 position, float tolerance = 1.2f)
		=> TrySnap(position, out Vector3 walkable) && walkable.DistanceTo(position) <= tolerance;

	// ───────────────────── 点位供给（各系统统一入口）─────────────────────

	/// <summary>
	/// 在中心附近找一圈内的可行走点。刷怪点、降临点都用它。
	/// 导航网格不可用时退回"中心点"（调用方仍能跑，只是不保证避开建筑）。
	/// </summary>
	public Vector3 RingPoint(Vector3 center, float minRadius, float maxRadius, int rings = 3, int samples = 12)
	{
		if (IsReady)
		{
			for (int ring = 0; ring < rings; ring++)
			{
				float radius = rings == 1 ? minRadius : Mathf.Lerp(minRadius, maxRadius, ring / (float)(rings - 1));
				for (int i = 0; i < samples; i++)
				{
					float angle = Mathf.Tau * i / samples;
					var probe = new Vector3(
						center.X + (Mathf.Cos(angle) * radius),
						center.Y + 1.5f,
						center.Z + (Mathf.Sin(angle) * radius));

					if (TrySnap(probe, out Vector3 walkable) && Mathf.Abs(walkable.Y - center.Y) < 6f)
					{
						return walkable + (Vector3.Up * 0.3f);
					}
				}
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
