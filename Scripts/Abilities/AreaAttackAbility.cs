using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 范围攻击：前摇（预警）→ 判定窗口 → 后摇。以自身为中心，命中体是子节点。
/// 自爆单位把 <see cref="KillSelf"/> 勾上即可复用同一套（接近 → 爆炸 → 自身死亡）。
/// </summary>
public partial class AreaAttackAbility : Ability
{
	[Export] public float ActiveTime { get; set; } = 0.15f;
	[Export] public bool KillSelf { get; set; }
	[Export] public float TriggerRadius { get; set; } = 2.5f;

	private AttackHitbox _hitbox = null!;
	private int _stage;
	private EnemyAttackWarning? _warning;
	protected override void OnFinishOrCancel() { _hitbox?.Deactivate(); _warning?.HideAll(); }

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

		return OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition) <= TriggerRadius;
	}

	protected override void OnActivate(Unit? target)
	{
		_stage = 0;
		_hitbox.Deactivate();
		_warning?.Hitbox(_hitbox);
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
				_warning?.Hitbox(_hitbox);
				if (StateTime >= Windup)
				{
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
					_stage = 2;
					StateTime = 0f;

					if (KillSelf)
					{
						Cancel();
						OwnerUnit?.ForceKill();
						return;
					}
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
