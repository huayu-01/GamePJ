using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 准星（屏幕中心射线）工具。技能/武器要「朝准星方向」时统一走这里，避免各写一份。
/// </summary>
public static class AimUtil
{
	/// <summary>仅在准星射线真实命中指定物理层时返回目标；不把远端空点当作地面。</summary>
	public static bool TryGetCrosshairPoint(Viewport viewport, Node3D owner, float maxDistance, uint mask, out Vector3 point)
	{
		point = Vector3.Zero;
		Camera3D? camera = viewport.GetCamera3D();
		if (camera == null) return false;
		Vector2 center = viewport.GetVisibleRect().Size * 0.5f;
		Vector3 origin = camera.ProjectRayOrigin(center);
		var query = PhysicsRayQueryParameters3D.Create(origin, origin + camera.ProjectRayNormal(center) * maxDistance, mask);
		if (owner is CollisionObject3D body) query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		var hit = owner.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return false;
		point = hit["position"].AsVector3();
		return true;
	}
	/// <summary>屏幕中心射线方向；没有相机时返回 fallback。</summary>
	public static Vector3 GetCrosshairDirection(Viewport viewport, Vector3 fallback)
	{
		Camera3D? camera = viewport.GetCamera3D();
		if (camera == null)
		{
			return fallback;
		}

		Vector2 center = viewport.GetVisibleRect().Size * 0.5f;
		return camera.ProjectRayNormal(center);
	}

	/// <summary>准星指向的世界点：射线打到东西就用命中点，否则取最大距离处。</summary>
	public static Vector3 GetCrosshairPoint(Viewport viewport, Node3D owner, float maxDistance, uint mask)
	{
		Camera3D? camera = viewport.GetCamera3D();
		if (camera == null)
		{
			return owner.GlobalPosition + (-owner.GlobalTransform.Basis.Z * maxDistance);
		}

		Vector2 center = viewport.GetVisibleRect().Size * 0.5f;
		Vector3 origin = camera.ProjectRayOrigin(center);
		Vector3 end = origin + (camera.ProjectRayNormal(center) * maxDistance);
		var query = PhysicsRayQueryParameters3D.Create(origin, end, mask);
		if (owner is CollisionObject3D body)
		{
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		}

		Godot.Collections.Dictionary hit = owner.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector3() : end;
	}
}
