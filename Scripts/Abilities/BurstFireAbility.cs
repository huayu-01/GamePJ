using ActRoguelike.Combat;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 火力倾泻（角色 1 的技能）。按武器类型分两种模式：
/// - **非连发武器（手枪 / 激光狙击枪）**：无视当前弹药，直接打出一整个弹匣（1/12 也能打满 12 发）；
/// - **连发武器（步枪）**：在 <see cref="Duration"/> 秒内持续开火且**不消耗子弹**（弹匣 0/N 也能用）。
/// 判定依据是 `Weapon.Automatic`，所以以后新增连发武器会自动走第二种。
/// </summary>
public partial class BurstFireAbility : Ability
{
	/// <summary>连发模式下持续开火的时长（秒）。</summary>
	[Export] public float Duration { get; set; } = 2.5f;

	/// <summary>连发模式下的射击间隔覆盖值（越小越猛）。</summary>
	[Export] public float BurstInterval { get; set; } = 0.06f;

	protected override bool CanActivate(Unit? target) => FindInOwner<Weapon>() != null;

	protected override void OnActivate(Unit? target)
	{
		if (FindInOwner<Weapon>() is not { } weapon)
		{
			Finish();
			return;
		}

		if (weapon.Automatic)
		{
			weapon.BeginInfiniteFire(Duration, BurstInterval);
		}
		else
		{
			weapon.FireFullMagazine();
		}

		Finish();
	}
}
