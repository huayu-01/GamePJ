using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Abilities;

public partial class ArcaneSliceAbility : EvolvedAttackAbility
{
	[Export] public float SearchRadius { get; set; } = 12;
	[Export] public float BounceRadius { get; set; } = 6;
	[Export] public int Bounces { get; set; } = 4;
	[Export] public float FlightSpeed { get; set; } = 45;
	[Export] public float MaxFlightSeconds { get; set; } = .9f;
	[Export] public float SlideDistance { get; set; } = .9f;
	[Export] public float SlideSeconds { get; set; } = .15f;
	public int ContactCount => _visited.Count;
	private readonly List<Unit> _visited = new();
	private Unit? _target; private Node3D? _model; private MeshInstance3D? _particle;
	private bool _oldVisible, _oldInvulnerable, _protected, _sliding;
	private Vector3 _fallback;
	private Unit? Select(Vector3 from, float radius) => Enemies(from + Vector3.Up, radius)
		.Where(u => !_visited.Contains(u) && CanSee(u, from + Vector3.Up)).OrderBy(u => u.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();
	protected override bool CanActivate(Unit? target) => MapManager.Instance is { IsReady: true } map
		&& map.TryGroundAnchor(OwnerUnit!.GlobalPosition, out var anchor, OwnerUnit) && map.TryValidPoint(anchor, anchor, out _, OwnerUnit)
		&& Enemies(Targeting.AimPoint(OwnerUnit!), SearchRadius).Any(u => CanSee(u, Targeting.AimPoint(OwnerUnit!)));
	protected override void OnActivate(Unit? target)
	{
		_visited.Clear(); _sliding = false;
		var map = MapManager.Instance!;
		if (!map.TryGroundAnchor(OwnerUnit!.GlobalPosition, out var anchor, OwnerUnit) || !map.TryValidPoint(anchor, anchor, out _fallback, OwnerUnit)) { Finish(); return; }
		Snapshot();
		_target = OwnerUnit.GetNodeOrNull<AimAssistController>("AimAssist")?.Selected;
		if (_target is not { IsValidTarget: true } || !OwnerUnit.IsHostileTo(_target) || _target.GlobalPosition.DistanceTo(_fallback) > SearchRadius || !CanSee(_target, Targeting.AimPoint(OwnerUnit))) _target = Select(_fallback, SearchRadius);
		_model = ActiveModel; _oldVisible = _model?.Visible ?? false; if (_model != null) _model.Visible = false;
		_oldInvulnerable = OwnerUnit.IsInvulnerable; OwnerUnit.IsInvulnerable = true; _protected = true;
		Movement?.BeginVelocityOverride(this, Vector3.Zero, isDash: true);
		_particle = new MeshInstance3D { Mesh = new SphereMesh { Radius = .12f, Height = .24f },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.5f, .8f, 1), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				EmissionEnabled = true, Emission = new Color(.5f, .8f, 1), EmissionEnergyMultiplier = 2 } };
		AddChild(_particle); _particle.Position = Vector3.Up;
	}
	protected override void Tick(float delta)
	{
		if (!IsActive) return;
		if (_sliding) { if (Movement?.HasVelocityOverride != true || Movement.VelocityOverrideFinished(this)) Finish(); return; }
		if (!IsInstanceValid(_target) || _target is not { IsValidTarget: true }) _target = Select(OwnerUnit!.GlobalPosition, BounceRadius);
		if (_target == null || StateTime >= MaxFlightSeconds) { Complete(); return; }
		Vector3 before = OwnerUnit!.GlobalPosition; Vector3 destination = _target.GlobalPosition;
		Vector3 nextPosition = before.MoveToward(destination, FlightSpeed * delta);
		using (var query = PhysicsRayQueryParameters3D.Create(before + Vector3.Up, nextPosition + Vector3.Up, CombatLayers.World))
		using (var obstacle = GetWorld3D().DirectSpaceState.IntersectRay(query)) if (obstacle.Count > 0) { Complete(); return; }
		OwnerUnit.GlobalPosition = nextPosition;
		SpecializationFx.Line(GetParent(), before + Vector3.Up, OwnerUnit.GlobalPosition + Vector3.Up, new Color(.55f, .8f, 1));
		if (OwnerUnit.GlobalPosition.DistanceSquaredTo(destination) > .04f) return;
		_visited.Add(_target); Vector3 center = Targeting.AimPoint(_target);
		SpecializationFx.Line(GetParent(), center + new Vector3(-.8f, -.6f, 0), center + new Vector3(.8f, .6f, 0), new Color(.8f, .9f, 1));
		_target = _visited.Count < Bounces + 1 ? Select(destination, BounceRadius) : null;
		if (_target == null) Complete();
	}
	private void Complete()
	{
		var attack = Attack(Damage); foreach (var unit in _visited) Strike(unit, attack);
		var map = MapManager.Instance; Vector3 desired = OwnerUnit!.GlobalPosition;
		if (map?.TryNavigationAnchor(_fallback, out var anchor, OwnerUnit) == true
			&& map.TryRingPoint(desired, Mathf.Max(.8f, MapManager.BodySize(OwnerUnit).Radius * 2), 2.2f, out var point, OwnerUnit, anchor)) _fallback = point;
		OwnerUnit.GlobalPosition = _fallback; OwnerUnit.Velocity = Vector3.Zero; OwnerUnit.ResetPhysicsInterpolation(); RestoreProtection();
		Movement?.EndVelocityOverride(this); _sliding = true;
		Vector3 forward = -OwnerUnit.GlobalBasis.Z; forward.Y = 0;
		if (Movement?.BeginVelocityOverride(this, forward.Normalized() * (SlideDistance / Mathf.Max(.01f, SlideSeconds)), SlideSeconds, isDash: true, planarOnly: true) != true) Finish();
	}
	private void RestoreProtection()
	{
		if (_protected && IsInstanceValid(OwnerUnit)) { OwnerUnit!.IsInvulnerable = _oldInvulnerable; if (IsInstanceValid(_model)) _model!.Visible = _oldVisible; }
		_protected = false; if (IsInstanceValid(_particle)) _particle!.QueueFree(); _particle = null;
	}
	protected override void OnFinishOrCancel()
	{
		if (_protected && !_sliding && IsInstanceValid(OwnerUnit)) { OwnerUnit!.GlobalPosition = _fallback; OwnerUnit.ResetPhysicsInterpolation(); }
		RestoreProtection(); Movement?.EndVelocityOverride(this); base.OnFinishOrCancel();
	}
	protected override void OnLoadoutReset() { RestoreProtection(); Movement?.EndVelocityOverride(this); _visited.Clear(); _target = null; }
	public override void _ExitTree() { OnLoadoutReset(); base._ExitTree(); }
}
