using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

public partial class SpearThrowAbility
{
	[Export] public bool LinearShot { get; set; }
	[Export] public float LanceDamage { get; set; } = 50;
	[Export] public float LanceSpeed { get; set; } = 40;
	[Export] public float LanceKnockback { get; set; } = 10;
	[Export] public int LanceMaxTargets { get; set; } = 2;
	public bool LanceUnlimitedTargets { get; set; }
	public int LanceShots { get; private set; }
	private bool _lanceFired;
	protected override void OnActivate(Unit? target)
	{
		if (!LinearShot) { base.OnActivate(target); return; }
		_lanceFired = false;
		if (PlaySkillAnimation().Length == 0) Cancel();
	}
	protected override void OnAnimEvent(string name)
	{
		if (!LinearShot) { base.OnAnimEvent(name); return; }
		if (_lanceFired || name != AnimEvent || !IsBusy || OwnerUnit == null) return;
		_lanceFired = true;
		Vector3 muzzle = OwnerUnit.GlobalPosition + Vector3.Up * .9f;
		Vector3 point = GetViewport().GetCamera3D() != null
			? AimUtil.GetCrosshairPoint(GetViewport(), OwnerUnit, MaxAimDistance, CombatLayers.World | CombatLayers.HostileTo(OwnerUnit.Faction))
			: muzzle + ResolveAimDirection() * MaxThrowDistance;
		Vector3 direction = (point - muzzle).Normalized();
		var projectile = new Projectile { Name = "SpearLance", Speed = LanceSpeed, SweepRadius = .18f, GravityScale = 0,
			MaxTravelDistance = MaxThrowDistance, QueryGeometry = 1 };
		projectile.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .035f, BottomRadius = .035f, Height = 2.4f },
			RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.8f, .9f, 1), Metallic = .7f } });
		projectile.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(.22f, .55f, .06f) },
			Position = new Vector3(0, 0, -1.4f), RotationDegrees = new Vector3(90, 0, 0),
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.85f, .95f, 1) } });
		(GetTree().CurrentScene ?? GetTree().Root).AddChild(projectile);
		projectile.GlobalPosition = muzzle + direction * .7f;
		var offense = AttackOffense.Capture(OwnerUnit);
		projectile.Launch(new AttackInstance { Attacker = OwnerUnit, Ability = "SpearLance", AbilityKind = AbilityId,
			IsManualWeaponAttack = true, IsDirectAbilityAttack = true, BaseDamage = LanceDamage * offense.DamageMultiplier,
			CanCrit = true, CritChance = offense.CritChance, CritMultiplier = offense.CritMultiplier,
			Knockback = LanceKnockback, Pierce = LanceUnlimitedTargets ? int.MaxValue - 1 : Mathf.Max(0, LanceMaxTargets - 1),
			Lifetime = MaxThrowDistance / Mathf.Max(1, LanceSpeed) + .1f }, direction);
		LanceShots++; Finish();
	}
	protected override void Tick(float delta)
	{
		if (!LinearShot) { base.Tick(delta); return; }
		if (IsBusy && StateTime > AnimationEventTimeout) Cancel();
	}
}
