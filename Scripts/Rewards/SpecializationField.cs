using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

/// <summary>旋风与雷池共用的独立伤害区域；生命周期使用游戏时间并随来源规则回收。</summary>
public partial class SpecializationField : Node3D, IProjectileSlowField
{
	public float ProjectileSpeedMultiplier { get; set; } = .25f;
	public float ProjectileEffectHeight { get; set; } = 3f;
	public bool SlowsProjectileAt(Vector3 point) => Kind != K.ThunderPool && Active && IsInsideTree() && !IsQueuedForDeletion()
		&& ProcessMode != ProcessModeEnum.Disabled && IsInstanceValid(SourceUnit) && !SourceUnit.IsDead
		&& Mathf.Abs(point.Y - GlobalPosition.Y) <= ProjectileEffectHeight
		&& new Vector2(point.X - GlobalPosition.X, point.Z - GlobalPosition.Z).LengthSquared() <= Radius * Radius;
	public SpecializationRuntime Runtime { get; set; } = null!;
	public Unit SourceUnit { get; set; } = null!;
	public K Kind { get; set; }
	public float Lifetime { get; set; }
	public bool Active { get; set; } = true;
	public System.Func<float>? RadiusResolver { get; set; }
	public System.Action<Unit>? Pulse { get; set; }
	public float TickInterval { get; set; } = .5f;
	public bool ThunderVisual { get; set; }
	public float Radius => RadiusResolver?.Invoke() ?? Runtime.Value(Kind, "Radius") * (Kind == K.ThunderPool ? 1f : Runtime.WindSizeScale);
	public int PulseCount { get; private set; }
	private float _timer, _age;
	private MeshInstance3D _mesh = null!;
	private MeshInstance3D? _border;
	private StandardMaterial3D? _borderMaterial;
	public override void _Ready()
	{
		TopLevel = true;
		if (Kind != K.ThunderPool) ProjectileInfluenceFields.Register(this);
		Color color = Kind == K.ThunderPool ? new Color(1f, 0.8f, 0.1f, 0.25f) : new Color(0.4f, 0.9f, 0.85f, 0.3f);
		var material = new StandardMaterial3D { AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
		Mesh geometry = Kind == K.ThunderPool ? new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, Material = material }
			: new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1f, Material = material };
		_mesh = new MeshInstance3D { Mesh = geometry, Position = Vector3.Up * 0.12f };
		AddChild(_mesh);
		if (Kind == K.ThunderPool)
		{
			_borderMaterial = new StandardMaterial3D { AlbedoColor = new Color(1f, .83f, .22f, .82f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				EmissionEnabled = true, Emission = new Color(1f, .72f, .1f), EmissionEnergyMultiplier = 1.2f };
			_border = new MeshInstance3D { Name = "ThunderBoundary", Mesh = new TorusMesh { InnerRadius = .94f, OuterRadius = 1f, Rings = 48, RingSegments = 8 },
				MaterialOverride = _borderMaterial, Position = Vector3.Up * .16f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			AddChild(_border);
		}
	}
	public override void _ExitTree() => ProjectileInfluenceFields.Remove(this);
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(SourceUnit) || SourceUnit.IsDead) { QueueFree(); return; }
		float dt = (float)delta;
		_age += dt;
		if (Lifetime > 0f && _age > Lifetime) { QueueFree(); return; }
		Visible = Active;
		_mesh.Scale = new Vector3(Radius, 0.4f, Radius);
		_mesh.RotateY(dt * 2f);
		if (_border != null)
		{
			_border.Scale = new Vector3(Radius, .4f, Radius);
			_borderMaterial!.AlbedoColor = new Color(1f, .83f, .22f, .65f + .2f * Mathf.Sin(_age * 6f));
		}
		if (!Active) { _timer = 0f; return; }
		_timer += dt;
		if (_timer < Mathf.Max(0.05f, Pulse == null ? Runtime.Value(Kind, "TickInterval") : TickInterval)) return;
		_timer = 0f; PulseCount++;
		if (Kind == K.ThunderPool)
			SpecializationFx.Line(this, GlobalPosition + new Vector3(Radius * .7f, 1.4f, 0), GlobalPosition + new Vector3(Radius * .7f, .15f, 0), new Color(1f, .9f, .3f));
		var targets = Runtime.Enemies(GlobalPosition, Radius).ToArray();
		foreach (Unit target in targets)
		{
			if (Pulse != null) Pulse(target);
			else if (Kind == K.ThunderPool) Runtime.Thunder(target, Runtime.Value(Kind, "Damage"));
			else
			{
				var attack = new AttackInstance { Attacker = SourceUnit, Ability = "Whirlwind", AbilityKind = Kind.ToString(), BaseDamage = Runtime.Value(Kind, "Damage") * AttackOffense.Capture(SourceUnit).DamageMultiplier, IsSecondary = true };
				target.ReceiveHit(attack.RollHit(target, Vector3.Zero, target.GlobalPosition));
			}
		}
	}
}
