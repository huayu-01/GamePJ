using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>细环和薄光效，按来源挂载、随来源退出；不占攻击预警层。</summary>
public partial class SurvivalAugmentFx : MeshInstance3D
{
	public Unit? Follow { get; set; }
	public Vector3 FollowOffset { get; set; } = Vector3.Up * .1f;
	public float Lifetime { get; set; }
	private float _remaining;
	private StandardMaterial3D _material = null!;
	private float _alpha;
	public override void _Ready() { _remaining = Lifetime; _material = (StandardMaterial3D)MaterialOverride; _alpha = _material.AlbedoColor.A; }
	public override void _Process(double delta)
	{
		if (Follow != null)
		{
			if (!IsInstanceValid(Follow) || Follow.IsDead) { QueueFree(); return; }
			GlobalPosition = Follow.GlobalPosition + FollowOffset;
		}
		if (Lifetime <= 0) return;
		_remaining -= (float)delta;
		_material.AlbedoColor = new Color(_material.AlbedoColor, _alpha * Mathf.Clamp(_remaining / Lifetime, 0, 1));
		if (_remaining <= 0) QueueFree();
	}
	public static SurvivalAugmentFx Ring(Node parent, Vector3 position, float radius, Color color, float seconds = 0, Unit? follow = null)
	{
		var fx = new SurvivalAugmentFx { Mesh = new TorusMesh { InnerRadius = Mathf.Max(.01f, radius - .045f), OuterRadius = radius, Rings = 48, RingSegments = 4 }, Scale = new Vector3(1, .12f, 1), MaterialOverride = Material(color), Lifetime = seconds, Follow = follow, CastShadow = ShadowCastingSetting.Off };
		parent.AddChild(fx); fx.GlobalPosition = position; return fx;
	}
	public static StandardMaterial3D Material(Color color) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = .7f,
	};
}
