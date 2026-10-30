using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>投掷物的空间减速场；没有场时直接返回，重叠场取最强值而非反复乘速度。</summary>
public interface IProjectileSlowField
{
	float ProjectileSpeedMultiplier { get; }
	bool SlowsProjectileAt(Vector3 point);
}

public interface IProjectileBlockField
{
	bool BlocksProjectile(Projectile projectile, Vector3 from, Vector3 to);
}

public static class ProjectileInfluenceFields
{
	private static readonly List<IProjectileSlowField> Fields = new();
	private static readonly List<IProjectileBlockField> Blockers = new();
	public static void RegisterBlocker(IProjectileBlockField field) { if (!Blockers.Contains(field)) Blockers.Add(field); }
	public static void RemoveBlocker(IProjectileBlockField field) => Blockers.Remove(field);
	public static bool Blocks(Projectile projectile, Vector3 from, Vector3 to)
	{
		foreach (var blocker in Blockers) if (blocker.BlocksProjectile(projectile, from, to)) return true;
		return false;
	}
	public static void Register(IProjectileSlowField field) { if (!Fields.Contains(field)) Fields.Add(field); }
	public static void Remove(IProjectileSlowField field) => Fields.Remove(field);
	public static float SpeedMultiplier(Vector3 point)
	{
		float scale = 1;
		foreach (var field in Fields) if (field.SlowsProjectileAt(point)) scale = Mathf.Min(scale, Mathf.Clamp(field.ProjectileSpeedMultiplier, .05f, 1));
		return scale;
	}
}
