using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>有上限的精英危险调度；几何同时决定预警、表现与命中，不创建逐帧物理触发器。</summary>
public partial class EliteHazardWorld : Node3D
{
	public int Capacity { get; set; } = 24;
	public int PerSourceCapacity { get; set; } = 6;
	public float HitInterval { get; set; } = .5f;
	public int AcceptedCount { get; private set; }
	public int DamageCount { get; private set; }
	public int RejectedCount { get; private set; }
	public int ActiveCount { get { int count = 0; foreach (var h in _hazards) if (!h.IsDisposed) count++; return count; } }
	public bool DamageSuspended { get; private set; }
	public IReadOnlyList<EliteHazardHandle> Hazards => _hazards;
	public float Time { get; private set; }
	private readonly List<EliteHazardHandle> _hazards = new();
	private readonly Dictionary<ulong, float> _controlProtectedUntil = new();
	private readonly List<(Unit Target, string Id, Unit? Source)> _appliedStatuses = new();
	private int _generation;
	private string StatusSource => "EliteHazardWorld:" + GetInstanceId();

	public EliteHazardHandle? Circle(Unit source, Vector3 center, float radius, float damage, float warning, float duration, bool groundOnly = false, StatusEffect? status = null)
		=> Add(source, center, center, 0, radius, damage, warning, duration, groundOnly, status, false);
	public EliteHazardHandle? Ring(Unit source, Vector3 center, float innerRadius, float outerRadius, float damage, float warning, float duration, bool groundOnly = false, StatusEffect? status = null)
		=> Add(source, center, center, innerRadius, outerRadius, damage, warning, duration, groundOnly, status, false);
	public EliteHazardHandle? Line(Unit source, Vector3 start, Vector3 end, float width, float damage, float warning, float duration, StatusEffect? status = null)
		=> Add(source, start, end, 0, width * .5f, damage, warning, duration, false, status, true);
	public EliteHazardHandle? Polyline(Unit source, IReadOnlyList<Vector3> points, float width, float damage, float warning, float duration)
	{
		if (points.Count < 2) return null;
		var h = Line(source, points[0], points[1], width, damage, warning, duration);
		if (h == null) return null;
		h.Points = new Vector3[Math.Min(40, points.Count)];
		for (int i = 0; i < h.Points.Length; i++) h.Points[i] = points[i];
		return h;
	}
	private EliteHazardHandle? Add(Unit source, Vector3 center, Vector3 end, float inner, float outer, float damage, float warning, float duration, bool groundOnly, StatusEffect? status, bool line)
	{
		if (groundOnly) center = GroundPoint(center);
		if (!IsInsideTree() || !IsInstanceValid(source) || !center.IsFinite() || !end.IsFinite() || !float.IsFinite(outer) || outer <= 0 || !float.IsFinite(duration) || duration <= 0 || line && new Vector2(end.X - center.X, end.Z - center.Z).LengthSquared() < .0001f) return null;
		int sourceCount = 0; foreach (var h in _hazards) if (!h.IsDisposed && h.SourceId == source.GetInstanceId()) sourceCount++;
		if (ActiveCount >= Math.Max(1, Capacity) || sourceCount >= Math.Max(1, PerSourceCapacity)) { RejectedCount++; return null; }
		var hazard = new EliteHazardHandle(this, source, center, end, Mathf.Clamp(inner, 0, outer), outer, Mathf.Max(0, damage), Mathf.Max(.05f, warning), duration, groundOnly, status, line);
		_hazards.Add(hazard); AddChild(hazard.Visual); AcceptedCount++; return hazard;
	}
	public void SuspendDamage(bool suspended) => DamageSuspended = suspended;
	public void Clear()
	{
		_generation++;
		foreach (var hazard in _hazards) hazard.Dispose();
		_hazards.Clear(); _controlProtectedUntil.Clear();
		foreach (var applied in _appliedStatuses)
		{
			if (!IsInstanceValid(applied.Target)) continue;
			foreach (var active in applied.Target.Statuses)
				if (active.Effect.Id == applied.Id && ReferenceEquals(active.Source, applied.Source) && active.SourceAbilityKind == StatusSource)
				{ applied.Target.RemoveStatus(applied.Id); break; }
		}
		_appliedStatuses.Clear();
	}
	public override void _ExitTree() => Clear();
	public override void _PhysicsProcess(double delta)
	{
		if (DamageSuspended) return;
		float dt = (float)delta; Time += dt; int generation = _generation;
		for (int i = _hazards.Count - 1; i >= 0; i--)
		{
			var hazard = _hazards[i];
			if (hazard.IsDisposed) { _hazards.RemoveAt(i); continue; }
			if (IsInstanceValid(hazard.Source) && !hazard.Source.IsDead && hazard.Source.ImpactPause?.IsHolding == true) continue;
			float previousAge = hazard.Age; hazard.Age += dt;
			bool expired = hazard.Age >= hazard.Warning + hazard.Duration;
			if (expired && previousAge >= hazard.Warning + hazard.Duration) { hazard.Dispose(); _hazards.RemoveAt(i); continue; }
			if (hazard.IsActive && hazard.AngularSpeed != 0 && hazard.BeamLength > 0)
			{
				hazard.Angle += dt * hazard.AngularSpeed;
				hazard.End = hazard.Center + new Vector3(Mathf.Cos(hazard.Angle), 0, Mathf.Sin(hazard.Angle)) * hazard.BeamLength;
			}
			if (hazard.IsLine && hazard.Points == null) hazard.ClipStraightBeam();
			hazard.Draw();
			if (!hazard.Enabled || hazard.Age < hazard.Warning) continue;
			foreach (Node node in GetTree().GetNodesInGroup(hazard.SourceFaction == Faction.Enemy ? "player_units" : "enemy_units"))
			{
				if (node is not Unit target || !IsInstanceValid(target) || target.IsDead || target.IsQueuedForDeletion()) continue;
				ulong id = target.GetInstanceId();
				if (hazard.NextHit.TryGetValue(id, out float next) && Time < next) continue;
				if (!hazard.Contains(target, out Vector3 origin) || !HasSight(origin + Vector3.Up * .7f, Projectile.GetTargetCenter(target))) continue;
				hazard.NextHit[id] = Time + Mathf.Max(.1f, HitInterval);
				Unit? attacker = IsInstanceValid(hazard.Source) ? hazard.Source : null;
				var result = target.ReceiveHit(new HitInstance { Attacker = attacker, Target = target, Attack = hazard.Attack, Context = EventContext.Create(attacker, target), Damage = hazard.Attack.BaseDamage, Position = Projectile.GetTargetCenter(target), Direction = (target.GlobalPosition - origin).Normalized(), Knockback = hazard.Knockback });
				// 致死可同步触发导演清场或奖励暂停，不能继续访问被清空的集合。
				if (generation != _generation || DamageSuspended || IsQueuedForDeletion()) return;
				if (result.ArmorLost + result.ShieldLost + result.HealthLost <= 0 || result.Invulnerable) continue;
				DamageCount++;
				if (hazard.Status is not { } status || target.IsDead) continue;
				if (status.IsControl && _controlProtectedUntil.TryGetValue(id, out float protectedUntil) && Time < protectedUntil) continue;
				if (status.IsControl) _controlProtectedUntil[id] = Time + Mathf.Max(2.5f, status.Duration + 1.5f);
				target.ApplyStatus(status, attacker, StatusSource);
				bool recorded = false; foreach (var old in _appliedStatuses) if (old.Target == target && old.Id == status.Id && ReferenceEquals(old.Source, attacker)) { recorded = true; break; }
				if (!recorded) _appliedStatuses.Add((target, status.Id, attacker));
			}
			if (expired) { hazard.Dispose(); _hazards.RemoveAt(i); }
		}
	}
	public bool HasSight(Vector3 start, Vector3 end)
	{
		if (!IsInsideTree() || !start.IsFinite() || !end.IsFinite()) return false;
		if (start.DistanceSquaredTo(end) < .0001f) return true;
		using var query = PhysicsRayQueryParameters3D.Create(start, end, CombatLayers.World);
		query.HitFromInside = true;
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0;
	}
	/// <summary>地板取真实地形碰撞面；导航表面只负责可达性，不能当作脚底高度。</summary>
	public Vector3 GroundPoint(Vector3 point)
	{
		if (!IsInsideTree()) return point;
		using var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 1.5f, point + Vector3.Down * 12, CombatLayers.World);
		using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector3() : point;
	}
	internal static Vector3 FeetOf(Unit target)
	{
		foreach (Node child in target.GetChildren())
		{
			if (child is not CollisionShape3D { Disabled: false, Shape: not null } shape) continue;
			Vector3 half = shape.Shape switch
			{
				CapsuleShape3D capsule => new(capsule.Radius, capsule.Height * .5f, capsule.Radius),
				SphereShape3D sphere => Vector3.One * sphere.Radius,
				BoxShape3D box => box.Size * .5f,
				CylinderShape3D cylinder => new(cylinder.Radius, cylinder.Height * .5f, cylinder.Radius),
				_ => Vector3.Zero
			};
			Basis basis = shape.GlobalBasis;
			float extent = Mathf.Abs(basis.X.Y) * half.X + Mathf.Abs(basis.Y.Y) * half.Y + Mathf.Abs(basis.Z.Y) * half.Z;
			return shape.GlobalPosition - Vector3.Up * extent;
		}
		return target.GlobalPosition;
	}
}

/// <summary>危险句柄不拥有场景逻辑；关闭后立即停止伤害，视觉延迟释放也不能再命中。</summary>
public sealed class EliteHazardHandle : IDisposable
{
	internal readonly EliteHazardWorld World;
	internal readonly Unit Source;
	internal readonly ulong SourceId;
	internal readonly Faction SourceFaction;
	internal readonly AttackInstance Attack;
	internal readonly StatusEffect? Status;
	internal readonly Dictionary<ulong, float> NextHit = new();
	internal readonly MeshInstance3D Visual;
	internal Vector3[]? Points;
	internal float Age;
	internal readonly float Warning, Duration, Inner, Outer;
	internal readonly bool GroundOnly, IsLine;
	public Vector3 Center { get; set; }
	private Vector3 _end;
	/// <summary>读取预警和伤害共用的裁切终点；写入时保留未受阻挡的期望终点。</summary>
	public Vector3 End { get => _end; set { _end = value; RequestedEnd = value; } }
	public Vector3 RequestedEnd { get; private set; }
	public bool Enabled { get; set; } = true;
	public bool IsDisposed { get; private set; }
	public bool IsActive => !IsDisposed && Enabled && Age >= Warning;
	public Color Tint { get; set; } = new(1, .16f, .09f, .85f);
	public bool Orb { get; set; }
	public float Knockback { get; set; }
	public float AngularSpeed { get; set; }
	public float BeamLength { get; set; }
	public float Angle { get; set; }
	private float _drawTime;
	private readonly ImmediateMesh _mesh = new();
	private MeshInstance3D? _orb;
	internal EliteHazardHandle(EliteHazardWorld world, Unit source, Vector3 center, Vector3 end, float inner, float outer, float damage, float warning, float duration, bool groundOnly, StatusEffect? status, bool line)
	{
		World = world; Source = source; SourceId = source.GetInstanceId(); SourceFaction = source.Faction;
		Center = center; End = end; Inner = inner; Outer = outer; Warning = warning; Duration = duration; GroundOnly = groundOnly; Status = status; IsLine = line;
		Attack = new AttackInstance { Attacker = source, Ability = "EliteHazard", AbilityKind = "EliteHazard", BaseDamage = damage * source.Stats.Get("DamageMultiplier"), IsSecondary = true, IsDamageOverTime = duration > .3f, Tags = new[] { "EliteHazard", "Secondary" } };
		Visual = new MeshInstance3D { Name = "EliteHazard", Mesh = _mesh, MaterialOverride = EliteTraitVisual.Material(Colors.White, true), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
	}
	internal bool Contains(Unit target, out Vector3 origin)
	{
		origin = Center; Vector3 feet = EliteHazardWorld.FeetOf(target);
		if (GroundOnly && (Mathf.Abs(feet.Y - Center.Y) > .32f || target.Movement is { IsGrounded: false })) return false;
		if (IsLine)
		{
			float best = float.MaxValue;
			if (Points is { Length: > 1 }) for (int i = 1; i < Points.Length; i++) Closest(feet, Points[i - 1], Points[i], ref best, ref origin);
			else Closest(feet, Center, End, ref best, ref origin);
			return best <= Outer * Outer && Mathf.Abs(feet.Y - origin.Y) <= 1.4f;
		}
		float flat = new Vector2(feet.X - Center.X, feet.Z - Center.Z).LengthSquared();
		return Mathf.Abs(feet.Y - Center.Y) <= 1.5f && flat <= Outer * Outer && flat >= Inner * Inner;
	}
	internal void ClipStraightBeam()
	{
		_end = RequestedEnd;
		Vector3 offset = RequestedEnd - Center;
		if (offset.LengthSquared() < .0001f || !World.IsInsideTree()) return;
		using var query = PhysicsRayQueryParameters3D.Create(Center + Vector3.Up * .7f, RequestedEnd + Vector3.Up * .7f, CombatLayers.World);
		query.HitFromInside = true;
		using var hit = World.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return;
		Vector3 contact = hit["position"].AsVector3() - Vector3.Up * .7f;
		float length = Center.DistanceTo(contact);
		_end = Center + offset.Normalized() * Mathf.Max(0, length - .025f);
		// 立即重画阻挡变化，不能在裁切后留下上一帧越墙的预警形状。
		_drawTime = 0;
	}
	private static void Closest(Vector3 target, Vector3 a, Vector3 b, ref float best, ref Vector3 origin)
	{
		Vector2 flat = new(target.X, target.Z), start = new(a.X, a.Z), end = new(b.X, b.Z);
		Vector2 axis = end - start; float t = axis.LengthSquared() < .0001f ? 0 : Mathf.Clamp((flat - start).Dot(axis) / axis.LengthSquared(), 0, 1);
		float distance = flat.DistanceSquaredTo(start + axis * t);
		if (distance < best) { best = distance; origin = a.Lerp(b, t); }
	}
	internal void Draw()
	{
		if (!GodotObject.IsInstanceValid(Visual)) return;
		Visual.Visible = Enabled;
		if (!Enabled || Age < _drawTime) return; _drawTime = Age + .05f;
		Color color = IsActive ? Tint : new Color(1, .65f, .14f, .72f);
		if (IsLine && Points == null && new Vector2(End.X - Center.X, End.Z - Center.Z).LengthSquared() < .0001f) { Visual.Visible = false; return; }
		_mesh.ClearSurfaces(); _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		if (IsLine)
		{
			if (Points is { Length: > 1 }) for (int i = 1; i < Points.Length; i++) Strip(Points[i - 1], Points[i], Outer, color);
			else Strip(Center, End, Outer, color);
		}
		else
		{
			float inner = Inner; Color fill = new(color, IsActive ? .13f : .075f);
			Annulus(Center, inner, Outer, fill); Annulus(Center, Mathf.Max(inner, Outer - .055f), Outer, color);
			if (inner > .1f) Annulus(Center, inner, inner + .055f, color);
			if (GroundOnly)
			{
				for (int i = 0; i < 6; i++)
				{
					float angle = i * Mathf.Tau / 6; Vector3 p = Center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Outer * .6f;
					Triangle(p + Vector3.Left * .11f, p + Vector3.Right * .11f, p + Vector3.Up * .22f, color);
				}
			}
		}
		_mesh.SurfaceEnd();
		if (Orb)
		{
			if (_orb == null) { _orb = new MeshInstance3D { Mesh = new SphereMesh { Radius = Mathf.Min(.32f, Outer), Height = Mathf.Min(.64f, Outer * 2) }, MaterialOverride = EliteTraitVisual.Material(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; Visual.AddChild(_orb); }
			_orb.GlobalPosition = Center + Vector3.Up * .75f;
		}
	}
	private void Annulus(Vector3 center, float inner, float outer, Color color)
	{
		for (int i = 0; i < 48; i++)
		{
			float a = i * Mathf.Tau / 48, b = (i + 1) * Mathf.Tau / 48;
			Vector3 p = new(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new(Mathf.Cos(b), 0, Mathf.Sin(b));
			Vector3 c = center + Vector3.Up * .055f;
			Triangle(c + p * inner, c + q * outer, c + p * outer, color); Triangle(c + p * inner, c + q * inner, c + q * outer, color);
		}
	}
	private void Strip(Vector3 a, Vector3 b, float halfWidth, Color color)
	{
		Vector3 axis = b - a; axis.Y = 0; if (axis.LengthSquared() < .0001f) return;
		Vector3 side = axis.Normalized().Cross(Vector3.Up) * halfWidth; a += Vector3.Up * .06f; b += Vector3.Up * .06f;
		Color fill = new(color, IsActive ? .28f : .12f);
		Triangle(a - side, b + side, a + side, fill); Triangle(a - side, b - side, b + side, fill);
		// 空中的细束与地面范围共用端点，能够辨认真实连接对象。
		Vector3 up = Vector3.Up * .8f, thin = side.Normalized() * .045f;
		Triangle(a + up - thin, b + up + thin, a + up + thin, color); Triangle(a + up - thin, b + up - thin, b + up + thin, color);
	}
	private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
	{
		_mesh.SurfaceSetColor(color); _mesh.SurfaceAddVertex(a); _mesh.SurfaceAddVertex(b); _mesh.SurfaceAddVertex(c);
	}
	public void Dispose()
	{
		if (IsDisposed) return; IsDisposed = true; Enabled = false; NextHit.Clear();
		if (GodotObject.IsInstanceValid(Visual)) { Visual.Visible = false; Visual.QueueFree(); }
	}
}

/// <summary>薄罩和细环只负责表现；不会改变身体材质、碰撞或战斗判定。</summary>
public static class EliteTraitVisual
{
	public static StandardMaterial3D Material(Color color, bool vertexColor = false) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		AlbedoColor = color, VertexColorUseAsAlbedo = vertexColor, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		EmissionEnabled = !vertexColor, Emission = vertexColor ? Colors.Black : color, EmissionEnergyMultiplier = .8f,
	};
	public static Node3D Aura(Unit owner, float radius, Color color, bool dome = false)
	{
		var aura = new Node3D { Name = "EliteTraitAura" }; owner.AddChild(aura);
		Vector3 scale = owner.GlobalBasis.Scale.Abs(); aura.Scale = new Vector3(1 / Mathf.Max(.1f, scale.X), 1 / Mathf.Max(.1f, scale.Y), 1 / Mathf.Max(.1f, scale.Z));
		aura.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = Mathf.Max(.05f, radius - .04f), OuterRadius = radius, Rings = 48, RingSegments = 4 }, Position = Vector3.Up * .08f, Scale = new Vector3(1, .12f, 1), MaterialOverride = Material(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		if (dome) aura.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 32, Rings = 16 }, Position = Vector3.Up * .35f, MaterialOverride = UnitRimOverlay.Create(color, .3f, .6f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
		return aura;
	}
}
