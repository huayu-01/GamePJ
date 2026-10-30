using System;
using ActRoguelike.Combat;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>沿命中表面法线刺落的长枪；同一来源只有一根，武技2可重定位，靠近收回。</summary>
public partial class SlantSunAbility : EvolvedAttackAbility, IPiercingSpearAbility, IWeaponPresentationSource
{
	[Export] public float AimDistance { get; set; } = 16;
	[Export] public float SpawnHeight { get; set; } = 5;
	[Export] public float FallSpeed { get; set; } = 20;
	[Export] public float FallDamage { get; set; } = 18;
	[Export] public float ImpactDamage { get; set; } = 40;
	[Export] public float ContactDamage { get; set; } = 12;
	[Export] public float ContactInterval { get; set; } = .75f;
	[Export] public float RecallRadius { get; set; } = 1.5f;
	public bool LanceUnlimitedTargets { get; set; } = true;
	public SurfaceSpearPin? Pin => IsInstanceValid(_pin) ? _pin : null;
	public bool PreviewShowing => _preview?.Visible == true;
	public bool TryGetAimSurface(out Vector3 point, out Vector3 normal) => Surface(out point, out normal);
	private SurfaceSpearPin? _pin; private MeshInstance3D? _preview; private Ability? _lockedPrimary;
	private bool _inputHeld, _primaryEnabled;
	public bool TryReadWeaponPresentation(out WeaponPresentationState state) { bool away = Enabled && Pin != null; state = new(away, false, Vector3.Zero); return away; }
	public override void UpdateInputPreview(bool held) { _inputHeld = held && Enabled; if (!_inputHeld && IsInstanceValid(_preview)) _preview!.Hide(); }
	private bool Surface(out Vector3 point, out Vector3 normal)
	{
		point = normal = default; if (OwnerUnit == null || GetViewport().GetCamera3D() is not { } camera) return false;
		Vector2 center = GetViewport().GetVisibleRect().Size * .5f; Vector3 from = camera.ProjectRayOrigin(center), direction = camera.ProjectRayNormal(center);
		using var query = PhysicsRayQueryParameters3D.Create(from, from + direction * (AimDistance + from.DistanceTo(OwnerUnit.GlobalPosition)), CombatLayers.World);
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query); if (hit.Count == 0) return false;
		point = hit["position"].AsVector3(); normal = hit["normal"].AsVector3().Normalized();
		return point.IsFinite() && normal.LengthSquared() > .9f && Targeting.AimPoint(OwnerUnit).DistanceTo(point) <= AimDistance;
	}
	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (!_inputHeld || OwnerUnit is not { IsDead: false } || !Surface(out var point, out var normal)) { if (IsInstanceValid(_preview)) _preview!.Hide(); return; }
		if (!IsInstanceValid(_preview))
		{
			_preview = new MeshInstance3D { TopLevel = true, Mesh = new TorusMesh { InnerRadius = .96f, OuterRadius = 1, Rings = 48, RingSegments = 6 },
				MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.55f, .85f, 1, .65f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_preview);
		}
		_preview!.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up, normal)).Scaled(new Vector3(EffectiveRadius, .05f, EffectiveRadius)), point + normal * .05f); _preview.Show();
	}
	protected override void OnActivate(Unit? target) { }
	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		if (!Surface(out var point, out var normal)) { Finish(); return; }
		Snapshot(); if (IsInstanceValid(_pin)) { _pin!.Removed -= Reclaimed; _pin.Stop(); }
		float offset = SpawnHeight;
		using (var query = PhysicsRayQueryParameters3D.Create(point + normal * .05f, point + normal * SpawnHeight, CombatLayers.World))
		using (var obstruction = GetWorld3D().DirectSpaceState.IntersectRay(query)) if (obstruction.Count > 0) offset = Mathf.Max(.1f, point.DistanceTo(obstruction["position"].AsVector3()) - .1f);
		float total = Mathf.Max(.01f, FallDamage + ImpactDamage);
		_pin = new SurfaceSpearPin { OwnerUnit = OwnerUnit!, Destination = point, SurfaceNormal = normal, Speed = FallSpeed, Radius = EffectiveRadius,
			SizeScale = Mathf.Max(.1f, OwnerUnit!.Stats.Get("AttackRangeMultiplier")) * OwnerUnit.GlobalBasis.Scale.Abs().X,
			RecallRadius = RecallRadius, ContactInterval = ContactInterval, FallingAttack = Attack(FallDamage, FallDamage / total), ImpactAttack = Attack(ImpactDamage, ImpactDamage / total), ContactAttack = Attack(ContactDamage, 0) };
		_pin.Removed += Reclaimed; AddChild(_pin); _pin.GlobalPosition = point + normal * offset;
		if (_lockedPrimary == null)
		{
			_lockedPrimary = OwnerUnit.AbilitySlots.Resolve("weapon_skill_1");
			if (_lockedPrimary != null) { _primaryEnabled = _lockedPrimary.Enabled; _lockedPrimary.StopForReplacement(); _lockedPrimary.Enabled = false; }
		}
		Finish();
	}
	private void Reclaimed(SurfaceSpearPin pin)
	{
		if (_pin != pin) return; _pin = null;
		if (IsInstanceValid(_lockedPrimary) && OwnerUnit?.AbilitySlots.Owns(_lockedPrimary!) == true) _lockedPrimary!.Enabled = _primaryEnabled;
		_lockedPrimary = null;
	}
	protected override void OnChargeAborted() => Finish();
	protected override void OnLoadoutReset()
	{
		_inputHeld = false; if (IsInstanceValid(_preview)) _preview!.Hide();
		if (IsInstanceValid(_pin)) { var pin = _pin!; pin.Removed -= Reclaimed; pin.Stop(); Reclaimed(pin); }
		else if (IsInstanceValid(_lockedPrimary) && OwnerUnit?.AbilitySlots.Owns(_lockedPrimary!) == true) { _lockedPrimary!.Enabled = _primaryEnabled; _lockedPrimary = null; }
	}
	public override void _ExitTree() { OnLoadoutReset(); base._ExitTree(); }
}
