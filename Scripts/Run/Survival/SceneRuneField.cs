using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Run;

public partial class SceneRuneField : SceneRuneActor
{
	private readonly HashSet<Unit> _inside = new();
	private readonly HashSet<Unit> _buffed = new();
	private readonly List<Unit> _iteration = new();
	private Area3D _area = null!;
	private MeshInstance3D _disc = null!;
	private float _tick;
	public int AppliedCount { get; private set; }
	public override void Initialize()
	{
		Ring(Data.Radius, Data.Tint);
		_disc = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = Data.Radius, BottomRadius = Data.Radius, Height = .035f, RadialSegments = 40 },
			Position = Vector3.Up * .045f, MaterialOverride = Material(new Color(Data.Tint, .18f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_disc);
		_area = new Area3D { CollisionLayer = 0, CollisionMask = CombatLayers.UnitBody, Monitoring = true, Monitorable = false };
		_area.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Data.Radius, Height = .8f }, Position = Vector3.Up * .4f });
		_area.BodyEntered += Enter; _area.BodyExited += Exit; AddChild(_area);
	}
	private void Enter(Node3D body) { if (body is Unit unit) _inside.Add(unit); }
	private void Exit(Node3D body) { if (body is Unit unit) { _inside.Remove(unit); RemoveBuff(unit); } }
	private void RemoveBuff(Unit unit) { if (_buffed.Remove(unit) && IsInstanceValid(unit)) unit.Stats.RemoveBySource(this); }
	public override void Tick(float dt)
	{
		if (Finished) return; Age += dt;
		if (Age >= Data.WarningSeconds + Data.Lifetime) { Finish(); return; }
		bool active = Age >= Data.WarningSeconds; _disc.Visible = active;
		if (!active) return;
		_tick -= dt; if (_tick > 0) return; _tick = .25f;
		_iteration.Clear(); _iteration.AddRange(_inside);
		foreach (var unit in _iteration)
		{
			if (!Live(unit)) { _inside.Remove(unit); RemoveBuff(unit); continue; }
			if (Data.Kind == SceneRuneDefinition.RuneKind.PowerField)
			{
				if (unit == Controller.Player && _buffed.Add(unit)) { unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = Data.DamageMultiplier, Source = this }); AppliedCount++; }
			}
			else if (Data.Kind == SceneRuneDefinition.RuneKind.HealingSpring) { unit.Heal(Data.HealPerSecond * .25f); AppliedCount++; }
			else if (unit.IsOnFloor() && unit.Movement != null)
			{
				float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * unit.Movement.GravityScale;
				unit.Movement.SetVerticalVelocity(Mathf.Sqrt(2 * gravity * Data.LaunchHeight)); AppliedCount++;
			}
		}
	}
	public override void Finish()
	{
		if (Finished) return;
		foreach (var unit in _buffed) if (IsInstanceValid(unit)) unit.Stats.RemoveBySource(this);
		_buffed.Clear(); _inside.Clear(); base.Finish();
	}
}
