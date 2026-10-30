using Godot;
using ActRoguelike.World;

namespace ActRoguelike.Combat;

/// <summary>同次死亡的各掉落来源独立领取摆放点；不决定概率、种类或奖励数值。</summary>
public static class PickupDropLayout
{
	private const string PointsKey = "pickup_drop_points";
	public const float Spacing = .8f;
	public static Vector3 Claim(Unit? victim, Vector3 origin)
	{
		if (!GodotObject.IsInstanceValid(victim)) return origin;
		var points = victim!.HasMeta(PointsKey)
			? victim.GetMeta(PointsKey).AsGodotArray<Vector3>() : new Godot.Collections.Array<Vector3>();
		Vector3 chosen = origin;
		for (int attempt = 0; points.Count > 0 && attempt < 24; attempt++)
		{
			float angle = (points.Count + attempt) * 2.399963f;
			float radius = Spacing * (1 + attempt / 8);
			var candidate = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
			if (MapManager.Instance is { IsReady: true } map && !map.TrySnap(candidate, out candidate)) continue;
			bool clear = true;
			foreach (var existing in points)
				if ((existing with { Y = 0 }).DistanceSquaredTo(candidate with { Y = 0 }) < Spacing * Spacing * .9f) { clear = false; break; }
			if (clear) { chosen = candidate; break; }
		}
		points.Add(chosen); victim.SetMeta(PointsKey, points);
		return chosen;
	}
}
