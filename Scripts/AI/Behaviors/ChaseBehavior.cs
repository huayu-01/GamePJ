using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>追向目标；到达 StopDistance 内停下（交给攻击行为处理）。</summary>
public partial class ChaseBehavior : Behavior
{
	[Export] public float StopDistance { get; set; } = 1.5f;
	[Export] public float SpeedScale { get; set; } = 1f;

	public override void Tick(float delta)
	{
		if (Target == null || Movement == null)
		{
			return;
		}

		Vector3 toTarget = HorizontalDirectionTo(Target);
		Movement.FaceDirection(toTarget, delta);

		float distance = HorizontalDistanceTo(Target);
		if (distance > StopDistance) OwnerUnit.GetNodeOrNull<UnitNavigation>("Navigation")?.MoveTo(Target.GlobalPosition, OwnerUnit.MoveSpeed * SpeedScale);
		else Movement.Stop();
	}
}
