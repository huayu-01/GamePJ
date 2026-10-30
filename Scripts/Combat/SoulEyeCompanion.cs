using System.Linq;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>只负责标记的独立留场物；不叠已有标记，不继承旧软启动后撤或镜头缩放。</summary>
public partial class SoulEyeCompanion : Node3D
{
	public Unit OwnerUnit { get; set; } = null!;
	public float Lifetime { get; set; } = 10;
	public float Interval { get; set; } = .2f;
	public float MarkDuration { get; set; } = 6;
	public float Radius { get; set; } = 12;
	public string AbilityId { get; set; } = "";
	public int MarkCount { get; private set; }
	private float _age, _timer;
	public override void _Ready()
	{
		TopLevel = true; SetProcess(false);
		// 双面眼形方便自由镜头辨识，不依赖角色骨骼或额外模型资产。
		AddPart(new Vector3(.32f, .16f, .1f), Vector3.Zero, new Color(.85f, .78f, 1));
		foreach (float side in new[] { -1f, 1f })
		{
			AddPart(new Vector3(.11f, .12f, .025f), new Vector3(0, 0, side * .095f), new Color(.8f, .08f, .2f));
			AddPart(new Vector3(.035f, .09f, .012f), new Vector3(0, 0, side * .12f), new Color(.05f, .02f, .1f));
		}
		GlobalPosition = OwnerUnit.GlobalPosition + Vector3.Up * 2.3f;
	}
	private void AddPart(Vector3 scale, Vector3 position, Color color) => AddChild(new MeshInstance3D
	{
		Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 20, Rings = 10 }, Scale = scale, Position = position,
		MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
	});
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta; _age += dt;
		if (!IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead || _age >= Lifetime) { QueueFree(); return; }
		GlobalPosition = OwnerUnit.GlobalPosition + Vector3.Up * (2.3f + Mathf.Sin(_age * 3) * .08f); _timer -= dt; if (_timer > 0) return;
		_timer = Mathf.Max(.05f, Interval);
		var candidates = GetTree().GetNodesInGroup("enemy_units").OfType<Unit>()
			.Where(u => u.IsValidTarget && OwnerUnit.IsHostileTo(u) && u.GetStatusStacks("Mark") == 0 && u.GlobalPosition.DistanceSquaredTo(OwnerUnit.GlobalPosition) <= Radius * Radius)
			.OrderBy(u => u.GlobalPosition.DistanceSquaredTo(OwnerUnit.GlobalPosition));
		foreach (var target in candidates)
		{
			using var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, Targeting.AimPoint(target), CombatLayers.World);
			using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query); if (hit.Count > 0) continue;
			if (StatusPresets.Create("Mark", MarkDuration) is { } mark) { target.ApplyStatus(mark, OwnerUnit, AbilityId); MarkCount++; }
			LookAt(Targeting.AimPoint(target)); SpecializationFx.Line(GetParent(), GlobalPosition, Targeting.AimPoint(target), new Color(1, .08f, .15f)); break;
		}
	}
}
