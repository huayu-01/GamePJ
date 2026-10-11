using ActRoguelike.Core;
using System;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>
/// 词条的<b>限定词</b>（适用条件）—— 这条词条<b>对谁</b>生效。
///
/// <b>为什么需要它</b>：词条池里有"技能参数"词条（例如「尖啸回响」= 改 <c>AbilityScream</c> 的冷却）。
/// 不是每个首领都有尖啸、也不是每一关都该出现同一条强化，所以一条词条可以带几个条件：
/// <list type="bullet">
/// <item><see cref="BossIds"/>：只对某个首领生效（写单位行 Id，例如 <c>boss_final</c>；也接受场景路径兜底）。</item>
/// <item><see cref="StageIds"/>：只对某几关生效（写关卡行 Id，例如 <c>stg_core</c>；也接受关号 <c>"3"</c>）。</item>
/// <item><see cref="RequiredAbilityNode"/>：这个首领必须真的有这个技能节点（技能参数词条的隐含条件）。</item>
/// </list>
///
/// <b>刷新判定（用户明确要求，也是这套东西的关键）</b>：
/// <see cref="FailedReason"/> <b>每次调用都现算</b>，不缓存、不记忆上一次的结果 ——
/// 而且调用点在 <c>BossPerk.Apply</c> 里，也就是<b>首领出生的那一刻</b>（<c>EncounterController.SpawnBossNow</c>
/// 与 <c>StageController.ApplyPendingPressurePerks</c> 两条路都经 <c>Apply</c>）。
/// 所以"同一局里先抽到、后出生"不会复用抽签时的判断：抽签只筛<b>候选标签</b>（节点行/关卡行给的那层），
/// 限定词一律留到出生时判 —— 判不成立就<b>换抽</b>另一条（见 <c>BossPerkCatalog.ApplyWithRefresh</c>）。
///
/// ⚠️ 为什么不在抽签时判：`StageController.AddPressureForNode` 触发抽签时<b>首领还不存在</b>
/// （它可能在本关任何一次挑战里出生、也可能这一关根本不打首领），所以拿不到"这个首领有没有这个技能"
/// 这个事实；只有出生时才有 <see cref="BossPerkContext.Boss"/>。这也是文档里"转换在阈值、套用在出生"的分工。
/// </summary>
public sealed class PerkQualifier
{
	/// <summary>没有任何限定（= 所有首领、所有关卡都适用）。</summary>
	public static readonly PerkQualifier Any = new();

	/// <summary>只对哪些首领生效（单位行 Id / 场景路径）。空 = 不限首领。</summary>
	public string[] BossIds { get; init; } = Array.Empty<string>();

	/// <summary>只对哪几关生效（关卡行 Id，例如 <c>stg_inner</c>；也接受关号字符串 <c>"2"</c>）。空 = 不限关卡。</summary>
	public string[] StageIds { get; init; } = Array.Empty<string>();

	/// <summary>首领必须有这个能力节点（技能参数词条用它兜住"这个 Boss 没这个技能"）。空 = 不要求。</summary>
	public string RequiredAbilityNode { get; init; } = string.Empty;

	/// <summary>这条词条到底带没带限定词（决定要不要写判定日志 —— 不带就不写，免得刷屏）。</summary>
	public bool HasAny => BossIds.Length > 0 || StageIds.Length > 0 || !string.IsNullOrEmpty(RequiredAbilityNode);

#if DEBUG
	/// <summary>
	/// <b>自测专用</b>（<c>Scripts/Testing/SelfTest.cs</c>）：把"当前关卡身份"当成这一串来判。
	/// 用来在<b>同一次进程里</b>证明限定词会被重新判定（换个条件就换个结果），而不是复用上一次的结果。
	/// 正式代码从不写它（Release 构建里这个属性不存在，因为调用方也在 <c>#if DEBUG</c> 里）。
	/// 用完必须还原成 <c>null</c>。
	/// </summary>
	public static string? DebugStageIdOverride { get; set; }
#endif

	/// <summary>
	/// 判定 + 给出**不成立的理由**（null = 通过）。**每次调用都重新算**，这里没有任何缓存字段 ——
	/// 这就是"刷新判定"的实现（判定的时机由调用方决定：本项目是首领出生时）。
	/// </summary>
	public string? FailedReason(BossPerkContext context)
	{
		if (BossIds.Length > 0)
		{
			string identity = BossIdentityOf(context.Boss);
			if (Array.IndexOf(BossIds, identity) < 0)
			{
				return $"限定首领 [{string.Join(",", BossIds)}]，而这次出生的是「{identity}」";
			}
		}

		if (StageIds.Length > 0 && !IsCurrentStage())
		{
			return $"限定关卡 [{string.Join(",", StageIds)}]，而这一关是「{CurrentStageText()}」";
		}

		if (!string.IsNullOrEmpty(RequiredAbilityNode)
			&& context.Boss.GetNodeOrNull<Ability>(RequiredAbilityNode) == null)
		{
			return $"要求首领有这个技能节点，而 {context.Boss.Name} 上没有 {RequiredAbilityNode}";
		}

		return null;
	}

	/// <summary>给日志/自测用的一行描述（不带限定词时为"无"）。</summary>
	public string Describe()
	{
		if (!HasAny)
		{
			return "无";
		}

		var parts = new System.Collections.Generic.List<string>();
		if (BossIds.Length > 0)
		{
			parts.Add($"首领 [{string.Join(",", BossIds)}]");
		}

		if (StageIds.Length > 0)
		{
			parts.Add($"关卡 [{string.Join(",", StageIds)}]");
		}

		if (!string.IsNullOrEmpty(RequiredAbilityNode))
		{
			parts.Add($"技能节点 {RequiredAbilityNode}");
		}

		return string.Join(" · ", parts);
	}

	/// <summary>
	/// 这个首领的**身份**：单位行 Id 优先（表是权威），行读不到就退到场景路径，最后才是节点名。
	/// 用单位行 Id 而不是中文显示名 —— 改文案不该改变词条的适用性（与敌人白名单改按 Id 是同一条纪律）。
	/// </summary>
	public static string BossIdentityOf(Unit boss)
	{
		string scenePath = boss.SceneFilePath;
		if (ConfigRows.ByScenePath<BossConfig>(scenePath) is { Id.Length: > 0 } row)
		{
			return row.Id;
		}

		return string.IsNullOrEmpty(scenePath) ? boss.Name : scenePath;
	}

	/// <summary>当前关卡的身份（关卡行 Id；读不到就给空串，绝不猜）。</summary>
	public static string StageIdentityOf()
	{
#if DEBUG
		if (!string.IsNullOrEmpty(DebugStageIdOverride))
		{
			return DebugStageIdOverride;
		}
#endif
		return Run.StageController.Instance?.Definition?.Id ?? string.Empty;
	}

	/// <summary>给日志用：把"这一关是哪个"写清楚（行 Id + 关号），避免日志里只剩一个空串。</summary>
	public static string CurrentStageText()
	{
		Run.StageController? stage = Run.StageController.Instance;
		string id = StageIdentityOf();
		if (stage == null)
		{
			return string.IsNullOrEmpty(id) ? "（没有 StageController）" : id;
		}

		return string.IsNullOrEmpty(id) ? $"第 {stage.StageNumber} 关" : $"{id}（第 {stage.StageNumber} 关）";
	}

	/// <summary>限定关卡里的一项是否命中：**关卡行 Id** 或**关号**（<c>"2"</c>）都算。</summary>
	private bool IsCurrentStage()
	{
		string id = StageIdentityOf();
		int number = Run.StageController.Instance?.StageNumber ?? 0;
		foreach (string entry in StageIds)
		{
			if (entry == id || (number > 0 && entry == number.ToString()))
			{
				return true;
			}
		}

		return false;
	}
}
