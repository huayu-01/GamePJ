using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>瘦长灵剑的蓄势亮核和短促速度尾迹；只读运动采样，不产生伤害或驱动飞行。</summary>
public partial class SpiritSwordFx : Node3D
{
	private readonly List<Vector3> _points = new();
	private readonly List<float> _ages = new();
	private readonly ImmediateMesh _trail = new();
	private MeshInstance3D _geometry = null!;
	private Node3D _sword = null!;
	private StandardMaterial3D _blade = null!;
	private readonly StandardMaterial3D _trailMaterial = new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		NoDepthTest = false, AlbedoColor = Colors.White,
		EmissionEnabled = true, Emission = new Color(.3f, .8f, 1f), EmissionEnergyMultiplier = 2f,
	};
	private float _flash, _windup;
	public bool TrailVisible => _geometry?.Visible == true;
	public override void _Ready()
	{
		_sword = GetParent<Node3D>();
		var blade = _sword.GetNode<MeshInstance3D>("Blade");
		_blade = (StandardMaterial3D)blade.Mesh!.SurfaceGetMaterial(0).Duplicate();
		blade.MaterialOverride = _blade;
		_geometry = new MeshInstance3D { Name = "VelocityTrail", Mesh = _trail, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		AddChild(_geometry); _geometry.GlobalTransform = Transform3D.Identity;
		_geometry.Visible = false;
	}
	public void Windup(float amount)
	{
		_windup = amount;
	}
	public void Launch()
	{
		_points.Clear(); _ages.Clear(); _flash = .055f; _windup = 0;
		_points.Add(_sword.GlobalPosition - _sword.GlobalBasis.Y * .65f); _ages.Add(.06f);
		Sample(_sword.GlobalPosition);
	}
	public void Sample(Vector3 position)
	{
		if (_points.Count > 0 && _points[^1].DistanceSquaredTo(position) < .001f) return;
		_points.Add(position); _ages.Add(0);
		if (_points.Count > 12) { _points.RemoveAt(0); _ages.RemoveAt(0); }
	}
	public void Impact() => _flash = .07f;
	public void Return() => _windup = 0;
	public override void _Process(double delta)
	{
		float dt = (float)delta; _flash = Mathf.Max(0, _flash - dt);
		float flash = _flash > 0 ? 1f : _windup;
		_blade.AlbedoColor = new Color(.35f, .85f, 1f).Lerp(Colors.White, flash);
		_blade.EmissionEnergyMultiplier = 1.5f + flash * 2.5f;
		for (int i = _ages.Count - 1; i >= 0; i--)
		{
			_ages[i] += dt;
			if (_ages[i] > .13f) { _ages.RemoveAt(i); _points.RemoveAt(i); }
		}
		_trail.ClearSurfaces(); _geometry.Visible = _points.Count >= 2;
		if (!_geometry.Visible) return;
		Camera3D? camera = GetViewport().GetCamera3D();
		Vector3 cameraPosition = camera?.GlobalPosition ?? _sword.GlobalPosition + Vector3.Back * 10f;
		_trail.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _trailMaterial);
		for (int i = 0; i < _points.Count; i++)
		{
			Vector3 along = i == 0 ? _points[1] - _points[0] : _points[i] - _points[i - 1];
			Vector3 side = along.Cross(cameraPosition - _points[i]);
			side = side.LengthSquared() > .0001f ? side.Normalized() : _sword.GlobalBasis.X;
			float life = Mathf.Clamp(1 - _ages[i] / .13f, 0, 1);
			float width = .018f + .075f * life;
			_trail.SurfaceSetColor(new Color(.4f, .85f, 1f, life * .85f));
			_trail.SurfaceAddVertex(_points[i] - side * width);
			_trail.SurfaceSetColor(new Color(.4f, .85f, 1f, life * .85f));
			_trail.SurfaceAddVertex(_points[i] + side * width);
		}
		_trail.SurfaceEnd();
	}
}
