using Godot;

namespace ActRoguelike.Rewards;

/// <summary>特化机制的轻量闪电示意，不参与碰撞和伤害判定。</summary>
public static class SpecializationFx
{
	public static void Line(Node parent, Vector3 from, Vector3 to, Color color)
	{
		Vector3 travel = to - from;
		if (travel.LengthSquared() < 0.0001f || DisplayServer.GetName() == "headless") return;
		var mesh = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = travel.Length() }, MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
		parent.AddChild(mesh);
		mesh.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up, travel.Normalized())), (from + to) * 0.5f);
		mesh.GetTree().CreateTimer(0.12f, false).Timeout += () => { if (GodotObject.IsInstanceValid(mesh)) mesh.QueueFree(); };
	}
}
