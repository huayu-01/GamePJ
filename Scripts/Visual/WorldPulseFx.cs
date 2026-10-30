using Godot;

namespace ActRoguelike.Visual;

/// <summary>短暂的世界光波；只有渲染节点，不创建碰撞、伤害或单位分组。</summary>
public partial class WorldPulseFx : Node3D
{
	private static readonly Shader WaveShader = new()
	{
		Code = """
		shader_type spatial;
		render_mode unshaded, cull_disabled, depth_draw_never;
		uniform vec4 wave_color : source_color = vec4(1.0, 0.15, 0.12, 0.8);
		uniform float progress = 0.0;
		void fragment() {
			float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 2.0);
			float band = pow(max(0.0, 1.0 - abs(UV.y - progress) * 9.0), 2.0);
			ALBEDO = wave_color.rgb;
			EMISSION = wave_color.rgb * (0.6 + rim + band * 0.8);
			ALPHA = wave_color.a * (0.08 + rim * 0.5 + band * 0.6) * (1.0 - progress);
		}
		""",
	};
	private static readonly SphereMesh DomeMesh = new() { Radius = 1, Height = 2, IsHemisphere = true, RadialSegments = 40, Rings = 16 };
	private static readonly TorusMesh RingMesh = new() { InnerRadius = .94f, OuterRadius = 1f, Rings = 40, RingSegments = 6 };
	public Color Tint { get; set; } = new(1, .15f, .12f, .85f);
	public float Radius { get; set; } = 2.6f;
	public float Duration { get; set; } = .55f;
	public bool DrawDome { get; set; } = true;
	public float Progress { get; private set; }
	private ShaderMaterial _material = null!;
	private float _elapsed;
	public static WorldPulseFx Play(Node parent, Vector3 worldPosition, Color color, float radius, float duration, bool dome = true)
	{
		var effect = new WorldPulseFx { Name = "WorldPulse", TopLevel = true, Tint = color, Radius = radius, Duration = duration, DrawDome = dome };
		parent.AddChild(effect);
		effect.GlobalPosition = worldPosition + Vector3.Up * .07f;
		return effect;
	}
	public override void _Ready()
	{
		_material = new ShaderMaterial { Shader = WaveShader };
		_material.SetShaderParameter("wave_color", Tint);
		AddChild(new MeshInstance3D { Name = "Dome", Mesh = DomeMesh, MaterialOverride = _material, Visible = DrawDome, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		AddChild(new MeshInstance3D { Name = "Ring", Mesh = RingMesh, Scale = new Vector3(1, .2f, 1), MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		Scale = new Vector3(.25f, .08f, .25f) * Mathf.Max(.1f, Radius);
	}
	public override void _Process(double delta)
	{
		_elapsed += (float)delta;
		Progress = Mathf.Clamp(_elapsed / Mathf.Max(.1f, Duration), 0, 1);
		float rise = 1 - Mathf.Pow(1 - Mathf.Clamp(Progress * 3, 0, 1), 3);
		Scale = new Vector3(.25f + .75f * rise, .08f + .92f * rise, .25f + .75f * rise) * Mathf.Max(.1f, Radius);
		_material.SetShaderParameter("progress", Progress);
		if (Progress >= 1) QueueFree();
	}
}
