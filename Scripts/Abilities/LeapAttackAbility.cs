using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 跳跃近战：朝目标起跳，落地瞬间判定一次。
/// 移动交给 CharacterMovement（水平冲刺 + 垂直初速），命中体是本能力的子节点。
/// </summary>
public partial class LeapAttackAbility : Ability
{
	[Export] public float JumpVelocity { get; set; } = 9f;
	[Export] public float HorizontalSpeed { get; set; } = 8f;
	[Export] public float ActiveTime { get; set; } = 0.2f;
	[Export] public float MaxAirTime { get; set; } = 3f;

	private AttackHitbox _hitbox = null!;
	private int _stage;
	private bool _leftGround;
	private EnemyAttackWarning? _warning;
	private Vector3 _jumpDirection;
	private float _warningRefresh;
	protected override void OnFinishOrCancel() { _warning?.HideAll(); _hitbox?.Deactivate(); Movement?.EndDash(); }

	protected override void OnReady()
	{
		_hitbox = GetNode<AttackHitbox>("Attack");
		_warning = EnemyAttackWarning.Create(this, OwnerUnit);
	}

	protected override bool CanActivate(Unit? target)
	{
		if (target == null || OwnerUnit == null)
		{
			return false;
		}

		float distance = OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		float max = ApproachMaxRange > 0f ? ApproachMaxRange : 8f;
		return distance >= ApproachMinRange && distance <= max;
	}

	protected override void OnActivate(Unit? target)
	{
		_stage = 0;
		_leftGround = false;
		_hitbox.Deactivate();

		if (Movement == null || OwnerUnit == null || target == null)
		{
			Cancel();
			return;
		}

		Vector3 direction = target.GlobalPosition - OwnerUnit.GlobalPosition;
		direction.Y = 0f;
		Movement.FaceDirection(direction, 1f);
		Movement.BeginDash(direction, HorizontalSpeed);
		Movement.BeginJump(JumpVelocity);
		_jumpDirection = direction.Normalized(); _warningRefresh = 0; UpdateWarning();
	}
	private void UpdateWarning()
	{
		if (_warning == null || OwnerUnit == null) return;
		float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		Vector3 velocity = _jumpDirection * HorizontalSpeed + Vector3.Up * OwnerUnit.Velocity.Y;
		Vector3 landing = EnemyAttackWarning.PredictImpact(OwnerUnit, OwnerUnit.GlobalPosition + Vector3.Up * .05f, velocity, gravity, 0, MaxAirTime, CombatLayers.World);
		Vector3 center = landing + _hitbox.GlobalPosition - OwnerUnit.GlobalPosition;
		_warning.Hitbox(_hitbox, center);
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
				_warningRefresh -= delta;
				if (_warningRefresh <= 0) { UpdateWarning(); _warningRefresh = .1f; }
				if (Movement == null)
				{
					Finish();
					return;
				}

				if (!Movement.IsGrounded)
				{
					_leftGround = true;
				}

				if ((_leftGround && Movement.IsGrounded) || StateTime > MaxAirTime)
				{
					Movement.EndDash();
					_hitbox.Activate(OwnerUnit);
					_warning?.HideAll();
					_stage = 1;
					StateTime = 0f;
				}

				break;

			case 1:
				if (StateTime >= ActiveTime)
				{
					_hitbox.Deactivate();
					Movement?.EndDash();
					Finish();
				}

				break;
		}
	}
}
