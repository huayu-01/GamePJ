using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>近战攻击：目标进入能力射程且能力就绪时发动。攻击过程由 Ability 自己推进。</summary>
public partial class MeleeAttackBehavior : Behavior
{
	private MeleeAttackAbility? _ability;

	protected override void OnReady() => _ability = NodeSearch.FindInSubtree<MeleeAttackAbility>(OwnerUnit);

	public override bool CanRun()
	{
		return _ability is { IsReady: true }
			&& base.CanRun()
			&& Target != null
			&& HorizontalDistanceTo(Target) <= Mathf.Min(_ability.AttackRange, _ability.HitReach);
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
