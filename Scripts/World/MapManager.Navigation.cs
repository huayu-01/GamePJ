using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.World;

public partial class MapManager
{
	public int NavigationRevision { get; private set; }
	private sealed record NavigationProfile(Rid Map, NavigationRegion3D Region);
	private readonly Dictionary<(float Radius, float Height), NavigationProfile> _navigationProfiles = new();
	private CapsuleShape3D? _clearanceShape;
	private PhysicsShapeQueryParameters3D? _clearanceQuery;
	private Godot.Collections.Array<Rid>? _clearanceExclude;
	private void ClearClearanceQuery()
	{
		_clearanceQuery?.Dispose(); _clearanceQuery = null;
		_clearanceShape?.Dispose(); _clearanceShape = null;
		_clearanceExclude?.Clear(); _clearanceExclude = null;
	}
	public static (float Radius, float Height) BodySize(Unit? unit)
	{
		if (unit == null) return (0.45f, 1.8f);
		Vector3 scale = unit.IsInsideTree() ? unit.GlobalTransform.Basis.Scale.Abs() : unit.Scale.Abs();
		float radius = 0.1f, top = 0.2f;
		for (int i = 0, count = unit.GetChildCount(); i < count; i++)
		{
			Node child = unit.GetChild(i);
			if (child is not CollisionShape3D { Disabled: false, Shape: { } shape } collider) continue;
			(float r, float h) = shape switch
			{
				CapsuleShape3D capsule => (capsule.Radius, capsule.Height),
				SphereShape3D sphere => (sphere.Radius, sphere.Radius * 2),
				CylinderShape3D cylinder => (cylinder.Radius, cylinder.Height),
				BoxShape3D box => (Mathf.Max(box.Size.X, box.Size.Z) * 0.5f, box.Size.Y),
				_ => (0.45f, 1.8f),
			};
			radius = Mathf.Max(radius, (r + new Vector2(collider.Position.X, collider.Position.Z).Length()) * Mathf.Max(scale.X, scale.Z));
			top = Mathf.Max(top, (collider.Position.Y + h * 0.5f) * scale.Y);
		}
		return (radius, Mathf.Max(top, radius * 2));
	}
	private NavigationMesh CreateNavigationMesh(float radius, float height) => new()
	{
		AgentRadius = Mathf.Ceil(radius / CellSize) * CellSize,
		AgentHeight = Mathf.Ceil(height / NavigationServer3D.MapGetCellHeight(GetWorld3D().NavigationMap)) * NavigationServer3D.MapGetCellHeight(GetWorld3D().NavigationMap),
		AgentMaxSlope = AgentMaxSlope, CellSize = CellSize,
		CellHeight = NavigationServer3D.MapGetCellHeight(GetWorld3D().NavigationMap),
		GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
		GeometryCollisionMask = GeometryMask,
		GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.GroupsWithChildren,
		GeometrySourceGroupName = GeometryGroupName,
	};
	private Rid NavigationMapFor(Unit? unit)
	{
		if (!IsReady) return default;
		var size = BodySize(unit);
		if (size.Radius <= AgentRadius && size.Height <= AgentHeight + 0.05f) return _map;
		var key = (Mathf.Ceil(size.Radius * 4) / 4, Mathf.Ceil(size.Height * 4) / 4);
		if (!_navigationProfiles.TryGetValue(key, out var profile))
		{
			Rid map = NavigationServer3D.MapCreate();
			NavigationServer3D.MapSetCellSize(map, CellSize);
			NavigationServer3D.MapSetCellHeight(map, NavigationServer3D.MapGetCellHeight(_map));
			NavigationServer3D.MapSetActive(map, true);
			var region = new NavigationRegion3D { Name = $"NavBody{_navigationProfiles.Count}", NavigationMesh = CreateNavigationMesh(key.Item1, key.Item2) };
			(GetTree().CurrentScene ?? GetTree().Root).AddChild(region);
			region.SetNavigationMap(map); region.BakeNavigationMesh(false);
			profile = new(map, region); _navigationProfiles.Add(key, profile);
		}
		return profile.Region.NavigationMesh!.GetPolygonCount() > 0 && NavigationServer3D.MapGetIterationId(profile.Map) > 0 ? profile.Map : default;
	}
	private void ClearNavigationProfiles()
	{
		foreach (var profile in _navigationProfiles.Values)
		{
			if (IsInstanceValid(profile.Region))
			{
				profile.Region.Enabled = false;
				profile.Region.SetNavigationMap(default);
				// 场景退树时父节点正在移除子节点，只能延迟释放自有区域。
				profile.Region.QueueFree();
			}
			if (profile.Map.IsValid) NavigationServer3D.FreeRid(profile.Map);
		}
		_navigationProfiles.Clear();
	}
	public bool TryPath(Vector3 from, Vector3 destination, out Vector3[] path, Unit? unit = null, float snapTolerance = 1.5f)
	{
		path = Array.Empty<Vector3>(); Rid map = NavigationMapFor(unit);
		if (!map.IsValid || !from.IsFinite() || !destination.IsFinite()) return false;
		Vector3 start = NavigationServer3D.MapGetClosestPoint(map, from), end = NavigationServer3D.MapGetClosestPoint(map, destination);
		if (!NavigationServer3D.MapGetClosestPointOwner(map, from).IsValid || !NavigationServer3D.MapGetClosestPointOwner(map, destination).IsValid
			|| start.DistanceTo(from) > snapTolerance || end.DistanceTo(destination) > snapTolerance) return false;
		path = NavigationServer3D.MapGetPath(map, start, end, true);
		// 最邻近点不代表同一连通区；不完整路径不能冒充可达。
		if (path.Length == 0 || path[^1].DistanceTo(end) > 0.2f) { path = Array.Empty<Vector3>(); return false; }
		return true;
	}
	/// <summary>追击允许终点不可达：保留导航求解器给出的最接近目标的可达路径，不改变严格选点规则。</summary>
	public bool TryApproachPath(Vector3 from, Vector3 destination, out Vector3[] path, Unit? unit = null)
	{
		path = Array.Empty<Vector3>(); Rid map = NavigationMapFor(unit);
		if (!map.IsValid || !from.IsFinite() || !destination.IsFinite()) return false;
		Vector3 start = NavigationServer3D.MapGetClosestPoint(map, from);
		if (start.DistanceTo(from) > 1.5f || !NavigationServer3D.MapGetClosestPointOwner(map, from).IsValid) return false;
		Vector3 end = NavigationServer3D.MapGetClosestPoint(map, destination);
		path = NavigationServer3D.MapGetPath(map, start, end, true);
		return path.Length > 0;
	}
	/// <summary>仅投影导航锚点，供高处玩家周围的刷怪规划使用；不代表落点净空或原位置可达。</summary>
	public bool TryNavigationAnchor(Vector3 position, out Vector3 anchor, Unit? unit = null)
	{
		anchor = default;
		Rid map = NavigationMapFor(unit);
		if (!map.IsValid || !position.IsFinite() || !NavigationServer3D.MapGetClosestPointOwner(map, position).IsValid) return false;
		anchor = NavigationServer3D.MapGetClosestPoint(map, position);
		return anchor.IsFinite();
	}
	/// <summary>查找脚下的导航地面；空中最近点可能偏向远处屋顶，不能直接拿它当局部范围中心。</summary>
	public bool TryGroundAnchor(Vector3 position, out Vector3 anchor, Unit? unit = null, float maxDrop = 64, float horizontalTolerance = 1.5f)
	{
		anchor = default; Rid map = NavigationMapFor(unit);
		if (!map.IsValid || !position.IsFinite() || !float.IsFinite(maxDrop) || maxDrop <= 0 || !float.IsFinite(horizontalTolerance)
			|| horizontalTolerance <= 0 || !NavigationServer3D.MapGetClosestPointOwner(map, position).IsValid) return false;
		for (int i = 0; i <= 32; i++)
		{
			Vector3 point = NavigationServer3D.MapGetClosestPoint(map, position + Vector3.Down * (maxDrop * i / 32));
			float drop = position.Y - point.Y;
			if (drop < -.1f || drop > maxDrop || new Vector2(point.X - position.X, point.Z - position.Z).LengthSquared() > horizontalTolerance * horizontalTolerance) continue;
			anchor = point; return true;
		}
		return false;
	}
	public bool HasBodyClearance(Vector3 ground, Unit? unit = null, bool includeUnits = true)
	{
		using var profile = ActRoguelike.Core.RuntimeFrameProfiler.Measure("BodyClearance");
		var size = BodySize(unit);
		// 查询同步完成且不调用游戏回调；按地图复用参数，每次仍检查实时占用与当前体型。
		_clearanceShape ??= new CapsuleShape3D(); _clearanceExclude ??= new Godot.Collections.Array<Rid>();
		_clearanceQuery ??= new PhysicsShapeQueryParameters3D { Shape = _clearanceShape };
		if (_clearanceShape.Radius != size.Radius) _clearanceShape.Radius = size.Radius;
		float height = Mathf.Max(size.Height, size.Radius * 2);
		if (_clearanceShape.Height != height) _clearanceShape.Height = height;
		_clearanceExclude.Clear(); if (unit != null && unit.IsInsideTree()) _clearanceExclude.Add(unit.GetRid());
		_clearanceQuery.Transform = new Transform3D(Basis.Identity, ground + Vector3.Up * (size.Height * 0.5f + 0.04f));
		_clearanceQuery.CollisionMask = CombatLayers.World | (includeUnits ? CombatLayers.UnitBody : 0);
		_clearanceQuery.Exclude = _clearanceExclude;
		var intersections = GetWorld3D().DirectSpaceState.IntersectShape(_clearanceQuery, 1);
		return intersections.Count == 0;
	}
	public bool TryValidPoint(Vector3 preferred, Vector3 connectedTo, out Vector3 point, Unit? unit = null, float snapTolerance = 1.5f, float maxHeightDifference = 3f)
	{
		point = default; Rid map = NavigationMapFor(unit);
		if (!map.IsValid || !preferred.IsFinite() || !connectedTo.IsFinite()) return false;
		Vector3 ground = NavigationServer3D.MapGetClosestPoint(map, preferred);
		if (ground.DistanceTo(preferred) > snapTolerance || Mathf.Abs(ground.Y - connectedTo.Y) > maxHeightDifference
			|| !TryPath(connectedTo, ground, out _, unit, Mathf.Max(snapTolerance, 1.5f)) || !HasBodyClearance(ground, unit)) return false;
		point = ground + Vector3.Up * 0.05f; return true;
	}
	public bool TryRingPoint(Vector3 center, float minRadius, float maxRadius, out Vector3 point, Unit? unit = null, Vector3? connectedTo = null, int attempts = 48)
	{
		point = default;
		if (!IsReady || minRadius < 0 || maxRadius < minRadius) return false;
		float angle = (float)GD.RandRange(0d, Mathf.Tau);
		for (int i = 0; i < attempts; i++)
		{
			float radius = Mathf.Lerp(minRadius, maxRadius, (i % 3) / 2f);
			float phase = angle + Mathf.Tau * i / Mathf.Max(1, attempts / 3);
			Vector3 candidate = center + new Vector3(Mathf.Cos(phase) * radius, 0, Mathf.Sin(phase) * radius);
			if (!TryValidPoint(candidate, connectedTo ?? center, out var valid, unit)) continue;
			float distance = new Vector2(valid.X - center.X, valid.Z - center.Z).Length();
			if (distance + 0.05f < minRadius || distance - 0.05f > maxRadius) continue;
			point = valid; return true;
		}
		return false;
	}
}
