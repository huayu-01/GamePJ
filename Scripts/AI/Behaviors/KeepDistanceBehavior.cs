using ActRoguelike.AI;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>保持距离：远了靠近、近了后退，在舒适圈内站定（可选绕圈）。远程单位用。</summary>
public partial class KeepDistanceBehavior : Behavior
{
	[Export] public float PreferredDistance { get; set; } = 8f;
	[Export] public float Tolerance { get; set; } = 1.5f;
	[Export] public float SpeedScale { get; set; } = 1f;
	[Export] public bool Strafe { get; set; }
	[Export] public float StrafeScale { get; set; } = 0.6f;

	public override void Tick(float delta)
	{
		if (Target == null || Movement == null)
		{
			return;
		}

		Vector3 direction = HorizontalDirectionTo(Target);
		Movement.FaceDirection(direction, delta);

		float distance = HorizontalDistanceTo(Target);
		float speed = OwnerUnit.MoveSpeed * SpeedScale;

		if (distance > PreferredDistance + Tolerance)
		{
			Movement.SetIntent(AvoidObstacles(direction), speed);
		}
		else if (distance < Mathf.Max(1.5f, PreferredDistance - Tolerance))
		{
			Movement.SetIntent(-direction, speed);
		}
		else if (Strafe)
		{
			Movement.SetIntent(direction.Cross(Vector3.Up).Normalized(), speed * StrafeScale);
		}
		else
		{
			Movement.SetIntent(Vector3.Zero, 0f);
		}
	}
}
