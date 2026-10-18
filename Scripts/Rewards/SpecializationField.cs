using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

/// <summary>旋风与雷池共用的独立伤害区域；生命周期使用游戏时间并随来源规则回收。</summary>
public partial class SpecializationField : Node3D
{
	public SpecializationRuntime Runtime { get; set; } = null!;
	public Unit SourceUnit { get; set; } = null!;
	public K Kind { get; set; }
	public float Lifetime { get; set; }
	public bool Active { get; set; } = true;
	public float Radius => Runtime.Value(Kind, "Radius") * (Kind == K.ThunderPool ? 1f : Runtime.WindSizeScale);
	public int PulseCount { get; private set; }
	private float _timer, _age;
	private MeshInstance3D _mesh = null!;
	public override void _Ready()
	{
		TopLevel = true;
		Color color = Kind == K.ThunderPool ? new Color(1f, 0.8f, 0.1f, 0.25f) : new Color(0.4f, 0.9f, 0.85f, 0.3f);
		var material = new StandardMaterial3D { AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
		Mesh geometry = Kind == K.ThunderPool ? new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, Material = material }
			: new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1f, Material = material };
		_mesh = new MeshInstance3D { Mesh = geometry, Position = Vector3.Up * 0.12f };
		AddChild(_mesh);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(SourceUnit) || SourceUnit.IsDead) { QueueFree(); return; }
		float dt = (float)delta;
		_age += dt;
		if (Lifetime > 0f && _age > Lifetime) { QueueFree(); return; }
		Visible = Active;
		_mesh.Scale = new Vector3(Radius, 0.4f, Radius);
		_mesh.RotateY(dt * 2f);
		if (!Active) { _timer = 0f; return; }
		_timer += dt;
		if (_timer < Mathf.Max(0.05f, Runtime.Value(Kind, "TickInterval"))) return;
		_timer = 0f; PulseCount++;
		var targets = Runtime.Enemies(GlobalPosition, Radius).ToArray();
		foreach (Unit target in targets)
		{
			if (Kind == K.ThunderPool) Runtime.Thunder(target, Runtime.Value(Kind, "Damage"));
			else
			{
				var attack = new AttackInstance { Attacker = SourceUnit, Ability = "Whirlwind", AbilityKind = Kind.ToString(), BaseDamage = Runtime.Value(Kind, "Damage") * AttackOffense.Capture(SourceUnit).DamageMultiplier, IsSecondary = true };
				target.ReceiveHit(attack.RollHit(target, Vector3.Zero, target.GlobalPosition));
			}
		}
	}
}
