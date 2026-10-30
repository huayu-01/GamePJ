using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

public partial class ElectromagneticRingRule : Node3D, IStoppableRewardRule
{
	public float Radius { get; set; } = 3;
	public float Damage { get; set; } = 3;
	public float LinkRadius { get; set; } = 4;
	public int Targets { get; set; } = 3;
	public float TargetInterval { get; set; } = 1;
	public float ChainInterval { get; set; } = .5f;
	public int ChainCount { get; private set; }
	public int HitCount { get; private set; }
	public float EffectiveRadius => Radius * (1 + (_owner?.Stats.Get("ElectromagneticRadiusAdd") ?? 0));
	private Unit? _owner; private MeshInstance3D _ring = null!;
	private readonly Dictionary<ulong, float> _next = new();
	private float _age, _scan, _chain, _prune;
	public override void _Ready()
	{
		_owner = GetParent<Unit>(); TopLevel = true; SetProcess(false);
		_ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .97f, OuterRadius = 1, Rings = 48, RingSegments = 6 },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.35f, .8f, 1, .7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(.3f, .7f, 1) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		AddChild(_ring);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner is not { IsDead: false }) { Stop(); return; }
		float dt = (float)delta; _age += dt; _chain -= dt; _scan -= dt; _prune -= dt;
		GlobalPosition = _owner.GlobalPosition + Vector3.Up * .2f; _ring.Scale = new Vector3(EffectiveRadius, .1f, EffectiveRadius);
		if (_prune <= 0) { _prune = 2; foreach (ulong key in _next.Where(p => p.Value <= _age).Select(p => p.Key).ToArray()) _next.Remove(key); }
		if (_scan > 0 || _chain > 0) return; _scan = .1f;
		var candidates = _owner.GetTree().GetNodesInGroup("enemy_units").OfType<Unit>().Where(u => Eligible(u)).ToArray();
		Unit? next = candidates.Where(u => HorizontalDistance(u.GlobalPosition, GlobalPosition) <= EffectiveRadius && VisibleFrom(GlobalPosition + Vector3.Up, u))
			.OrderBy(u => u.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault();
		if (next == null) return;
		_chain = ChainInterval; ChainCount++; var visited = new HashSet<Unit>(); Vector3 from = GlobalPosition;
		int extra = _owner.Stats.Get("ElectromagneticTargetsAdd") > 0 ? Mathf.RoundToInt(_owner.Stats.Get("ElectromagneticTargetsAdd")) : 0;
		var mastery = _owner.GetNodeOrNull<SpecializationRuntime>(SpecializationRuntime.NodeName)?.Definition<ThunderMasteryBehaviorDefinition>(SpecializationKind.ThunderMastery);
		int limit = Mathf.Min(6, Targets + extra + (mastery?.ExtraJumps ?? 0)); var offense = AttackOffense.Capture(_owner);
		for (int i = 0; i < limit && next != null; i++)
		{
			visited.Add(next); _next[next.GetInstanceId()] = _age + TargetInterval;
			Vector3 position = Targeting.AimPoint(next); SpecializationFx.Line(GetParent(), from + Vector3.Up, position, new Color(.35f, .8f, 1));
			var attack = new AttackInstance { Attacker = _owner, BaseDamage = Damage * offense.DamageMultiplier, IsSecondary = true, SuppressFlatDamageBonuses = true, Ability = "ElectromagneticChain", Tags = new[] { "Lightning" } };
			next.ReceiveHit(attack.RollHit(next, Vector3.Zero, position)); HitCount++; from = next.GlobalPosition;
			next = candidates.Where(u => !visited.Contains(u) && Eligible(u) && u.GlobalPosition.DistanceSquaredTo(from) <= LinkRadius * LinkRadius && VisibleFrom(from + Vector3.Up, u))
				.OrderBy(u => u.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();
		}
	}
	private bool Eligible(Unit u) => u.IsValidTarget && _owner!.IsHostileTo(u) && Mathf.Abs(u.GlobalPosition.Y - _owner.GlobalPosition.Y) < 2.5f && (!_next.TryGetValue(u.GetInstanceId(), out float until) || until <= _age);
	private bool VisibleFrom(Vector3 from, Unit target)
	{
		using var query = PhysicsRayQueryParameters3D.Create(from, Targeting.AimPoint(target), CombatLayers.World);
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query); return hit.Count == 0;
	}
	private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
	public void Stop() { _owner = null; _next.Clear(); Hide(); SetPhysicsProcess(false); }
	public override void _ExitTree() => Stop();
}
