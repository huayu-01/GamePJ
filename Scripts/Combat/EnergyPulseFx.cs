using Godot;

namespace ActRoguelike.Combat;

/// <summary>紧凑的空间波外壳；脱离0.12秒判定体后独立播放，不写入任何战斗状态。</summary>
public partial class EnergyPulseFx : Node3D
{
	[Export] public float DurationSeconds { get; set; } = .48f;
	[Export] public float RadiusScale { get; set; } = .9f;
	private ShaderMaterial _material = null!;
	private Vector3 _fullScale;
	private float _age;
	private bool _detached;
	public override void _Ready()
	{
		var shell = GetNode<MeshInstance3D>("Shell");
		_material = (ShaderMaterial)shell.MaterialOverride.Duplicate(); shell.MaterialOverride = _material;
		Callable.From(Detach).CallDeferred();
	}
	private void Detach()
	{
		Node root = GetTree().CurrentScene ?? GetTree().Root;
		Transform3D world = GlobalTransform;
		if (GetParent() != root) Reparent(root, false);
		GlobalTransform = world; ResetPhysicsInterpolation();
		_fullScale = Scale * RadiusScale; Scale = _fullScale * .18f; _detached = true;
	}
	public override void _Process(double delta)
	{
		if (!_detached) return;
		_age += (float)delta;
		float phase = Mathf.Clamp(_age / Mathf.Max(.01f, DurationSeconds), 0, 1);
		Scale = _fullScale * Mathf.Lerp(.18f, 1, 1 - Mathf.Pow(1 - phase, 3));
		_material.SetShaderParameter("phase", phase);
		if (phase >= 1) QueueFree();
	}
}
