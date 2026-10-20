using Godot;

namespace ActRoguelike.Combat;

/// <summary>仅偏移相机投影；不写CameraRig朝向、跟随位置、FOV或单位变换。</summary>
public partial class CameraImpact : Node
{
	private Camera3D _camera = null!;
	private float _remaining, _amplitude, _homeH, _homeV;
	private Vector2 _axis;
	public Vector2 Offset { get; private set; }
	/// <summary>投影抖动只改变显示；所有准星求交共读去掉本组件偏移后的射线原点。</summary>
	public static Vector3 AimOrigin(Camera3D camera, Vector2 screen)
	{
		Vector3 origin = camera.ProjectRayOrigin(screen);
		if (camera.GetNodeOrNull<CameraImpact>("ImpactFeedback") is { } effect)
			origin -= camera.GlobalBasis.X * effect.Offset.X + camera.GlobalBasis.Y * effect.Offset.Y;
		return origin;
	}
	public static CameraImpact Ensure(Camera3D camera)
	{
		if (camera.GetNodeOrNull<CameraImpact>("ImpactFeedback") is { } existing) return existing;
		var effect = new CameraImpact { Name = "ImpactFeedback" }; camera.AddChild(effect); return effect;
	}
	public override void _Ready() { _camera = GetParent<Camera3D>(); _homeH = _camera.HOffset; _homeV = _camera.VOffset; }
	public void Kick(Vector3 direction, float weight)
	{
		var p = CombatFeedback.Profile;
		_remaining = Mathf.Max(0, p.CameraSeconds);
		_amplitude = Mathf.Clamp(.025f * weight * p.CameraStrength, 0, Mathf.Max(0, p.CameraMaxOffset));
		_axis = new Vector2(direction.Dot(_camera.GlobalBasis.X), direction.Dot(_camera.GlobalBasis.Y));
		_axis = _axis.LengthSquared() < .01f ? new Vector2(.35f, .94f) : _axis.Normalized();
	}
	public override void _Process(double delta)
	{
		var p = CombatFeedback.Profile;
		if (!p.Enabled || p.CameraStrength <= 0) _remaining = 0;
		_remaining = Mathf.Max(0, _remaining - (float)delta);
		float phase = _remaining / Mathf.Max(.001f, p.CameraSeconds);
		Offset = _axis * (_amplitude * phase * phase * Mathf.Sin((1 - phase) * Mathf.Pi * 3));
		_camera.HOffset = _homeH + Offset.X; _camera.VOffset = _homeV + Offset.Y;
	}
	public override void _ExitTree()
	{
		if (IsInstanceValid(_camera)) { _camera.HOffset = _homeH; _camera.VOffset = _homeV; }
	}
}
