using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>原三刀改成一枚基础飞刀，实际命中后由独立本轮实例安排三枚追击。</summary>
public partial class KnifeDanceAbility : EvolvedAttackAbility
{
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public float Speed { get; set; } = 32;
	[Export] public float FollowupDamage { get; set; } = 8;
	[Export] public int FollowupCount { get; set; } = 3;
	[Export] public float FollowupInterval { get; set; } = .08f;
	[Export] public float HomingDelay { get; set; } = .15f;
	[Export] public float Lifetime { get; set; } = 1.5f;
	public int CastCount { get; private set; }
	private bool _fired;
	protected override void OnActivate(Unit? target) { _fired = false; PlaySkillAnimation(); }
	protected override void OnAnimEvent(string name) { if (name == AnimEvent && !_fired) Fire(); }
	private void Fire()
	{
		if (ProjectileScene == null || OwnerUnit == null) { Finish(); return; }
		_fired = true; Snapshot(); CastCount++;
		int extras = Mathf.Max(0, Mathf.RoundToInt(OwnerUnit.Stats.Get("ProjectileCountAdd")));
		int delay = Mathf.Max(0, Mathf.RoundToInt(OwnerUnit.Stats.Get("ProjectileFollowupDelayFrames")));
		float total = Mathf.Max(.01f, Damage + FollowupDamage * FollowupCount);
		var attack = new AttackInstance { Attacker = OwnerUnit, Ability = AbilityId, AbilityKind = AbilityId, IsManualWeaponAttack = true,
			BaseDamage = Damage * Offense.DamageMultiplier, CastId = Cast, ProjectileCount = 1 + extras, PowerDamageWeight = Damage / total,
			PowerDamageSnapshotOverride = PowerSnapshot, CanCrit = true, CritChance = Offense.CritChance, CritMultiplier = Offense.CritMultiplier, Lifetime = Lifetime };
		var followups = new KnifeDanceFollowup { OwnerUnit = OwnerUnit, PrimaryAttack = attack, ProjectileScene = ProjectileScene,
			Damage = FollowupDamage * Offense.DamageMultiplier, Count = FollowupCount, Speed = Speed, Interval = FollowupInterval,
			HomingDelay = HomingDelay, Lifetime = Lifetime, PowerShare = FollowupDamage / total };
		AddChild(followups);
		Vector3 chest = Targeting.AimPoint(OwnerUnit); Vector3 aim = AimUtil.GetCrosshairPoint(GetViewport(), OwnerUnit, 200, CombatLayers.World | CombatLayers.HostileTo(OwnerUnit.Faction));
		Vector3 direction = (aim - chest).Normalized(); if (direction.LengthSquared() < .1f) direction = ResolveAimDirection();
		for (int i = 0; i <= extras; i++)
		{
			var projectile = ProjectileScene.Instantiate<Projectile>(); projectile.Speed = Speed; projectile.GravityScale = 1;
			ProjectileDispatch.Submit(this, OwnerUnit, projectile, attack, chest + direction * .6f, direction, delay * i);
		}
	}
	protected override void Tick(float delta) { if (IsActive && _fired && StateTime >= Recover) Finish(); }
	protected override void OnLoadoutReset()
	{
		foreach (var child in GetChildren()) if (child is KnifeDanceFollowup followup) { followup.Stop(); followup.QueueFree(); }
	}
	public override void _ExitTree() { OnLoadoutReset(); base._ExitTree(); }
}
