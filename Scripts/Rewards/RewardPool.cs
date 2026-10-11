using System.Collections.Generic;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 奖励候选池（执行文档 §8.4）：角色/武器确定后建立一次候选池，每次奖励只做加权抽取。
/// 过滤条件：武器标签兼容、层数上限、已持有互斥项。
/// </summary>
public sealed partial class RewardPool : RefCounted
{
	private readonly List<RewardDefinition> _candidates = new();
	private readonly RandomNumberGenerator _rng = new();

	public int CandidateCount => _candidates.Count;

	public void Initialize(Weapon weapon)
	{
		_candidates.Clear();
		foreach (RewardDefinition definition in RewardCatalog.All)
		{
			if (definition.AllowsWeapon(weapon))
			{
				_candidates.Add(definition);
			}
		}
	}

	/// <summary>当前角色是不是召唤控制（决定「亡者随从」这类限定奖励是否进池）。</summary>
	public bool SummonerAllowed { get; set; }

	public List<RewardDefinition> Roll(RunBuild build, int count, RewardKind? kindFilter = null, RewardKind[]? kindSet = null)
	{
		var available = new List<RewardDefinition>();
		foreach (RewardDefinition definition in _candidates)
		{
			// 地面节点分类型（强化池 / 进化池 / 异化池），所以抽取支持按 Kind 过滤；
			// kindSet 用于「稀有池」这种多类型限定（特殊奖励节点）。
			if (kindFilter.HasValue && definition.Kind != kindFilter.Value)
			{
				continue;
			}

			if (kindSet is { Length: > 0 } && System.Array.IndexOf(kindSet, definition.Kind) < 0)
			{
				continue;
			}

			// 角色限定：召唤类奖励只有召唤控制角色才进池。
			if (definition.RequiresSummoner && !SummonerAllowed)
			{
				continue;
			}

			// 互斥（双向）：候选排斥已拥有的，或已被拥有的奖励排斥。
			if (IsExcluded(build, definition))
			{
				continue;
			}

			if (definition.MaxStacks > 0 && build.StacksOf(definition.Id) >= definition.MaxStacks)
			{
				continue;
			}

			bool blocked = false;
			foreach (string conflict in definition.ConflictsWith)
			{
				if (build.Has(conflict))
				{
					blocked = true;
					break;
				}
			}

			if (!blocked)
			{
				available.Add(definition);
			}
		}

		var picked = new List<RewardDefinition>();
		for (int i = 0; i < count && available.Count > 0; i++)
		{
			float total = 0f;
			foreach (RewardDefinition definition in available)
			{
				total += Mathf.Max(0.01f, definition.Weight);
			}

			float roll = _rng.RandfRange(0f, total);
			int index = available.Count - 1;
			for (int j = 0; j < available.Count; j++)
			{
				roll -= Mathf.Max(0.01f, available[j].Weight);
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
	/// <summary>互斥判定（双向）：弹射与穿透这类不合理的组合不会同时出现。</summary>
	private bool IsExcluded(RunBuild build, RewardDefinition candidate)
	{
		foreach (string banned in candidate.Excludes)
		{
			if (build.StacksOf(banned) > 0)
			{
				return true;
			}
		}

		foreach (RewardDefinition owned in _candidates)
		{
			if (build.StacksOf(owned.Id) > 0 && System.Array.IndexOf(owned.Excludes, candidate.Id) >= 0)
			{
				return true;
			}
		}

		return false;
	}
}