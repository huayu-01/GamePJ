using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 一个 Stage 的配置（策划文档 §6.2：Stage 之间靠敌人强度与进度区分）。
/// 每一关复用同一个 Stage 场景，只有这里的数值不同，代码不认具体关卡。
/// </summary>
[Tool]
[GlobalClass]
public partial class StageDefinition : Resource
{
	/// <summary>
	/// Stage 的稳定键（关卡核心文档 §7.4「Stage 标识」）。存档 / 分支 / 别的表引用都用它，
	/// 定好不再改；与行文件名（<c>Config/Tables/Stages/stg_*.tres</c>）取同一个语义名便于人肉对照。
	/// </summary>
	[Export] public string Id { get; set; } = "stage_1";

	[Export] public string DisplayName { get; set; } = "Stage 1";

	[ExportGroup("战斗")]
	[Export] public float CombatBudget { get; set; } = 10f;
	[Export] public int MaxConcurrent { get; set; } = 3;
	[Export] public float SpawnInterval { get; set; } = 2f;
	[Export] public float EnemyHealthMultiplier { get; set; } = 1f;
	[Export] public float EnemyDamageMultiplier { get; set; } = 1f;

	/// <summary>精英出现概率的额外缩放（加在各生成项自己的 EliteChance 上）。</summary>
	[Export] public float EliteChanceScale { get; set; }

	/// <summary>
	/// 本关允许生成的敌人（写敌人表的行 Id，如 <c>enemy_chaser</c>；留空 = 不限制）。
	///
	/// ⚠️ 这里以前按**中文显示名**匹配（`"基础近战"` 这种），改一个文案就静默放行/屏蔽敌人 ——
	/// 现在按敌人表的稳定键 <see cref="ConfigRow.Id"/> 匹配，显示名只用于界面与日志。
	/// 未知 Id 会在开局由 <see cref="Balance"/> 校验并写警告（不再静默失效）。
	/// </summary>
	[Export] public string[] AllowedEnemyIds { get; set; } = System.Array.Empty<string>();

	[ExportGroup("节点与挑战")]
	[Export] public int RewardNodeCount { get; set; } = 3;
	[Export] public int FreeChallengeQuota { get; set; } = 2;

	[ExportGroup("Boss")]
	[Export] public int BossGuardCount { get; set; } = 2;
	[Export] public float BossHealthMultiplier { get; set; } = 1f;
	[Export] public float BossDamageMultiplier { get; set; } = 1f;

	/// <summary>Boss 词条数量（策划文档 §12 Boss 构筑）。</summary>
	[Export] public int BossPerkCount { get; set; } = 2;

	/// <summary>
	/// Boss Pressure 的 Stage 倍率表（关卡核心文档 §7.4：Stage 倍率使用固定表、不随机生成；
	/// 「具体倍率数值暂未最终确定，不能在核心代码中散落硬编码常量」）。
	///
	/// <b>取值规则（只有一条，避免出现两个真源）</b>：
	/// 行里**填了**就用行里的（行是权威）；行里**留 0**（= 未设置）则回退到
	/// <see cref="BalanceConfig"/> 上已有的全局默认值（150 / 1.25 / 1.5 / 100）。
	/// 所以：① 没填 → 与分表之前（只读 Balance）逐位一致；② 缺一整张表 → 用本类默认值
	/// （也就是"未设置"）→ 仍然落到 Balance 的全局默认值，不会静默换一套数值（§1.4-① 的教训）；
	/// ③ 想按关调，只在这一行填，不必动 Balance。
	///
	/// ⚠️ 这些字段**只描述数值型强化**（Boss Pressure）。行为/技能型强化属 Boss Build
	/// （<see cref="ActRoguelike.Boss.BossPerkCatalog"/>），关卡核心文档 §7.1 明令两者不得合并成一个池。
	/// </summary>
	[ExportGroup("Boss Pressure（关卡核心文档 §7.4）")]
	/// <summary>Pressure 增长倍率（§7.2「不同 Stage 使用固定的 Stage 倍率表进行调整，倍率必须可配置」）。&lt;= 0 → 按 1 处理（等于分表之前的乘数）。</summary>
	[Export] public float PressureGrowthMultiplier { get; set; } = 1f;

	/// <summary>Pressure 阈值：每满这么多点转成 1 点首领压力（§7.3；余数清零不结转）。&lt;= 0 → 用 Balance 的 PressurePerPoint。</summary>
	[Export] public int PressureThreshold { get; set; }

	/// <summary>
	/// 普通挑战（中/低价值节点）完成后增加的基础 Pressure（§7.4）。
	/// &lt;= 0 视为不加 —— 这正是分表之前的行为（只有 Layer &gt;= 2 的高价值节点加压力）。
	/// </summary>
	[Export] public float PressurePerNodeNormal { get; set; }

	/// <summary>高价值挑战（进化/异化节点）完成后增加的基础 Pressure（§7.4）。&lt;= 0 → 用 Balance 的 PressurePerHighValueNode。</summary>
	[Export] public float PressurePerNodeHighValue { get; set; }

	/// <summary>进化类挑战对 Pressure 增加值的额外比例（§7.2「进化…可以按规则对 Pressure 增加值进行额外比例调整」）。&lt;= 0 → 用 Balance 的 PressureEvolutionBonus。</summary>
	[Export] public float PressureEvolutionBonus { get; set; }

	/// <summary>异化类挑战对 Pressure 增加值的额外比例（同上）。&lt;= 0 → 用 Balance 的 PressureMutationBonus。</summary>
	[Export] public float PressureMutationBonus { get; set; }

	/// <summary>
	/// 本关 Pressure 转换后允许进入的**数值型**强化候选标签（§7.4「强化池权重或适用规则」）。
	///
	/// 取值优先级（只有一个真源）：节点行的 <see cref="NodeConfig.BossModifierTags"/>（挑战类型更具体）
	/// → 本字段（关卡级兜底）→ 两层都留空 = **不限制**（Pressure 表里每一行都是候选）。
	/// 命中规则 = 候选行 <c>PressureModConfig.Tags</c> 与本值**有交集**。
	///
	/// ⚠️ 只描述数值型强化（生命/伤害/攻速/移速这类，§7.5），**绝不包含行为或技能型词条** ——
	/// 那是 Boss Build 的职责，两者必须能独立配置、独立追踪、独立结算（§7.1）。
	/// 消费者是 <c>StageController.PickTags</c>（配置表阶段 6 起真的生效；在此之前它只进日志）。
	/// </summary>
	[Export] public string[] PressureUpgradeTags { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 本关允不允许某个敌人生成。按敌人表的行 Id 匹配（见 <see cref="AllowedEnemyIds"/>）。
	/// <paramref name="enemyId"/> 为空 = 敌人表整个读不到 —— 这时**不筛**（放全部敌人），
	/// 因为"整张表回退"的语义是不限制难度，而不是把本关变成空场（回退路径必须还能玩）。
	/// </summary>
	public bool AllowsEnemy(string? enemyId)
	{
		if (AllowedEnemyIds.Length == 0 || string.IsNullOrEmpty(enemyId))
		{
			return true;
		}

		return System.Array.IndexOf(AllowedEnemyIds, enemyId) >= 0;
	}
}
