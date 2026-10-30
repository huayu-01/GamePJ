using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Rewards;

public partial class DamageBlinkRule : Node, IStoppableRewardRule
{
	public float Threshold { get; set; } = 20;
	public float MinRadius { get; set; } = 3;
	public float MaxRadius { get; set; } = 6;
	public float Cooldown { get; set; } = 2;
	public float AccumulatedDamage { get; private set; }
	public int BlinkCount { get; private set; }
	private Unit? _owner; private float _cooldownRemaining;
	public override void _Ready() { _owner = GetParent<Unit>(); _owner.Damaged += OnDamaged; SetProcess(false); SetPhysicsProcess(false); }
	private void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (_owner is not { IsDead: false, Health: > 0 } || hit.Attacker == null || !_owner.IsHostileTo(hit.Attacker) || result.HealthLost <= 0) return;
		AccumulatedDamage = Mathf.Min(Threshold, AccumulatedDamage + result.HealthLost);
		Vector3 center = _owner.GlobalPosition;
		if (AccumulatedDamage < Threshold || _cooldownRemaining > 0 || MapManager.Instance is not { IsReady: true } map
			|| !map.TryGroundAnchor(_owner.GlobalPosition, out var anchor, _owner)
			|| !map.TryRingPoint(center with { Y = anchor.Y }, MinRadius, MaxRadius, out var point, _owner, anchor)) return;
		Vector3 before = _owner.GlobalPosition; _owner.Velocity = Vector3.Zero; _owner.GlobalPosition = point; _owner.ResetPhysicsInterpolation();
		AccumulatedDamage = 0; _cooldownRemaining = Cooldown; SetPhysicsProcess(Cooldown > 0); BlinkCount++;
		SpecializationFx.Line(_owner.GetParent(), before + Vector3.Up, point + Vector3.Up, new Color(.7f, 1, .9f));
	}
	public override void _PhysicsProcess(double delta) { _cooldownRemaining = Mathf.Max(0, _cooldownRemaining - (float)delta); if (_cooldownRemaining <= 0) SetPhysicsProcess(false); }
	public void Stop() { if (IsInstanceValid(_owner)) _owner!.Damaged -= OnDamaged; _owner = null; SetPhysicsProcess(false); }
	public override void _ExitTree() => Stop();
}
