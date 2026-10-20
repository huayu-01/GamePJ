using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>每次攻击独立持有预制体指示器，只接收几何数据；不写移动、伤害或冷却。</summary>
public partial class EnemyAttackWarning : Node3D
{
	public SkillIndicator Indicator { get; private set; } = null!;
	public override void _Ready()
	{
		Indicator = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/SkillIndicator.tscn").Instantiate<SkillIndicator>();
		Indicator.IsHostile = true; Indicator.Name = "Indicator"; AddChild(Indicator);
	}
	public static EnemyAttackWarning? Create(Node parent, Unit? owner)
	{
		if (owner?.Faction != Faction.Enemy) return null;
		var warning = new EnemyAttackWarning { Name = "AttackWarning" }; parent.AddChild(warning); return warning;
	}
	public void HideAll() => Indicator.HideAll();
	public void Circle(Vector3 center, float radius) => Indicator.ShowAreaPoint(center, radius);
	public void SweepCircle(Vector3 center, Vector3 direction, float distance, float radius)
	{
		Vector3 flat = new Vector3(direction.X, 0, direction.Z).Normalized();
		if (flat.LengthSquared() < .001f) flat = Vector3.Forward;
		Indicator.ShowLineArea(center - flat * radius, flat, Mathf.Max(0, distance) + radius * 2, radius * 2);
	}
	/// <summary>读取实际命中体与世界缩放，圆用圆、盒用矩形；不能把触发距离当伤害范围。</summary>
	public void Hitbox(AttackHitbox hitbox, Vector3? translatedCenter = null, Vector3? sweepDirection = null, float sweepDistance = 0)
	{
		foreach (Node child in hitbox.GetChildren())
		{
			if (child is not CollisionShape3D { Disabled: false, Shape: { } shape } collider) continue;
			Vector3 center = translatedCenter ?? collider.GlobalPosition;
			Vector3 scale = collider.GlobalBasis.Scale.Abs();
			if (shape is SphereShape3D sphere)
			{
				float radius = sphere.Radius * Mathf.Max(scale.X, scale.Z);
				if (sweepDirection is { } direction) SweepCircle(center, direction, sweepDistance, radius);
				else Circle(center, radius);
			}
			else if (shape is BoxShape3D box)
			{
				Vector3 direction = sweepDirection ?? -collider.GlobalBasis.Z.Normalized();
				direction.Y = 0; direction = direction.Normalized();
				Vector3 side = direction.Cross(Vector3.Up);
				Vector3 x = collider.GlobalBasis.X * box.Size.X * .5f;
				Vector3 z = collider.GlobalBasis.Z * box.Size.Z * .5f;
				float depth = Mathf.Abs(x.Dot(direction)) + Mathf.Abs(z.Dot(direction));
				float width = 2 * (Mathf.Abs(x.Dot(side)) + Mathf.Abs(z.Dot(side)));
				Indicator.ShowLineArea(center - direction * depth, direction, depth * 2 + Mathf.Max(0, sweepDistance), width);
			}
			return;
		}
		HideAll();
	}
	/// <summary>当前速度下的预计碰地／碰墙／航程终点；仅用于画图，真实命中仍由投射物结算。</summary>
	public static Vector3 PredictImpact(Node3D source, Vector3 origin, Vector3 velocity, float gravity, float maxDistance, float maxSeconds, uint mask)
	{
		Vector3 previous = origin; float travelled = 0;
		var space = source.GetWorld3D().DirectSpaceState;
		float lifetime = Mathf.Clamp(maxSeconds, .01f, 12);
		for (float time = .05f; time <= lifetime + .049f; time += .05f)
		{
			float t = Mathf.Min(time, lifetime);
			// 近似60Hz半隐式重力，与实际弹道同一重力符号；采样仅供预览。
			Vector3 next = origin + velocity * t + Vector3.Down * gravity * .5f * t * (t + 1f / 60f);
			float length = previous.DistanceTo(next);
			bool exhausted = maxDistance > 0 && travelled + length >= maxDistance;
			if (exhausted) next = previous.MoveToward(next, Mathf.Max(0, maxDistance - travelled));
			using var query = PhysicsRayQueryParameters3D.Create(previous, next, mask);
			using var hit = space.IntersectRay(query);
			if (hit.Count > 0) return hit["position"].AsVector3();
			travelled += length; previous = next;
			if (exhausted || t >= lifetime) break;
		}
		return previous;
	}
}
