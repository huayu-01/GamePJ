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

	/// <summary>
	/// 本项**专属的升级池 Id**（空 = 落到"标签匹配 / 通用"两层）。
	/// 由 <c>RewardTable</c> 编译磁盘行时从 <c>RewardConfig.Pool</c> 引用收集而来
	/// （不把 `Config.UpgradePoolConfig` 这个**磁盘类型**带进运行期视图，见 config-schema.md §4.5.4）。
	/// </summary>
	public string[] ApplicablePoolIds { get; init; } = Array.Empty<string>();

	/// <summary>要求武器具备的标签；为空表示不限武器。</summary>
	public string[] RequiredWeaponTags { get; init; } = Array.Empty<string>();

	/// <summary>
	/// **限定（角色层）**：只有当前角色的**行 Id** 在这里面时才进池；空 = 不限角色。
	/// 与 <see cref="ApplicablePoolIds"/>（武器层）/ <see cref="RequiredWeaponTags"/> 一起构成"限定"这一层 ——
	/// 由 <c>RewardConfig.RequiredCharacterIds</c> 填进来，见那里的注释（含 `&lt;A限定&gt;` vs `&lt;A1限定&gt;` 的写法）。
	/// </summary>
	public string[] RequiredCharacterIds { get; init; } = Array.Empty<string>();

	/// <summary>
	/// **前置**：必须**全部**已拥有，本项才进池（不满足 = 根本不出现）。空 = 无前置。
	/// 读取点是 <c>RewardPool.HasPrerequisites</c>（抽取时判，因为"你现在有什么"是每局的）。
	/// </summary>
	public string[] Requires { get; init; } = Array.Empty<string>();

	/// <summary>与这些 Id 互斥（**单向**：已拥有 → 本项不进池；由 <c>RewardConfig.ConflictsWith</c> 填入口）。</summary>
	public string[] ConflictsWith { get; init; } = Array.Empty<string>();

	public float Weight { get; init; } = 1f;

	/// <summary>0 = 不限层数。</summary>
	public int MaxStacks { get; init; } = 1;

	/// <summary>互斥的奖励 Id：已经拥有其中任意一个时，本奖励不再出现在池子里（双向判定）。</summary>
	public string[] Excludes { get; init; } = System.Array.Empty<string>();

	// ⚠️ P6a 删掉了 `RequiresSummoner`（唯一用户 `mut_summon` 已随旧角色一起清掉）。
	// 角色限定统一走 `RequiredCharacterIds` + `AllowsCharacter`（见 RewardPool.Initialize）。

	public required Action<RewardContext> Apply { get; init; }

	/// <summary>
	/// **三级回退**（设计文档 config-schema.md §4.5.3 的分层；方向按用户 2026-10 拍板改成"武器 → 池"）：
	///
	/// ① **专属池**：<see cref="ApplicablePoolIds"/> 非空 → 只有 <c>weapon.PoolId</c> 在其中时才允许
	///    （**不再看标签**：这一条只属于那个池，也就是只属于指向该池的武器）；
	/// ② **武器类别**：本项没指定池 → 逐个 <see cref="RequiredWeaponTags"/> 查 <c>weapon.HasTag</c>
	///    （现有 4 条进化走这层，行为与分表前**逐位一致**）；
	/// ③ **通用**：两者都为空 → 恒真（现有 10 条强化 + 5 条异化走这层）。
	///
	/// 武器行没填 Pool / 池表读不到 / 整份表没加载 → <c>weapon.PoolId</c> 为空 → 自动落到 ②③，
	/// 这就是"读不到则使用自身的默认值"在**关联**层面的同一套语义。
	/// </summary>
	public bool AllowsWeapon(Weapon weapon)
	{
		if (ApplicablePoolIds.Length > 0)
		{
			return !string.IsNullOrEmpty(weapon.PoolId)
				&& Array.IndexOf(ApplicablePoolIds, weapon.PoolId) >= 0;
		}

		foreach (string tag in RequiredWeaponTags)
		{
			if (!weapon.HasTag(tag))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// **限定 · 角色层**：当前角色行 Id 是否被本项接受。
	///
	/// <list type="bullet">
	/// <item>本项没填 <see cref="RequiredCharacterIds"/> → <b>恒真</b>（既有 19 行都在这一支，逐位不变）；</item>
	/// <item>填了而 <paramref name="characterId"/> 为空（角色表读不到 / 还没定角色）→ <b>false</b>
	///   —— "不知道你是谁就不给限定项"，而不是"给了没效果"（用户 2026-10-12 §1.3）。</item>
	/// </list>
	/// </summary>
	public bool AllowsCharacter(string? characterId)
	{
		if (RequiredCharacterIds.Length == 0)
		{
			return true;
		}

		return !string.IsNullOrEmpty(characterId)
			&& Array.IndexOf(RequiredCharacterIds, characterId) >= 0;
	}
}
