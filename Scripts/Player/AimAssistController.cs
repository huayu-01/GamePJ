using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Player;

/// <summary>屏幕准星附近的可见敌人优先；不转动相机、不改变弹体追踪或穿墙规则。</summary>
public partial class AimAssistController : Node
{
	[Export] public float RadiusAt1080 { get; set; } = 140;
	[Export] public float MaxDistance { get; set; } = 60;
	[Export] public float RefreshSeconds { get; set; } = .08f;
	public bool Enabled { get; private set; }
	public Unit? Selected { get; private set; }
	private Unit _owner = null!;
	private float _timer;
	private readonly List<(Unit Unit, float Score)> _candidates = new();
	public override void _Ready() { _owner = GetParent<Unit>(); SetPhysicsProcess(false); }
	public void SetEnabled(bool value)
	{
		Enabled = value; Selected = null; _timer = 0; SetPhysicsProcess(value);
		if (value) RefreshTarget();
	}
	public override void _Input(InputEvent input)
	{
		if (GetTree().Paused || UiRouter.Instance?.AnyOpen == true || _owner.IsDead) return;
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: true })
		{ SetEnabled(!Enabled); GetViewport().SetInputAsHandled(); }
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner.IsDead) { SetEnabled(false); return; }
		_timer -= (float)delta; if (_timer > 0) return;
		_timer = Mathf.Max(.02f, RefreshSeconds); RefreshTarget();
	}
	public bool TryGetAim(float maxDistance, out Vector3 point)
	{
		point = Vector3.Zero;
		if (!Enabled || GetTree().Paused || !IsInstanceValid(Selected) || Selected is not { IsValidTarget: true } target
			|| !_owner.IsHostileTo(target) || _owner.GlobalPosition.DistanceTo(target.GlobalPosition) > Mathf.Min(maxDistance, MaxDistance)) return false;
		point = Targeting.AimPoint(target);
		Camera3D? camera = GetViewport().GetCamera3D(); if (camera == null || camera.IsPositionBehind(point)) return false;
		Vector2 size = GetViewport().GetVisibleRect().Size;
		if (camera.UnprojectPosition(point).DistanceTo(size * .5f) > RadiusAt1080 * size.Y / 1080) return false;
		return Visible(target, point);
	}
	public void RefreshTarget()
	{
		Camera3D? camera = GetViewport().GetCamera3D();
		if (!Enabled || camera == null) { Selected = null; return; }
		Vector2 screen = GetViewport().GetVisibleRect().Size, center = screen * .5f;
		float radius = RadiusAt1080 * screen.Y / 1080;
		_candidates.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit target || !target.IsValidTarget || !_owner.IsHostileTo(target)
				|| _owner.GlobalPosition.DistanceSquaredTo(target.GlobalPosition) > MaxDistance * MaxDistance) continue;
			Vector3 point = Targeting.AimPoint(target);
			if (camera.IsPositionBehind(point)) continue;
			Vector2 projected = camera.UnprojectPosition(point);
			if (!new Rect2(Vector2.Zero, screen).HasPoint(projected)) continue;
			float score = projected.DistanceSquaredTo(center);
			if (score <= radius * radius) _candidates.Add((target, score * (target == Selected ? .8f : 1)));
		}
		_candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
		Selected = null;
		foreach (var candidate in _candidates)
			if (Visible(candidate.Unit, Targeting.AimPoint(candidate.Unit))) { Selected = candidate.Unit; break; }
	}
	private bool Visible(Unit target, Vector3 point)
	{
		var camera = GetViewport().GetCamera3D(); if (camera == null) return false;
		// 相机和出手位置均不能隔着地形选中；单位身体不会遮挡其它单位的选中资格。
		using var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, CombatLayers.World);
		using var cameraHit = _owner.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (cameraHit.Count > 0) return false;
		query.From = Targeting.AimPoint(_owner); query.To = point;
		using var shotHit = _owner.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return shotHit.Count == 0;
	}
}
