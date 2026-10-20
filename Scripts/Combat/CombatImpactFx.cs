using Godot;
using ActRoguelike.Core;

namespace ActRoguelike.Combat;

/// <summary>防御材质专用的短促碎片／冲击环；挂世界根，不继承精英缩放，不含判定。</summary>
public partial class CombatImpactFx : Node3D
{
	private float _age, _duration;
	private readonly System.Collections.Generic.List<(Node3D Mesh, Vector3 Direction)> _shards = new();
	private MeshInstance3D _ring = null!;
	private StandardMaterial3D _material = null!;
	public ImpactKind Kind { get; private set; }
	public static void Spawn(Node context, Vector3 point, Vector3 direction, ImpactKind kind, float weight)
	{
		var fx = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/CombatImpactFx.tscn").Instantiate<CombatImpactFx>();
		(context.GetTree().CurrentScene ?? context.GetTree().Root).AddChild(fx);
		fx.GlobalPosition = point; fx.Configure(direction, kind, weight);
	}
	private void Configure(Vector3 direction, ImpactKind kind, float weight)
	{
		Kind = kind; _duration = Mathf.Max(.03f, CombatFeedback.Profile.BurstSeconds);
		_material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = CombatFeedback.ColorFor(kind), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			NoDepthTest = false, EmissionEnabled = true, Emission = CombatFeedback.ColorFor(kind), EmissionEnergyMultiplier = 1.4f };
		Vector3 normal = direction.LengthSquared() > .001f ? direction.Normalized() : Vector3.Forward;
		Vector3 up = Mathf.Abs(normal.Dot(Vector3.Up)) > .95f ? Vector3.Right : Vector3.Up;
		Basis basis = Basis.LookingAt(normal, up);
		int count = kind == ImpactKind.ArmorBreak ? 10 : 6;
		for (int i = 0; i < count; i++)
		{
			float angle = i * Mathf.Tau / count;
			Vector3 outward = basis * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.3f);
			var shard = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(.035f, .035f, .18f * weight), Material = _material },
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			AddChild(shard); shard.LookAt(GlobalPosition + outward, Mathf.Abs(outward.Normalized().Dot(up)) > .95f ? Vector3.Forward : up);
			_shards.Add((shard, outward * (.6f + weight * .3f)));
		}
		_ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .16f * weight, OuterRadius = .19f * weight, Rings = 12, RingSegments = 20, Material = _material },
			Basis = basis * new Basis(Vector3.Right, Mathf.Pi * .5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		AddChild(_ring);
	}
	public override void _Process(double delta)
	{
		if (_material == null) return;
		_age += (float)delta; float t = Mathf.Clamp(_age / _duration, 0, 1);
		foreach (var shard in _shards) { shard.Mesh.Position = shard.Direction * t; shard.Mesh.Scale = Vector3.One * Mathf.Max(.01f, 1 - t); }
		_ring.Scale = Vector3.One * (1 + 2 * t);
		Color color = _material.AlbedoColor; color.A = 1 - t; _material.AlbedoColor = color;
		if (t >= 1) QueueFree();
	}
}
