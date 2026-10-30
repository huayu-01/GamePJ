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
	/// <summary>只累计真实打开命中窗口的次数，前摇取消不算执行过攻击。</summary>
	public int ExecutedAttackCount { get; private set; }

	private AttackHitbox _hitbox = null!;
	private Transform3D _restHitbox;
	private int _stage;
	protected override void OnFinishOrCancel()
	{
		if (_hitbox == null) return;
		_hitbox.Deactivate(); _hitbox.Transform = _restHitbox;
	}

	protected override void OnReady()
	{
		_hitbox = GetNode<AttackHitbox>("Attack");
		_restHitbox = _hitbox.Transform;
	}

	protected override bool CanActivate(Unit? target)
	{
		return target != null && IsInRange(target);
	}

	public bool IsInRange(Unit target)
	{
		if (OwnerUnit == null)
		{
			return false;
		}

		Vector3 origin = Projectile.GetTargetCenter(OwnerUnit), center = Projectile.GetTargetCenter(target);
		float radius = ActRoguelike.World.MapManager.BodySize(target).Radius;
		if (origin.DistanceTo(center) > Mathf.Min(AttackRange, HitReach) + radius) return false;
		using var query = PhysicsRayQueryParameters3D.Create(origin, center, CombatLayers.World);
		query.HitFromInside = true;
		using var obstruction = OwnerUnit.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return obstruction.Count == 0;
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
					// 玩家站在头顶时朝真实身体中心上挑；只旋转本次命中体，结束后恢复原姿态。
					if (Target != null && OwnerUnit != null)
					{
						Vector3 pivot = OwnerUnit.ToLocal(Projectile.GetTargetCenter(OwnerUnit));
						Vector3 aim = OwnerUnit.GlobalBasis.Inverse() * (Projectile.GetTargetCenter(Target) - Projectile.GetTargetCenter(OwnerUnit));
						float pitch = Mathf.Atan2(aim.Y, new Vector2(aim.X, aim.Z).Length());
						Basis tilt = new(Vector3.Right, pitch);
						_hitbox.Transform = new Transform3D(tilt * _restHitbox.Basis, pivot + tilt * (_restHitbox.Origin - pivot));
					}
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
					ExecutedAttackCount++;
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
				if (StateTime >= Recover / ActRoguelike.Combat.Elites.EliteProjectileRules.RecoveryScale(OwnerUnit))
				{
					Finish();
				}

				break;
		}
	}
}
