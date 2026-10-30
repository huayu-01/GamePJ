using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>本局碎片拾取物；只通知导演入账，不直接写局外存档。</summary>
public partial class RunShardPickup : Node3D
{
	public Unit? Player { get; set; }
	public int Amount { get; set; }
	[Export] public float PickupRadius { get; set; } = 1.4f;
	[Export] public float PickupDelaySeconds { get; set; } = .2f;
	public event Action<RunShardPickup>? Collected;
	private bool _claimed;
	private float _age;
	public override void _Ready()
	{
		SetProcess(false);
		if (GetNodeOrNull<Label3D>("Amount") is { } label) label.Text = Amount.ToString();
	}
	public override void _PhysicsProcess(double delta)
	{
		_age += (float)delta;
		if (_claimed || _age < PickupDelaySeconds || !IsInstanceValid(Player) || Player!.IsDead) return;
		Vector3 deltaPosition = Player.GlobalPosition - GlobalPosition;
		if (Mathf.Abs(deltaPosition.Y) > 2 || new Vector2(deltaPosition.X, deltaPosition.Z).LengthSquared() > PickupRadius * PickupRadius) return;
		_claimed = true; Collected?.Invoke(this); Collected = null; QueueFree();
	}
}
