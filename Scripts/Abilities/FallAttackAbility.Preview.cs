using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

public partial class FallAttackAbility
{
	private SkillIndicator? _landingPreview, _rangePreview;
	public bool LandingPreviewVisible => _landingPreview?.IsShowing == true;
	public Vector3 PreviewAimPoint { get; private set; }
	public float PreviewAimRadius { get; private set; }

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateLandingPreview();
	}

	/// <summary>预览与真实Action共用同一选点；越界点压回圆内后重新探测真实地形。</summary>
	private bool TryResolveLandingAim(out Vector3 point, out Vector3 center, out float radius)
	{
		point = center = Vector3.Zero; radius = 0;
		if (OwnerUnit == null || HomingMode != DiveMode.Crosshair || !TryGroundHeight(out float height)) return false;
		Vector3 feet = OwnerUnit.GlobalPosition;
		point = center = feet + Vector3.Down * height;
		radius = AimRadiusHeightRatio > 0 ? height * AimRadiusHeightRatio : height * Mathf.Tan(Mathf.DegToRad(Mathf.Clamp(MaxDiveAngleDegrees, 0, 85)));
		if (!AimUtil.TryGetCrosshairGroundPoint(GetViewport(), OwnerUnit, MaxAimDistance, GroundMinNormalY, out var target)
			|| target.Y >= feet.Y - .001f) return AimRadiusHeightRatio > 0;
		Vector3 planar = new(target.X - feet.X, 0, target.Z - feet.Z);
		if (AimRadiusHeightRatio <= 0) radius = (feet.Y - target.Y) * Mathf.Tan(Mathf.DegToRad(Mathf.Clamp(MaxDiveAngleDegrees, 0, 85)));
		planar = planar.LimitLength(radius);
		using var query = PhysicsRayQueryParameters3D.Create(feet + planar, feet + planar + Vector3.Down * GroundProbeDistance, CombatLayers.World);
		query.Exclude = new Godot.Collections.Array<Rid> { OwnerUnit.GetRid() };
		using var hit = OwnerUnit.GetWorld3D().DirectSpaceState.IntersectRay(query);
		// 圆内没有可落地表面时保留脚下安全落点，不在深坑或墙面画落点。
		if (hit.Count == 0 || hit["normal"].AsVector3().Y < GroundMinNormalY) return true;
		Vector3 candidate = hit["position"].AsVector3();
		if (candidate.Y >= feet.Y - .001f) return true;
		// 斜向路径被墙挡住时回退直落，避免预览越过真实碰撞。
		query.From = feet + Vector3.Up * .02f; query.To = candidate + Vector3.Up * .02f;
		using var obstruction = OwnerUnit.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (obstruction.Count > 0) return true;
		point = candidate;
		return true;
	}

	private void UpdateLandingPreview()
	{
		if (!ShowLandingIndicator || !Enabled || OwnerUnit is not { IsDead: false, HasActionLock: false }
			|| Movement is not { IsGrounded: false, HasVelocityOverride: false, IsControlDisabled: false }
			|| (!IsReady && !(IsActive && !_diving && !_landed))
			|| !TryResolveLandingAim(out var point, out var center, out float radius))
		{ HideLandingPreview(); return; }
		if (_landingPreview == null)
		{
			var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/SkillIndicator.tscn");
			_landingPreview = scene.Instantiate<SkillIndicator>(); _landingPreview.Name = "LandingPreview";
			_landingPreview.Pulse = false; AddChild(_landingPreview);
			_rangePreview = scene.Instantiate<SkillIndicator>(); _rangePreview.Name = "LandingRange";
			_rangePreview.FillAlphaScale = 0; _rangePreview.Alpha = .22f; _rangePreview.Pulse = false; AddChild(_rangePreview);
		}
		PreviewAimPoint = point; PreviewAimRadius = radius;
		float markerRadius = (_hitbox?.GetNodeOrNull<CollisionShape3D>("Shape")?.Shape as SphereShape3D)?.Radius ?? .5f;
		_landingPreview.ShowAreaPoint(point, markerRadius);
		_rangePreview!.ShowAreaPoint(center, radius);
	}

	private void HideLandingPreview()
	{
		_landingPreview?.HideAll(); _rangePreview?.HideAll();
	}
}
