using Godot;

namespace ActRoguelike.Combat;

/// <summary>独立留场的透明罩；弹体用自身扫过的完整线段检验，高速弹也不能漏穿。</summary>
public partial class EnergyDomeField : Node3D, IProjectileBlockField
{
	public Unit OwnerUnit { get; set; } = null!;
	public float Radius { get; set; } = 3;
	public float Lifetime { get; set; } = 2;
	public int DestroyedCount { get; private set; }
	private float _age;
	public override void _Ready()
	{
		TopLevel = true; SetProcess(false); ProjectileInfluenceFields.RegisterBlocker(this);
		var material = new StandardMaterial3D { AlbedoColor = new Color(.3f, .8f, 1, .1f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true, Emission = new Color(.1f, .35f, .5f) };
		AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = Radius, Height = Radius * 2, RadialSegments = 32, Rings = 16 },
			MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		GlobalPosition = Targeting.AimPoint(OwnerUnit);
	}
	public bool BlocksProjectile(Projectile projectile, Vector3 from, Vector3 to)
	{
		if (IsQueuedForDeletion() || OwnerUnit.IsDead || projectile.CurrentAttack?.Attacker is not { } attacker || !OwnerUnit.IsHostileTo(attacker) || !projectile.CanBeDestroyed) return false;
		Vector3 line = to - from; float t = line.LengthSquared() > .000001f ? Mathf.Clamp((GlobalPosition - from).Dot(line) / line.LengthSquared(), 0, 1) : 0;
		if ((from + line * t).DistanceSquaredTo(GlobalPosition) > Radius * Radius) return false;
		DestroyedCount++; return true;
	}
	public override void _PhysicsProcess(double delta)
	{
		_age += (float)delta; if (!IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead || _age >= Lifetime) { QueueFree(); return; }
		GlobalPosition = Targeting.AimPoint(OwnerUnit);
	}
	public override void _ExitTree() => ProjectileInfluenceFields.RemoveBlocker(this);
}
