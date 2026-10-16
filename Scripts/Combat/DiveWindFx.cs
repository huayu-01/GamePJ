using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>只读下落攻击状态，以逆速度方向的细长光带表现加速。不复制单位，不创建物理体或分组。</summary>
public partial class DiveWindFx : Node3D
{
	[ExportGroup("下落风线 · 几何")]
	[Export] public int StreakCount { get; set; } = 10;
	[Export] public float Radius { get; set; } = 0.72f;
	[Export] public float BodyHeight { get; set; } = 0.75f;
	[Export] public float TravelDistance { get; set; } = 2.2f;
	[Export] public float StreakLength { get; set; } = 1.65f;
	[Export] public float StreakWidth { get; set; } = 0.045f;
	[Export] public float ScrollSpeed { get; set; } = 12f;
	[Export] public float TwistRadians { get; set; } = 0.32f;
	[ExportGroup("下落风线 · 配色与时长")]
	[Export] public Color CoreColor { get; set; } = new(0.12f, 0.72f, 0.95f, 0.9f);
	[Export] public Color HaloColor { get; set; } = new(0.025f, 0.2f, 0.34f, 0.28f);
	[Export] public float HaloWidthScale { get; set; } = 3.5f;
	[Export] public float FadeInSeconds { get; set; } = 0.035f;
	[Export] public float FadeOutSeconds { get; set; } = 0.09f;
	[Export] public float EmissionEnergy { get; set; } = 0.35f;

	public bool SourceActive => _ability is { Enabled: true, IsDiving: true } && _unit is { IsDead: false };
	public int SourceFrames { get; private set; }
	public float Intensity { get; private set; }

	private FallAttackAbility? _ability;
	private Unit? _unit;
	private MeshInstance3D _meshNode = null!;
	private readonly ImmediateMesh _mesh = new();
	private StandardMaterial3D _material = null!;
	private float _time;
	private float _previewSeconds;

	public override void _Ready()
	{
		_ability = GetParentOrNull<FallAttackAbility>();
		_unit = _ability?.GetParentOrNull<Unit>();
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			VertexColorUseAsAlbedo = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			EmissionEnabled = true,
			Emission = CoreColor,
			EmissionEnergyMultiplier = EmissionEnergy,
		};
		_meshNode = new MeshInstance3D
		{
			Name = "WindRibbons",
			Mesh = _mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(_meshNode);
		Visible = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		bool active = SourceActive;
		if (active)
		{
			SourceFrames++;
		}
		bool draw = active || _previewSeconds > 0f;
		_previewSeconds = Mathf.Max(0f, _previewSeconds - dt);
		Intensity = Mathf.MoveToward(Intensity, draw ? 1f : 0f, dt / Mathf.Max(0.001f, draw ? FadeInSeconds : FadeOutSeconds));
		Visible = Intensity > 0.001f;
		_mesh.ClearSurfaces();
		if (!Visible)
		{
			return;
		}
		_time += dt;
		DrawWind();
	}

	private void DrawWind()
	{
		Vector3 velocity = _unit?.Velocity ?? Vector3.Down;
		Vector3 axis = SourceActive && velocity.LengthSquared() > 0.01f
			? GlobalBasis.Inverse() * -velocity.Normalized() : Vector3.Up;
		Vector3 across = axis.Cross(Vector3.Forward).Normalized();
		if (across.LengthSquared() < 0.01f)
		{
			across = Vector3.Right;
		}
		Vector3 around = axis.Cross(across).Normalized();
		Vector3 camera = ToLocal(GetViewport().GetCamera3D()?.GlobalPosition ?? GlobalPosition + Vector3.Back);
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
		int count = Mathf.Clamp(StreakCount, 1, 32);
		for (int i = 0; i < count; i++)
		{
			float phase = Mathf.PosMod(_time * ScrollSpeed / Mathf.Max(0.01f, TravelDistance) + i / (float)count, 1f);
			float angle = Mathf.Tau * i / count + phase * TwistRadians;
			Vector3 radial = across * Mathf.Cos(angle) + around * Mathf.Sin(angle);
			Vector3 start = Vector3.Up * BodyHeight + radial * Radius + axis * (phase * TravelDistance);
			Vector3 end = start + axis * (StreakLength * (0.7f + 0.3f * (i % 3) / 2f));
			Vector3 side = (end - start).Cross(camera - (start + end) * 0.5f).Normalized();
			float alpha = Mathf.Sin(phase * Mathf.Pi) * Intensity;
			Ribbon(start, end, side, StreakWidth * HaloWidthScale, HaloColor, alpha);
			Ribbon(start, end, side, StreakWidth, CoreColor, alpha);
		}
		_mesh.SurfaceEnd();
	}

	private void Ribbon(Vector3 start, Vector3 end, Vector3 side, float width, Color color, float alpha)
	{
		Vector3 middle = start.Lerp(end, 0.35f);
		_mesh.SurfaceSetColor(new Color(color.R, color.G, color.B, 0f));
		_mesh.SurfaceAddVertex(start);
		_mesh.SurfaceSetColor(new Color(color.R, color.G, color.B, color.A * alpha));
		_mesh.SurfaceAddVertex(middle - side * width);
		_mesh.SurfaceAddVertex(middle + side * width);
		_mesh.SurfaceAddVertex(middle - side * width);
		_mesh.SurfaceSetColor(new Color(color.R, color.G, color.B, 0f));
		_mesh.SurfaceAddVertex(end);
		_mesh.SurfaceSetColor(new Color(color.R, color.G, color.B, color.A * alpha));
		_mesh.SurfaceAddVertex(middle + side * width);
	}

	/// <summary>仅预演画法；不触发技能，不改变速度、伤害、位置或碰撞。</summary>
	public void DebugPreview(float seconds) => _previewSeconds = Mathf.Max(0f, seconds);
	public void DebugClearPreview() => _previewSeconds = 0f;
	public string DebugState()
		=> $"下落风线：只读源={SourceActive} 真实下落帧={SourceFrames} 强度={Intensity:0.##} 可见={Visible}"
			+ $" 速度={_unit?.Velocity} 预演剩余={_previewSeconds:0.##}s 光带={StreakCount}"
			+ $"｜碰撞体={FindChildren("*", "CollisionObject3D", true, false).Count} 单位={FindChildren("*", "Unit", true, false).Count} 分组={GetGroups().Count}";
}
