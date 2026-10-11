using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 位移技能：朝 <see cref="Ability.Aim"/> 配置的方向高速移动（玩家滑铲配成准星方向），期间无敌。
/// 冲刺/蓄力突进用同一套参数加命中体即可。
/// </summary>
public partial class DashAbility : Ability
{
	[Export] public float Distance { get; set; } = 6f;
	[Export] public float Duration { get; set; } = 0.22f;
	[Export] public float InvulnerableTime { get; set; } = 0.18f;

	private float _invulnerableRemaining;
	private bool _granted;

	protected override void OnActivate(Unit? target)
	{
		if (Movement == null || OwnerUnit == null)
		{
			Cancel();
			return;
		}

		Vector3 direction = ResolveAimDirection();
		if (direction.LengthSquared() < 0.04f)
		{
			direction = -OwnerUnit.GlobalTransform.Basis.Z;
		}

		Movement.BeginDash(direction, Distance / Mathf.Max(0.05f, Duration));
		_invulnerableRemaining = InvulnerableTime;
		_granted = true;
		OwnerUnit.IsInvulnerable = true;
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (_granted)
		{
			_invulnerableRemaining -= delta;
			if (_invulnerableRemaining <= 0f && OwnerUnit != null)
			{
				OwnerUnit.IsInvulnerable = false;
				_granted = false;
			}
		}

		if (StateTime >= Duration)
		{
			Movement?.EndDash();
			Finish();
		}
	}

	public override void _ExitTree()
	{
		if (_granted && OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.IsInvulnerable = false;
		}
	}
}
