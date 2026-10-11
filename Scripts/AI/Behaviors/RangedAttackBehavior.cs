using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>远程攻击：目标在能力射程内且能力就绪时发动。</summary>
public partial class RangedAttackBehavior : Behavior
{
	private ProjectileAttackAbility? _ability;

	protected override void OnReady() => _ability = NodeSearch.FindInSubtree<ProjectileAttackAbility>(OwnerUnit);

	public override bool CanRun()
	{
		if (_ability is not { IsReady: true } || Target == null || !base.CanRun())
		{
			return false;
		}

		float distance = HorizontalDistanceTo(Target);
		return distance >= _ability.MinRange && distance <= _ability.MaxRange;
	}

	public override void Tick(float delta)
	{
		if (Target == null || _ability == null)
		{
			return;
		}

		Movement?.FaceDirection(HorizontalDirectionTo(Target), delta);
		Movement?.SetIntent(Vector3.Zero, 0f);
		_ability.TryActivate(Target);
	}
}
