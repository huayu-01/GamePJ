using ActRoguelike.AI;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>撤退：血量低于阈值时远离目标。优先级通常设得很高。</summary>
public partial class RetreatBehavior : Behavior
{
	[Export(PropertyHint.Range, "0,1,0.05")] public float HealthThreshold { get; set; } = 0.3f;
	[Export] public float SpeedScale { get; set; } = 1.1f;
	[Export] public float KeepDistance { get; set; } = 12f;

	public override bool CanRun()
	{
		if (!base.CanRun() || OwnerUnit.MaxHealth <= 0f)
		{
			return false;
		}

		return OwnerUnit.Health / OwnerUnit.MaxHealth <= HealthThreshold;
	}

	public override void Tick(float delta)
	{
		if (Target == null || Movement == null)
		{
			return;
		}

		Vector3 direction = HorizontalDirectionTo(Target);
		Movement.FaceDirection(-direction, delta);

		float distance = HorizontalDistanceTo(Target);
		if (distance < KeepDistance) OwnerUnit.GetNodeOrNull<UnitNavigation>("Navigation")?.KeepRange(Target.GlobalPosition, KeepDistance, OwnerUnit.MoveSpeed * SpeedScale);
		else Movement.Stop();
	}
}
