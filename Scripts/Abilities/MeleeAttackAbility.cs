using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 近战攻击：前摇 → 判定窗口（激活 AttackHitbox）→ 后摇。
/// 命中体是本能力的子节点，形状与伤害在预制体里配置。
/// </summary>
public partial class MeleeAttackAbility : Ability
{
	[Export] public float AttackRange { get; set; } = 2.3f;
	[Export] public float ActiveTime { get; set; } = 0.12f;
	[Export] public float DamageScale { get; set; } = 1f;

	/// <summary>命中体实际够得到的距离。行为用它判断“现在打得到”，避免停在够不着的距离空挥。</summary>
	public float HitReach => _hitbox?.Reach ?? AttackRange;

	private AttackHitbox _hitbox = null!;
	private int _stage;

	protected override void OnReady()
	{
		_hitbox = GetNode<AttackHitbox>("Attack");
	}

	protected override bool CanActivate(Unit? target)
	{
		return target != null && IsInRange(target);
	}

	private bool IsInRange(Unit target)
	{
		if (OwnerUnit == null)
		{
			return false;
		}

		Vector3 offset = target.GlobalPosition - OwnerUnit.GlobalPosition;
		offset.Y = 0f;
		return offset.Length() <= Mathf.Min(AttackRange, HitReach);
	}

	protected override void OnActivate(Unit? target)
	{
		_stage = 0;
		_hitbox.Deactivate();
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		switch (_stage)
		{
			case 0:
				if (StateTime >= Windup)
				{
					var attack = new AttackInstance
					{
						Attacker = OwnerUnit,
						Ability = AbilityId,
						BaseDamage = _hitbox.Damage * DamageScale,
						CritChance = _hitbox.CritChance,
						CritMultiplier = _hitbox.CritMultiplier,
						Knockback = _hitbox.Knockback,
						Policy = _hitbox.Policy,
						HitInterval = _hitbox.HitInterval,
						Tags = _hitbox.AttackTags,
					};
					_hitbox.Activate(OwnerUnit, attack);
					_stage = 1;
					StateTime = 0f;
				}

				break;

			case 1:
				if (StateTime >= ActiveTime)
				{
					_hitbox.Deactivate();
					_stage = 2;
					StateTime = 0f;
				}

				break;

			case 2:
				if (StateTime >= Recover)
				{
					Finish();
				}

				break;
		}
	}
}
