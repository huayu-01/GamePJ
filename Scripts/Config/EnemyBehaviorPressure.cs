using System;
using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Config;

/// <summary>行为压力是生成资源成本，独立于抽取概率；精英词条附加成本暂为0。</summary>
[GlobalClass]
public partial class EnemyBehaviorPressure : Resource
{
	[Export] public ParamEntry[] Weights { get; set; } = Array.Empty<ParamEntry>();
	public bool TryCalculate(string[] behaviors, out float pressure)
	{
		pressure = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string key in behaviors)
		{
			if (!seen.Add(key)) continue;
			ParamEntry? found = Array.Find(Weights, x => x.Key == key);
			if (found == null || !float.IsFinite(found.AsFloat()) || found.AsFloat() < 0) return false;
			pressure += found.AsFloat();
		}
		return float.IsFinite(pressure);
	}
	public static int SpawnCost(string[] behaviors, int fallback, float eliteAffixPressure = 0)
	{
		if (behaviors.Length == 0) return Mathf.Max(1, fallback);
		var table = RuntimeResources.Load<EnemyBehaviorPressure>("res://Config/EnemyBehaviorPressure.tres");
		if (!table.TryCalculate(behaviors, out float pressure)) return int.MaxValue;
		return Mathf.Max(1, Mathf.CeilToInt(pressure + Mathf.Max(0, eliteAffixPressure)));
	}
}
