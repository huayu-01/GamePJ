using ActRoguelike.Combat;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 远程攻击：前摇 → 开火（走统一 Weapon/投射物流程）→ 后摇。
/// 武器是本能力的子节点，因此敌人武器与玩家武器共用同一套逻辑。
/// </summary>
public partial class ProjectileAttackAbility : Ability
{
	[Export] public float MinRange { get; set; } = 3f;
	[Export] public float MaxRange { get; set; } = 18f;

	/// <summary>瞄准目标身上抬高多少（默认胸口高度）。</summary>
	[Export] public float AimHeight { get; set; } = 0.9f;

	private Weapon? _weapon;
	private bool _fired;

	protected override void OnReady()
	{
		_weapon = GetNodeOrNull<Weapon>("Weapon");
	}

	protected override bool CanActivate(Unit? target)
	{
		if (target == null || OwnerUnit == null)
		{
			return false;
		}

		float distance = OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		return distance >= MinRange && distance <= MaxRange;
	}

	protected override void OnActivate(Unit? target)
	{
		_fired = false;
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (!_fired && StateTime >= Windup)
		{
			Fire();
			_fired = true;
			return;
		}

		if (_fired && StateTime >= Windup + Recover)
		{
			Finish();
		}
	}

	private void Fire()
	{
		if (_weapon == null || Target == null || OwnerUnit == null)
		{
			return;
		}

		Vector3 aimPoint = Target.GlobalPosition + (Vector3.Up * AimHeight);
		_weapon.AimPoint = aimPoint;
		_weapon.TryFire(aimPoint);
	}
}
