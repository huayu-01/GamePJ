using System.Collections.Generic;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 奖励候选池（执行文档 §8.4）：角色/武器确定后建立一次候选池，每次奖励只做加权抽取。
///
/// <para><b>三层判定（P5）</b>——「池 = 按当前角色 + 当前武器过滤的祝福清单」（用户 2026-10-12 §0）：</para>
/// <list type="number">
/// <item><b>限定</b>（<b>进池时</b>判，一次）：角色层 <see cref="CharacterId"/> →
///   <c>RewardDefinition.AllowsCharacter</c>；武器层 → <c>AllowsWeapon</c>（专属池 → 武器标签 → 通用 三级回退）。</item>
/// <item><b>互斥</b>（<b>每次抽取</b>判）：<c>Excludes</c> 双向 / <c>ConflictsWith</c> 单向。</item>
/// <item><b>前置</b>（<b>每次抽取</b>判）：<c>Requires</c> 必须全部已拥有。</item>
/// </list>
/// <para>后两层天然要每次算（"你现在已经有什么"是每局的），但<b>候选项的来源集合不再变</b>（用户 C2）。</para>
/// </summary>
public sealed partial class RewardPool : RefCounted
{
	private readonly List<RewardDefinition> _candidates = new();
	private readonly RandomNumberGenerator _rng = new();

	public int CandidateCount => _candidates.Count;

	/// <summary>
	/// 当前角色的**表行 Id**（`chr_hunter` / `chr_weapon_master` / 旧三行 …），限定（角色层）读它。
	/// 空 = 角色未知 → 角色限定项不进池（`RewardDefinition.AllowsCharacter` 的语义）。
	/// </summary>
	public string CharacterId { get; set; } = string.Empty;

	/// <summary>
	/// 建候选池。`characterId` 给 null 表示"不改已有的 <see cref="CharacterId"/>" ——
	/// 这样既有的单参调用点（自测里手搓的池）一行都不用改。
	/// </summary>
	public void Initialize(Weapon weapon, string? characterId = null)
	{
		if (characterId != null)
		{
			CharacterId = characterId;
		}

		_candidates.Clear();
		foreach (RewardDefinition definition in RewardCatalog.All)
		{
			// 限定（角色层）：角色行 Id 不在名单里 → 根本不进池。
			if (!definition.AllowsCharacter(CharacterId))
			{
				continue;
			}

			// 限定（武器层）：专属池 → 武器标签 → 通用（三级回退，见 RewardDefinition.AllowsWeapon）。
			if (definition.AllowsWeapon(weapon))
			{
				_candidates.Add(definition);
			}
		}
	}

	// ⚠️ P6a 删掉了 `SummonerAllowed`（"当前角色是不是召唤控制"）—— 它唯一的消费者是
	// `RewardDefinition.RequiresSummoner`（= `mut_summon` 亡者随从）。角色限定统一走
	// `RewardDefinition.AllowsCharacter`（在 Initialize 里就筛掉，不是"抽到了没效果"）。

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

			// 互斥（双向）：候选排斥已拥有的，或已被拥有的奖励排斥。
			if (IsExcluded(build, definition))
			{
				continue;
			}

			// 前置：必须全部已拥有才进池（不满足 = 不出现，不是"拿了没效果"）。
			if (!HasPrerequisites(build, definition))
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

		// 加权抽样走公共工具（Config/WeightedPick.cs）。⚠️ 必须把本池自己的 _rng 传进去：
		// 抽公共工具之前这里是内联的同一段算法，换成共用一把 RNG 会让随机序列变、自测基线漂。
		return ActRoguelike.Config.WeightedPick.Pick(available, definition => definition.Weight, count, _rng);
	}
	/// <summary>
	/// **前置判定**（P5 三层判定的第三层）：<c>Requires</c> 里的 Id **全部**已拥有才放行。
	/// 与互斥是**镜像**的：互斥是"有了 X 就不能有本项"，前置是"没有 X 就不能有本项"。
	/// </summary>
	private static bool HasPrerequisites(RunBuild build, RewardDefinition candidate)
	{
		foreach (string required in candidate.Requires)
		{
			if (build.StacksOf(required) <= 0)
			{
				return false;
			}
		}

		return true;
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