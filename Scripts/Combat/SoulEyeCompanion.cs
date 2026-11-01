using System.Linq;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>只负责标记的独立留场物；不叠已有标记，不继承旧软启动后撤或镜头缩放。</summary>
public partial class SoulEyeCompanion : Node3D
{
	public Unit OwnerUnit { get; set; } = null!;
	public float Lifetime { get; set; } = 10;
	/// <summary>084：标记间隔改为 0.5 秒（用户 2026-11-01 口径）。</summary>
	public float Interval { get; set; } = .5f;
	public float MarkDuration { get; set; } = 6;
	public float Radius { get; set; } = 12;
	public string AbilityId { get; set; } = "";
	/// <summary>084：每次标记顺带射出一支灵魂箭，伤害 1，走真实攻击实例（可触发增幅）。</summary>
	public float ArrowDamage { get; set; } = 1f;
	public int MarkCount { get; private set; }
	/// <summary>已射出的灵魂箭数量（自测取证）。</summary>
	public int ArrowCount { get; private set; }
	private float _age, _timer;
	public override void _Ready()
	{
		TopLevel = true; SetProcess(false);
		// 双面眼形方便自由镜头辨识，不依赖角色骨骼或额外模型资产。
		AddPart(new Vector3(.32f, .16f, .1f), Vector3.Zero, new Color(.85f, .78f, 1));
		foreach (float side in new[] { -1f, 1f })
		{
			AddPart(new Vector3(.11f, .12f, .025f), new Vector3(0, 0, side * .095f), new Color(.8f, .08f, .2f));
			AddPart(new Vector3(.035f, .09f, .012f), new Vector3(0, 0, side * .12f), new Color(.05f, .02f, .1f));
		}
		GlobalPosition = OwnerUnit.GlobalPosition + Vector3.Up * 2.3f;
	}
	private void AddPart(Vector3 scale, Vector3 position, Color color) => AddChild(new MeshInstance3D
	{
		Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 20, Rings = 10 }, Scale = scale, Position = position,
		MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
	});
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta; _age += dt;
		if (!IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead || _age >= Lifetime) { QueueFree(); return; }
		GlobalPosition = OwnerUnit.GlobalPosition + Vector3.Up * (2.3f + Mathf.Sin(_age * 3) * .08f); _timer -= dt; if (_timer > 0) return;
		_timer = Mathf.Max(.05f, Interval);
		var candidates = GetTree().GetNodesInGroup("enemy_units").OfType<Unit>()
			.Where(u => u.IsValidTarget && OwnerUnit.IsHostileTo(u) && u.GetStatusStacks("Mark") == 0 && u.GlobalPosition.DistanceSquaredTo(OwnerUnit.GlobalPosition) <= Radius * Radius)
			.OrderBy(u => u.GlobalPosition.DistanceSquaredTo(OwnerUnit.GlobalPosition));
		foreach (var target in candidates)
		{
			using var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, Targeting.AimPoint(target), CombatLayers.World);
			using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query); if (hit.Count > 0) continue;
			if (StatusPresets.Create("Mark", MarkDuration) is { } mark) { target.ApplyStatus(mark, OwnerUnit, AbilityId); MarkCount++; }
			LookAt(Targeting.AimPoint(target)); SpecializationFx.Line(GetParent(), GlobalPosition, Targeting.AimPoint(target), new Color(1, .08f, .15f));
			ShootSoulArrow(target); break;
		}
	}

	/// <summary>
	/// 084 的「灵魂箭」：真实 <see cref="Projectile"/> + 真实 <see cref="AttackInstance"/>（伤害 1），
	/// 所以增幅／特化／暴击这些挂钩照常生效，不是假伤害。
	/// </summary>
	private void ShootSoulArrow(Unit target)
	{
		if (ArrowDamage <= 0f || !GodotObject.IsInstanceValid(target)) return;
		if (RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Arrow.tscn") is not { } scene
			|| scene.Instantiate<Projectile>() is not { } arrow) return;
		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		parent.AddChild(arrow);
		Vector3 origin = GlobalPosition;
		arrow.GlobalPosition = origin;
		Vector3 direction = (Targeting.AimPoint(target) - origin).Normalized();
		arrow.Speed = 24f;
		arrow.Pierce = 0;
		arrow.MaxLifetime = 3f;
		arrow.Scale = Vector3.One * .7f;
		arrow.Launch(new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			AbilityKind = AbilityId,
			BaseDamage = ArrowDamage,
			CanCrit = true,
			CritChance = OwnerUnit.Stats.Get("CritChance"),
			CritMultiplier = OwnerUnit.Stats.Get("CritMultiplier"),
			Policy = HitPolicy.OncePerTarget,
			Lifetime = 3f,
			IsDirectAbilityAttack = true,
		}, direction);
		ArrowCount++;
	}
}
