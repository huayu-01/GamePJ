using System;
using ActRoguelike.Combat;
using ActRoguelike.Weapons;

namespace ActRoguelike.Rewards;

public enum RewardKind
{
	/// <summary>强化：主要改参数。</summary>
	Enhancement,

	/// <summary>进化：改武器核心行为，武器/武器家族限定。</summary>
	Evolution,

	/// <summary>异化：加新效果或新规则。</summary>
	Mutation,
}

/// <summary>应用奖励时能拿到的东西（策划文档 §9.3：角色决定候选范围，抽取规则决定最终出现）。</summary>
public sealed class RewardContext
{
	public required Unit Player { get; init; }
	public required Weapon Weapon { get; init; }
	public required RunBuild Build { get; init; }
}

/// <summary>
/// 一条奖励的定义。效果用委托声明，池子按标签过滤 —— 不在池代码里按奖励 Id 分支。
/// </summary>
public sealed class RewardDefinition
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string Description { get; init; }
	public RewardKind Kind { get; init; } = RewardKind.Enhancement;

	/// <summary>池标签（Gun / Projectile / KillTrigger / Healing / Movement …）。</summary>
	public string[] PoolTags { get; init; } = Array.Empty<string>();

	/// <summary>要求武器具备的标签；为空表示不限武器。</summary>
	public string[] RequiredWeaponTags { get; init; } = Array.Empty<string>();

	/// <summary>与这些 Id 互斥。</summary>
	public string[] ConflictsWith { get; init; } = Array.Empty<string>();

	public float Weight { get; init; } = 1f;

	/// <summary>0 = 不限层数。</summary>
	public int MaxStacks { get; init; } = 1;

	/// <summary>互斥的奖励 Id：已经拥有其中任意一个时，本奖励不再出现在池子里（双向判定）。</summary>
	public string[] Excludes { get; init; } = System.Array.Empty<string>();

	/// <summary>只有召唤控制角色才能拿到（例如「亡者随从」）。</summary>
	public bool RequiresSummoner { get; init; }

	public required Action<RewardContext> Apply { get; init; }

	public bool AllowsWeapon(Weapon weapon)
	{
		foreach (string tag in RequiredWeaponTags)
		{
			if (!weapon.HasTag(tag))
			{
				return false;
			}
		}

		return true;
	}
}
