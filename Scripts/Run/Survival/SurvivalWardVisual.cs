using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Run;

/// <summary>复用现有菲涅尔着色器形成薄护罩，身体贴图与其他表现不变。</summary>
public partial class SurvivalWardVisual : MeshInstance3D
{
	public Unit Player { get; init; } = null!;
	public object ShieldSource { get; init; } = null!;
	public bool RequiresShield { get; init; } = true;
	public Color RimColor { get; init; } = new(.25f,.9f,1,.7f);
	public float Strength { get; init; } = .8f;
	private ShaderMaterial _material = null!;
	public override void _Ready()
	{
		Mesh = new SphereMesh { Radius=.7f, Height=2, RadialSegments=24, Rings=12 }; CastShadow=ShadowCastingSetting.Off;
		_material = new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/charge_rim_placeholder.gdshader") };
		_material.SetShaderParameter("rim_color",RimColor); _material.SetShaderParameter("rim_strength",Strength); MaterialOverride=_material;
	}
	public override void _Process(double delta)
	{
		if (!IsInstanceValid(Player) || Player.IsDead || (RequiresShield && Player.TemporaryShieldFrom(ShieldSource)<=0)) { QueueFree(); return; }
		if (RequiresShield) _material.SetShaderParameter("rim_strength",Strength*Mathf.Clamp(Player.TemporaryShieldSecondsFrom(ShieldSource)/.35f,0,1));
		GlobalPosition=Player.GlobalPosition+Vector3.Up*.9f;
	}
}
