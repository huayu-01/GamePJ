using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>追向目标；到达 StopDistance 内停下（交给攻击行为处理）。</summary>
public partial class ChaseBehavior : Behavior
{
	[Export] public float StopDistance { get; set; } = 1.5f;
	[Export] public float SpeedScale { get; set; } = 1f;
	private ActRoguelike.Abilities.MeleeAttackAbility? _melee;
	protected override void OnReady() => _melee = NodeSearch.FindInSubtree<ActRoguelike.Abilities.MeleeAttackAbility>(OwnerUnit);

	public override void Tick(float delta)
	{
		if (Target == null || Movement == null)
		{
			return;
		}

		Vector3 toTarget = HorizontalDirectionTo(Target);
		Movement.FaceDirection(toTarget, delta);

		float distance = HorizontalDistanceTo(Target);
		if (distance > StopDistance || _melee != null && !_melee.IsInRange(Target)) OwnerUnit.GetNodeOrNull<UnitNavigation>("Navigation")?.MoveTo(Target.GlobalPosition, OwnerUnit.MoveSpeed * SpeedScale);
		else Movement.Stop();
	}
}
