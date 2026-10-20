using Godot;

namespace ActRoguelike.Combat;

/// <summary>实际接触点的小亮核可越过目标模型，但先检查World遮挡，不穿墙显示。外围火花保留正常深度。</summary>
public partial class ImpactVisibility : Node3D
{
	private StandardMaterial3D _material = null!;
	private float _age, _duration;
	public static bool IsWorldVisible(Node3D context, Camera3D camera, Vector3 point)
	{
		if (camera.IsPositionBehind(point)) return false;
		using var query = PhysicsRayQueryParameters3D.Create(camera.GetCameraTransform().Origin, point, CombatLayers.World);
		using var hit = context.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0;
	}
	public static ImpactVisibility? Spawn(Unit target, Vector3 point, ImpactKind kind)
	{
		var p = CombatFeedback.Profile;
		if (p.ContactCoreSeconds <= 0 || p.ContactCoreSize <= 0 || target.GetViewport().GetCamera3D() is not { } camera
			|| !IsWorldVisible(target, camera, point)) return null;
		var effect = new ImpactVisibility(); (target.GetTree().CurrentScene ?? target.GetTree().Root).AddChild(effect);
		effect.TopLevel = true; effect.GlobalPosition = point; effect._duration = p.ContactCoreSeconds;
		effect._material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 127,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = CombatFeedback.ColorFor(kind).Lerp(Colors.White, .75f) };
		var mesh = new ImmediateMesh(); mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, effect._material);
		for (int i = 0; i < 8; i++)
		{
			float angle = i * Mathf.Pi / 4, next = (i + 1) * Mathf.Pi / 4;
			float radius = i % 2 == 0 ? .5f : .12f, nextRadius = i % 2 == 0 ? .12f : .5f;
			mesh.SurfaceAddVertex(Vector3.Zero);
			mesh.SurfaceAddVertex(new Vector3(Mathf.Cos(next), Mathf.Sin(next), 0) * nextRadius * p.ContactCoreSize);
			mesh.SurfaceAddVertex(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * p.ContactCoreSize);
		}
		mesh.SurfaceEnd(); effect.AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		return effect;
	}
	public override void _Process(double delta)
	{
		_age += (float)delta; float t = Mathf.Clamp(_age / Mathf.Max(.001f, _duration), 0, 1);
		Color color = _material.AlbedoColor; color.A = 1 - t; _material.AlbedoColor = color;
		if (t >= 1) QueueFree();
	}
}
