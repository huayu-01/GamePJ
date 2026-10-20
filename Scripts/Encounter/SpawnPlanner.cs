using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Encounter;

/// <summary>
/// 刷怪规划（从 EncounterController 抽出的纯逻辑，不持有状态）。
///
/// 关卡核心文档 §波次：
/// - 每波 4–15 只、最多 1–3 种敌人类型；
/// - 一波**一次性**刷出，不要逐只滴灌；
/// - 落点必须可行走（交给 MapManager，以后程序化地图也走这里）。
/// </summary>
public static class SpawnPlanner
{
	public static bool TryFindSpawnPoint(Vector3 playerPosition, float minRadius, float maxRadius, out Vector3 point, ActRoguelike.Combat.Unit? unit = null)
	{
		point = default;
		return World.MapManager.Instance?.TryRingPoint(playerPosition, minRadius, maxRadius, out point, unit) == true;
	}
	/// <summary>每波数量下限 / 上限（文档 L241）。</summary>
	public const int MinWaveSize = 4;
	public const int MaxWaveSize = 15;

	/// <summary>随机决定这一波出多少只。</summary>
	public static int RollWaveSize(int min = MinWaveSize, int max = MaxWaveSize)
		=> GD.RandRange(min, Mathf.Max(min, max));

	/// <summary>
	/// 从候选里随机抽 1~maxTypes 种敌人类型（保证同一波不会只有一种以外的意外组合）。
	/// </summary>
	public static List<EnemySpawnEntry> PickTypes(List<EnemySpawnEntry> candidates, int maxTypes)
	{
		var picked = new List<EnemySpawnEntry>();
		if (candidates.Count == 0)
		{
			return picked;
		}

		var pool = new List<EnemySpawnEntry>(candidates);
		int types = GD.RandRange(1, Mathf.Clamp(maxTypes, 1, pool.Count));
		for (int i = 0; i < types && pool.Count > 0; i++)
		{
			int index = (int)(GD.Randi() % (uint)pool.Count);
			picked.Add(pool[index]);
			pool.RemoveAt(index);
		}

		return picked;
	}

	/// <summary>在玩家周围找一个可行走的刷怪点（走 MapManager，保证不刷进建筑/坡上）。</summary>
	public static Vector3 FindSpawnPoint(Vector3 playerPosition, float minRadius, float maxRadius)
	{
		if (World.MapManager.Instance is { } map)
		{
			return map.SpawnPoint(playerPosition, minRadius, maxRadius);
		}

		// 没有地图管理器时退回随机环点。
		float angle = (float)GD.RandRange(0d, Mathf.Tau);
		float radius = (float)GD.RandRange(minRadius, Mathf.Max(minRadius, maxRadius));
		return playerPosition + new Vector3(Mathf.Cos(angle) * radius, 0.6f, Mathf.Sin(angle) * radius);
	}
}
