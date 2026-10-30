using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>玩家实际损失的屏幕反馈，与出手命中反馈分开；背后和衍生伤害也可感知。</summary>
public partial class PlayerHurtFeedback : Node
{
	private Unit _player = null!;
	private CanvasLayer _layer = null!;
	private ColorRect _edge = null!;
	private ShaderMaterial _material = null!;
	private float _remaining, _duration, _cooldown, _weight;
	public int PresentedCount { get; private set; }
	public float ScreenAlpha { get; private set; }
	public static PlayerHurtFeedback Ensure(Unit player)
	{
		if (player.GetNodeOrNull<PlayerHurtFeedback>("HurtFeedback") is { } existing) return existing;
		var feedback = new PlayerHurtFeedback { Name = "HurtFeedback" }; player.AddChild(feedback); return feedback;
	}
	public override void _Ready()
	{
		_player = GetParent<Unit>();
		_layer = new CanvasLayer { Layer = 8 }; AddChild(_layer);
		_material = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Effects/player_hurt_edge.gdshader") };
		_edge = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Material = _material, Visible = false };
		_layer.AddChild(_edge); _edge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
	}
	public bool Show(HitInstance hit, DamageResult result)
	{
		var profile = CombatFeedback.Profile;
		float applied = result.HealthLost + result.ArmorLost + result.ShieldLost;
		if (!profile.Enabled || applied <= 0 || result.Invulnerable || _cooldown > 0 || GetTree().Paused) return false;
		PresentedCount++; _cooldown = Mathf.Max(.08f, profile.PlayerHurtCooldownSeconds);
		_duration = Mathf.Max(.05f, profile.PlayerHurtSeconds); _remaining = _duration;
		_weight = Mathf.Clamp(.65f + applied / Mathf.Max(1, _player.MaxHealth) * 3, .65f, 1.4f);
		ScreenAlpha = Mathf.Clamp(profile.PlayerHurtOpacity * _weight, 0, .3f);
		_material.SetShaderParameter("edge_color", new Color(.82f, .12f, .09f, ScreenAlpha));
		_material.SetShaderParameter("pulse", 1f); _edge.Visible = ScreenAlpha > 0;
		if (profile.CameraStrength <= 0 || _player.GetViewport().GetCamera3D() is not { } camera) return false;
		CameraImpact.Ensure(camera).Kick(hit.Direction, Mathf.Min(1.8f, _weight * profile.PlayerHurtCameraWeight)); return true;
	}
	public override void _PhysicsProcess(double delta) => _cooldown = Mathf.Max(0, _cooldown - (float)delta);
	public override void _Process(double delta)
	{
		if (!CombatFeedback.Profile.Enabled) _remaining = 0;
		_remaining = Mathf.Max(0, _remaining - (float)delta);
		float pulse = _remaining / Mathf.Max(.001f, _duration);
		ScreenAlpha = Mathf.Clamp(CombatFeedback.Profile.PlayerHurtOpacity * _weight, 0, .3f) * pulse * pulse;
		_material.SetShaderParameter("pulse", pulse * pulse); _edge.Visible = _remaining > 0 && ScreenAlpha > 0;
	}
}
