using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Rewards;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Combat;

public partial class SurfaceSpearPin : Node3D
{
	public Unit OwnerUnit { get; set; } = null!;
	public Vector3 Destination { get; set; }
	public Vector3 SurfaceNormal { get; set; } = Vector3.Up;
	public float Speed { get; set; } = 20;
	public float ReturnSpeed { get; set; } = 20;
	public float Radius { get; set; } = 2.6f;
	public float SizeScale { get; set; } = 1;
	public float RecallRadius { get; set; } = 1.5f;
	public float ContactInterval { get; set; } = .75f;
	public AttackInstance FallingAttack { get; set; } = null!;
	public AttackInstance ImpactAttack { get; set; } = null!;
	public AttackInstance ContactAttack { get; set; } = null!;
	public bool Landed { get; private set; }
	public bool Returning { get; private set; }
	public event Action<SurfaceSpearPin>? Removed;
	private readonly HashSet<ulong> _fallHits = new(); private readonly Dictionary<ulong, float> _nextContact = new();
	private float _age, _scan;
	private bool _stopped;
	public override void _Ready()
	{
		TopLevel = true; SetProcess(false); Basis = new Basis(new Quaternion(Vector3.Up, SurfaceNormal));
		var material = new StandardMaterial3D { AlbedoColor = new Color(.75f, .85f, 1), Metallic = .75f, Roughness = .2f, EmissionEnabled = true, Emission = new Color(.15f, .25f, .4f) };
		AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .04f * SizeScale, BottomRadius = .04f * SizeScale, Height = 2 * SizeScale }, Position = Vector3.Up * SizeScale, MaterialOverride = material });
		AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = .12f * SizeScale, Height = .5f * SizeScale }, Rotation = Vector3.Right * Mathf.Pi, Position = Vector3.Up * .15f * SizeScale, MaterialOverride = material });
	}
	private Unit[] Enemies() => GetTree().GetNodesInGroup("enemy_units").OfType<Unit>().Where(u => u.IsValidTarget && OwnerUnit.IsHostileTo(u)).ToArray();
	private static float SegmentDistance(Vector3 point, Vector3 from, Vector3 to)
	{
		Vector3 line = to - from; float t = line.LengthSquared() > .000001f ? Mathf.Clamp((point - from).Dot(line) / line.LengthSquared(), 0, 1) : 0;
		return point.DistanceTo(from + line * t);
	}
	private void Hit(Unit enemy, AttackInstance attack) => enemy.ReceiveHit(attack.RollHit(enemy, -SurfaceNormal, Targeting.AimPoint(enemy)));
	public override void _PhysicsProcess(double delta)
	{
		if (_stopped) return; if (!IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead) { Stop(); return; }
		float dt = (float)delta; _age += dt;
		if (Returning)
		{
			Vector3 home = OwnerUnit.GlobalPosition + Vector3.Up * .9f;
			GlobalPosition = GlobalPosition.MoveToward(home, ReturnSpeed * dt);
			if (GlobalPosition.DistanceSquaredTo(home) <= .04f) Stop();
			return;
		}
		if (!Landed)
		{
			Vector3 before = GlobalPosition, after = before.MoveToward(Destination, Speed * dt);
			foreach (var enemy in Enemies())
				if (!_fallHits.Contains(enemy.GetInstanceId()) && SegmentDistance(Targeting.AimPoint(enemy), before, after) <= MapManager.BodySize(enemy).Radius + .15f * SizeScale)
				{ _fallHits.Add(enemy.GetInstanceId()); Hit(enemy, FallingAttack); }
			GlobalPosition = after;
			if (after.DistanceSquaredTo(Destination) > .0001f) return;
			Landed = true;
			foreach (var enemy in Enemies())
			{
				Vector3 offset = Targeting.AimPoint(enemy) - Destination; float depth = offset.Dot(SurfaceNormal);
				if (depth < -.1f || depth > 2 || (offset - SurfaceNormal * depth).LengthSquared() > Radius * Radius) continue;
				using var query = PhysicsRayQueryParameters3D.Create(Destination + SurfaceNormal * .08f, Targeting.AimPoint(enemy), CombatLayers.World);
				using var obstacle = GetWorld3D().DirectSpaceState.IntersectRay(query); if (obstacle.Count == 0) Hit(enemy, ImpactAttack);
			}
			SpecializationFx.Line(GetParent(), Destination, Destination + SurfaceNormal * 2, new Color(.7f, .85f, 1));
		}
		if (OwnerUnit.GlobalPosition.DistanceSquaredTo(Destination) <= RecallRadius * RecallRadius) { Stop(); return; }
		_scan -= dt; if (_scan > 0) return; _scan = .1f;
		foreach (var enemy in Enemies())
		{
			ulong id = enemy.GetInstanceId(); if (_nextContact.TryGetValue(id, out float next) && _age < next) continue;
			if (SegmentDistance(Targeting.AimPoint(enemy), Destination, Destination + SurfaceNormal * (2 * SizeScale)) > MapManager.BodySize(enemy).Radius + .15f * SizeScale) continue;
			_nextContact[id] = _age + Mathf.Max(.05f, ContactInterval); Hit(enemy, ContactAttack);
		}
	}
	/// <summary>落地后召回：以独立 ReturnSpeed 飞回玩家，期间不产生接触伤害。</summary>
	public bool Recall()
	{
		if (_stopped || Returning || !Landed) return false;
		Returning = true;
		return true;
	}

	public void Stop() { if (_stopped) return; _stopped = true; Hide(); SetPhysicsProcess(false); Removed?.Invoke(this); Removed = null; QueueFree(); }
	public override void _ExitTree() { Removed?.Invoke(this); Removed = null; _nextContact.Clear(); _fallHits.Clear(); }
}
