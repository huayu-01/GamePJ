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
	public static (float Radius, float Height) BodySize(Unit? unit)
	{
		if (unit == null) return (0.45f, 1.8f);
		Vector3 scale = unit.IsInsideTree() ? unit.GlobalTransform.Basis.Scale.Abs() : unit.Scale.Abs();
		float radius = 0.1f, top = 0.2f;
		foreach (Node child in unit.GetChildren())
		{
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
	public bool HasBodyClearance(Vector3 ground, Unit? unit = null, bool includeUnits = true)
	{
		var size = BodySize(unit);
		using var capsule = new CapsuleShape3D { Radius = size.Radius, Height = size.Height };
		using var query = new PhysicsShapeQueryParameters3D
		{
			Shape = capsule, Transform = new Transform3D(Basis.Identity, ground + Vector3.Up * (size.Height * 0.5f + 0.04f)),
			CollisionMask = CombatLayers.World | (includeUnits ? CombatLayers.UnitBody : 0),
			Exclude = unit != null && unit.IsInsideTree() ? new Godot.Collections.Array<Rid> { unit.GetRid() } : new Godot.Collections.Array<Rid>(),
		};
		return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
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
