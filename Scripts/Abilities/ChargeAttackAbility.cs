using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 冲锋：前摇锁定方向 → 直线高速突进（命中体全程开启）→ 命中目标或超过距离后停下 → 后摇。
/// 移动同样走 CharacterMovement 的位移接口，不自己写物理。
/// </summary>
public partial class ChargeAttackAbility : Ability
{
	[Export] public float ChargeSpeed { get; set; } = 16f;
	[Export] public float MaxDistance { get; set; } = 14f;

	private AttackHitbox _hitbox = null!;
	private Vector3 _direction;
	private Vector3 _startPosition;
	private int _stage;
	private bool _hitSomething;

	protected override void OnReady()
	{
		_hitbox = GetNode<AttackHitbox>("Attack");
		_hitbox.HitLanded += _ => _hitSomething = true;
	}

	protected override bool CanActivate(Unit? target)
	{
		if (target == null || OwnerUnit == null)
		{
			return false;
		}

		float distance = OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		float max = ApproachMaxRange > 0f ? ApproachMaxRange : 14f;
		return distance >= ApproachMinRange && distance <= max;
	}

	protected override void OnActivate(Unit? target)
	{
		_stage = 0;
		_hitSomething = false;
		_hitbox.Deactivate();

		if (OwnerUnit == null || target == null)
		{
			Cancel();
			return;
		}

		_direction = target.GlobalPosition - OwnerUnit.GlobalPosition;
		_direction.Y = 0f;
		_direction = _direction.LengthSquared() < 0.0001f ? -OwnerUnit.GlobalTransform.Basis.Z : _direction.Normalized();
		_startPosition = OwnerUnit.GlobalPosition;
		Movement?.FaceDirection(_direction, 1f);
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
					Movement?.BeginDash(_direction, ChargeSpeed);
					_hitbox.Activate(OwnerUnit);
					_stage = 1;
					StateTime = 0f;
				}

				break;

			case 1:
				bool traveled = OwnerUnit != null && OwnerUnit.GlobalPosition.DistanceTo(_startPosition) >= MaxDistance;
				if (_hitSomething || traveled || StateTime > 2f)
				{
					Movement?.EndDash();
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
