using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.UI;

/// <summary>跟随世界锚点的短命跳字；上飘只改变局部偏移，不冻结镜头投影。</summary>
public partial class DamageNumberLabel : Label
{
	private Unit? _target;
	private Vector3 _world;
	private Vector2 _fallback;
	private float _age, _duration, _rise, _slot;
	private bool _worldAnchor;
	public void Configure(Unit? target, Vector2 screen, float slot, float rise, float lifetime)
	{
		_target = target; _fallback = screen; _slot = slot; _rise = rise; _duration = Mathf.Max(.001f, lifetime);
		_worldAnchor = IsInstanceValid(target);
		if (_worldAnchor) _world = target!.GlobalPosition + Vector3.Up * 1.5f;
		MouseFilter = MouseFilterEnum.Ignore; ProcessPriority = 100;
		UpdatePosition();
	}
	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (_age >= _duration) { QueueFree(); return; }
		UpdatePosition();
		var color = Modulate; color.A = 1 - _age / _duration; Modulate = color;
	}
	private void UpdatePosition()
	{
		if (IsInstanceValid(_target) && _target!.IsInsideTree()) _world = _target.GlobalPosition + Vector3.Up * 1.5f;
		else _target = null;
		Vector2 anchor = _fallback;
		if (_worldAnchor)
		{
			var camera = GetViewport().GetCamera3D();
			Visible = camera != null && !camera.IsPositionBehind(_world);
			if (!Visible) return;
			anchor = camera!.UnprojectPosition(_world);
		}
		Position = anchor + new Vector2(0, _slot - _rise * Mathf.Clamp(_age / _duration, 0, 1));
	}
}
