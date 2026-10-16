using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>复用真实单位木桩；死亡后重建，练习不发放奖励或写进度。</summary>
public partial class PracticeTarget : Node3D
{
	[Export] public PackedScene TargetScene { get; set; } = null!;
	[Export] public float RespawnSeconds { get; set; } = 2;
	public Unit? Target { get; private set; }
	public int HitCount { get; private set; }
	public int RespawnCount { get; private set; }
	private double _wait;
	public override void _Ready() => Spawn();
	private void Spawn()
	{
		Target = TargetScene.Instantiate<Unit>();
		// 在单位进树前补组件，避免Presentation的Ready里添加兄弟节点。
		UnitAnimator.Ensure(Target);
		Target.Damaged += OnDamaged;
		AddChild(Target);
		RespawnCount++;
	}
	private void OnDamaged(DamageResult result, HitInstance hit) { if (result.FinalDamage > 0) HitCount++; }
	public override void _Process(double delta)
	{
		if (Target != null && IsInstanceValid(Target) && !Target.IsDead) { _wait = 0; return; }
		_wait += delta;
		if (_wait < RespawnSeconds) return;
		if (Target != null && IsInstanceValid(Target)) { Target.Damaged -= OnDamaged; Target.QueueFree(); }
		Spawn();
		_wait = 0;
	}
	public override void _ExitTree() { if (Target != null && IsInstanceValid(Target)) Target.Damaged -= OnDamaged; }
}
