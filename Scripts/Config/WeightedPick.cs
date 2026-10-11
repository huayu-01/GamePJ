using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 公共加权抽样（设计文档 config-schema.md §3 T9）：从一组行里按权重抽 count 个**不重复**的行。
///
/// ⚠️ <b>随机数发生器必须由调用方传进来</b>：合并之前，项目里有两处逐行重复的加权抽样
/// （<c>RewardPool.Roll</c> 用的是它自己的实例 RNG、<c>BossPerkCatalog.Roll</c> 用的是静态 RNG），
/// 两者**不是同一把 RNG**。如果抽出来的公共工具自己持有一把 RNG，随机序列就会变、自测基线会漂，
/// 所以这里不持有任何 RNG、不缓存任何状态：只做"给定 rng + 权重 → 抽 count 个"。
///
/// 算法与原两处实现逐行等价（同样的 <c>RandfRange(0, total)</c>、同样的"先扣权重再判 &lt;= 0"、
/// 同样的从最后一个下标开始兜底），因此把原来那把 rng 传进来，随机序列与改造前完全一致。
/// </summary>
public static class WeightedPick
{
	/// <summary>权重下限保护：权重为 0（或没填）的行仍然保留 1% 的机会，不会被彻底饿死。</summary>
	public const float MinWeight = 0.01f;

	/// <summary>
	/// 按权重抽 count 个不重复的行，返回顺序即抽取顺序。
	/// </summary>
	/// <param name="rows">候选行（顺序会决定同分时的结果，调用方负责传入稳定的顺序）。</param>
	/// <param name="weightOf">取某一行的权重。</param>
	/// <param name="count">最多抽几个（候选不够时少于 count）。</param>
	/// <param name="rng">调用方自己的 RNG —— 不要在这里 new 一个。</param>
	/// <param name="into">可选：把结果追加到这个列表里（RewardPool 就是这么用的）。</param>
	public static List<T> Pick<T>(IReadOnlyList<T> rows, Func<T, float> weightOf, int count, RandomNumberGenerator rng, List<T>? into = null)
	{
		var picked = into ?? new List<T>();
		var available = new List<T>(rows);

		for (int i = 0; i < count && available.Count > 0; i++)
		{
			float total = 0f;
			foreach (T row in available)
			{
				total += Mathf.Max(MinWeight, weightOf(row));
			}

			float roll = rng.RandfRange(0f, total);
			int index = available.Count - 1;
			for (int j = 0; j < available.Count; j++)
			{
				roll -= Mathf.Max(MinWeight, weightOf(available[j]));
				if (roll <= 0f)
				{
					index = j;
					break;
				}
			}

			picked.Add(available[index]);
			available.RemoveAt(index);
		}

		return picked;
	}
}
