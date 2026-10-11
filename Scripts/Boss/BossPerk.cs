using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>
/// Boss 强化条目（策划文档 §12 Boss 构筑）：让 Boss 也有自己的「构筑」，而不是只有血量差异。
///
/// ⚠️ 类名保持 <c>BossPerk</c>（不改名、不换文件），但**两类条目在人话里叫法不同**（用户 2026-10 拍板）：
/// <b>行为型 = 技能</b>（Boss Build，来自 `Config/Tables/Skills/bhv_*.tres`，它本来就是技能表的行）、
/// <b>数值型 = 词条</b>（Pressure 池 `Config/Tables/Pressure/pmod_*.tres`）。
/// 所以下面注释里凡是写"词条"的地方，若指的是行为型，都已经改称**技能**。
/// </summary>
public sealed class BossPerk
{
	public required string Id { get; init; }

	/// <summary>
	/// 显示名（日志与 HUD 用）。
	/// ⚠️ 它**可以被改写**：限定词在首领出生时判不成立、改抽了另一条时，会被写成
	/// <c>「原条目→实际生效的条目」</c>（见 <see cref="MarkRedrawn"/>）——
	/// 这样 <c>StageController</c> / <c>EncounterController</c> 里那两条**既有日志**（它们先 Apply、后取 Name）
	/// 说的仍然是"真正生效的那一条"，不会写一个没生效的名字。
	/// </summary>
	public string Name { get; set; } = string.Empty;

	public string Description { get; init; } = string.Empty;
	public float Weight { get; init; } = 1f;

	/// <summary>
	/// **数值型**（词条）候选的适用标签（关卡核心文档 §7.4/§7.5 的"强化池权重或适用规则"）。
	/// 只有 Pressure 转换池会用它筛候选；行为型（技能）留空（Boss Build 不做标签过滤）。
	/// </summary>
	public string[] Tags { get; init; } = System.Array.Empty<string>();

	/// <summary>
	/// 条目的**限定词**（适用条件）：只对某个首领 / 某几关 / 必须有某个技能节点才生效。
	/// 默认 <see cref="PerkQualifier.Any"/>（不限）。
	/// </summary>
	public PerkQualifier Qualifier { get; init; } = PerkQualifier.Any;

	/// <summary>
	/// 这次抽签用的**候选标签**（`RollPressure(tags)` 传进来的那一组）。限定词判不成立时，
	/// 换抽会用同一组标签把候选限制回同一批（见 <see cref="RollTags"/>）。
	/// 行为型（技能，<c>Roll()</c>）为 null = **不换抽**（技能条数由关卡行 `BossPerkCount` 定死，
	/// 判不成立只写日志跳过）。
	/// </summary>
	public string[]? RollTags { get; init; }

	/// <summary>条目的**效果本体**（数值操作 / 行为能力）。限定词与刷新判定都在 <see cref="Apply"/> 里，不在这里。</summary>
	public required Action<BossPerkContext> Effect { get; init; }

	private Action<BossPerkContext>? _apply;

	/// <summary>
	/// 抽取点调用的入口。**签名与接线方式没有变**（<c>StageController</c> / <c>EncounterController</c>
	/// 依旧写 <c>perk.Apply(context)</c>），但内部先做一件事：
	/// <b>用"此刻的上下文"重新判一次限定词</b>（首领出生时的那一次），判不成立就换抽 ——
	/// 所以"同一局里先抽到、后出生"不会复用抽签时的结论。
	/// </summary>
	public Action<BossPerkContext> Apply => _apply ??= context => BossPerkCatalog.ApplyWithRefresh(this, context);

	/// <summary>
	/// 带"这次抽签用的标签"的一份副本 —— <c>RollPressure</c> 抽中时包一层。
	/// 为什么必须是副本：换抽会把 <see cref="Name"/> 改写成"原词条→换抽词条"，
	/// 若改在编译期共享的那个实例上，这份改动会跟着留到下一局（跨局污染）。
	/// </summary>
	public BossPerk AsRolled(System.Collections.Generic.IReadOnlyList<string>? rollTags)
	{
		var copy = new BossPerk
		{
			Id = Id,
			Name = Name,
			Description = Description,
			Weight = Weight,
			Tags = Tags,
			Qualifier = Qualifier,
			Effect = Effect,
			RollTags = rollTags == null || rollTags.Count == 0 ? System.Array.Empty<string>() : ToArray(rollTags),
		};
		return copy;
	}

	/// <summary>限定词不成立、改抽了 <paramref name="applied"/> 之后，把名字改写成"原词条→实际生效的词条"。</summary>
	public void MarkRedrawn(BossPerk applied) => Name = $"{Name}→{applied.Name}";

	private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> source)
	{
		var copy = new string[source.Count];
		for (int i = 0; i < source.Count; i++)
		{
			copy[i] = source[i];
		}

		return copy;
	}
}

/// <summary>套用强化条目（技能 / 词条）时能拿到的东西。</summary>
public sealed class BossPerkContext
{
	public required Unit Boss { get; init; }
	public required Node Source { get; init; }
	public BossPressure? Pressure { get; init; }
}

/// <summary>
/// Boss 强化目录（两类来源：**技能** = 行为型 Boss Build / **词条** = 数值型 Pressure）。
/// 加内容只加一行定义；效果走 StatSheet 的 Modifier 或直接改 Boss 的组件参数，
/// 不往 Boss 脚本里加分支（策划文档 §12：Boss Build 与 Boss Pressure、Boss Skill 分离）。
/// </summary>
/// <remarks>
/// ⚠️ <b>关卡核心文档 §7.1 的硬约束</b>：这里是 <b>Boss Build</b>（首领实际获得的战斗行为/技能）。
/// <b>Boss Pressure</b>（数值型强化，按 Pressure 阈值转换）是**另一套**来源，两套可以在最终 Boss
/// 初始化时共同生效，但**不允许合并成一个无法区分来源的强化池**。
///
/// <b>配置表阶段 3 之后，两套来源在数据上已经分开</b>（用户："首领行为表本质也是一些技能"）：
/// <list type="bullet">
/// <item><b>行为/技能型</b> → `Config/Tables/Skills/` 里 <c>ApplyHandler</c> 非空的那 4 行
///   （<c>bhv_awakened</c> / <c>bhv_summoner</c> / <c>bhv_screaming</c> / <c>bhv_charging</c>）。</item>
/// <item><b>数值型</b> → `Config/Tables/Pressure/` 的 <c>pmod_*.tres</c>（当前 7 行：
///   <c>pmod_berserk_dmg</c> / <c>pmod_berserk_speed</c> / <c>pmod_fortress_hp</c> / <c>pmod_swift</c>
///   + 3 条技能参数 <c>pmod_scream_echo</c> / <c>pmod_charge_impact</c> / <c>pmod_summon_haste</c>）。</item>
/// </list>
/// <see cref="Build"/> 把两张表分别编译，<see cref="All"/> 按"数值型在前、行为型在后"合并
/// （保留 `All` 只为日志与统计；**它已经不是任何抽取点的候选集**）。
///
/// <b>两个抽取点彻底分开（配置表阶段 6，关卡核心文档 §7.1/§7.5 的硬要求）</b>：
/// <list type="bullet">
/// <item><b>Boss 开场技能 = Boss Build</b> → <see cref="Roll"/>，**只从行为型**里抽
///   （<c>EncounterController.SpawnBossNow</c> 调用，条数 = <c>StageDefinition.BossPerkCount</c>）。</item>
/// <item><b>Pressure 转换 = 数值强化</b> → <see cref="RollPressure"/>，只从 <b>数值型</b>里抽，
///   按 <see cref="BossPerk.Weight"/> 真加权、按 <see cref="BossPerk.Tags"/> 过滤候选
///   （<c>StageController.AddPressureForNode</c> 在**跨过阈值的那一刻**调用，套用点在 Boss 出生时）。</item>
/// </list>
/// 两套来源因此可以**独立配置、独立追踪、独立结算**（§7.5 最后一句）。
/// ⚠️ 仍然**没有**为 Pressure 设计"可被玩家增幅修改"的接口（文档明令禁止）。
/// </remarks>
public static class BossPerkCatalog
{
	/// <summary>
	/// 数值型（Pressure 池）的代码兜底 —— 表读不到时用它，**逐条与
	/// `Config/Tables/Pressure/pmod_*.tres` 七行对齐**（同样的 Id / 显示名 / 权重 / 标签 / 限定词 / 效果）。
	///
	/// ⚠️ 为什么拆成 8 条也要跟着改：兜底若还留着"伤害+攻速挤在一栏"的旧样子，表一读不到就会
	/// **静默换挡**成"一抽买两个参数"（正是项目里反复踩的那个坑）。所以兜底必须与表同结构、同数值。
	/// **不要删**，这是回退路径。
	///
	/// ⚠️ **护甲那一栏（`pmod_fortress_armor`，+150 甲）已按用户要求删除**（"护甲目前就只给精英怪使用，
	/// 首领不需要"）—— 表行、`Balance.tres` 的数组项与 ext_resource、以及这里这条兜底**同一次改动**一起下线。
	/// 只删表行不改兜底 = 表一读不到首领又会吃护甲（静默换挡）；精英怪的护甲来自另一套
	/// （<see cref="ActRoguelike.Combat.EliteModifier.ArmorAdd"/>，与 Pressure 无关），所以不受影响。
	/// </summary>
	private static readonly BossPerk[] FallbackPressure =
	{
		new BossPerk
		{
			Id = "pmod_berserk_dmg",
			Name = "狂暴·伤害",
			Description = "伤害 ×1.25",
			Weight = 3f,
			Tags = new[] { "Offense" },
			Effect = context => Mod(context, "DamageMultiplier", ModifierOperation.Multiply, 1.25f),
		},
		new BossPerk
		{
			Id = "pmod_berserk_speed",
			Name = "狂暴·攻速",
			Description = "攻速 ×1.2",
			Weight = 3f,
			Tags = new[] { "Offense" },
			Effect = context => Mod(context, "AttackSpeedMultiplier", ModifierOperation.Multiply, 1.2f),
		},
		new BossPerk
		{
			Id = "pmod_fortress_hp",
			Name = "铁壁·生命",
			Description = "最大生命 ×1.4",
			Weight = 2f,
			Tags = new[] { "Defense" },
			Effect = context => Mod(context, "MaxHealthMultiplier", ModifierOperation.Multiply, 1.4f),
		},
		new BossPerk
		{
			Id = "pmod_swift",
			Name = "疾行",
			Description = "移速 ×1.35",
			Weight = 2f,
			Tags = new[] { "Mobility" },
			Effect = context => Mod(context, "MoveSpeedMultiplier", ModifierOperation.Multiply, 1.35f),
		},

		// 技能参数词条（§7.5：技能冷却等工作在数值型这一栏；限定词 = 这个 Boss 必须真的有这个技能节点）。
		new BossPerk
		{
			Id = "pmod_scream_echo",
			Name = "尖啸回响",
			Description = "尖啸冷却 ×0.8",
			Weight = 1f,
			Tags = new[] { "Offense", "Skill" },
			Qualifier = BossFinalSkill("AbilityScream"),
			Effect = context => ScaleAbility(context, "AbilityScream", "Cooldown", 0.8f),
		},
		new BossPerk
		{
			Id = "pmod_charge_impact",
			Name = "冲锋冲击",
			Description = "冲锋伤害 ×1.25",
			Weight = 1f,
			Tags = new[] { "Offense", "Skill" },
			Qualifier = BossFinalSkill("AbilityCharge"),
			Effect = context => ScaleHitbox(context, "AbilityCharge", "Damage", 1.25f),
		},
		new BossPerk
		{
			Id = "pmod_summon_haste",
			Name = "召唤急速",
			Description = "召唤冷却 ×0.75",
			Weight = 1f,
			Tags = new[] { "Defense", "Skill" },
			Qualifier = BossFinalSkill("AbilitySummon"),
			Effect = context => ScaleAbility(context, "AbilitySummon", "Cooldown", 0.75f),
		},
	};

	/// <summary>技能参数词条的兜底限定词：只对 `boss_final`、且它必须真的有这个技能节点。</summary>
	private static PerkQualifier BossFinalSkill(string abilityNode)
		=> new() { BossIds = new[] { "boss_final" }, RequiredAbilityNode = abilityNode };

	/// <summary>行为/技能型（Boss Build）的代码兜底 —— 表读不到时用它，逐条与分表前一致。</summary>
	private static readonly BossPerk[] FallbackBehavior =
	{
		new BossPerk
		{
			Id = "awakened",
			Name = "提前觉醒",
			Description = "开场即带 1 级压力",
			Effect = context =>
			{
				if (context.Pressure != null)
				{
					context.Pressure.InitialLevel += 1;
				}
			},
		},
		new BossPerk
		{
			Id = "summoner",
			Name = "召唤师",
			Description = "压力每次升级多召唤 1 个增援，且更早开始召唤",
			Effect = context =>
			{
				if (context.Pressure != null)
				{
					context.Pressure.AddsPerLevel += 1;
					context.Pressure.AddSpawnLevel = Mathf.Max(1, context.Pressure.AddSpawnLevel - 1);
				}
			},
		},
		new BossPerk
		{
			Id = "screaming",
			Name = "尖啸强化",
			Description = "开场就会范围尖啸",
			Effect = context =>
			{
				if (context.Boss.GetNodeOrNull<Ability>("AbilityScream") is { } scream)
				{
					scream.Enabled = true;
				}
			},
		},
		new BossPerk
		{
			Id = "charging",
			Name = "冲锋强化",
			Description = "开场就会冲锋",
			Effect = context =>
			{
				if (context.Boss.GetNodeOrNull<Ability>("AbilityCharge") is { } charge)
				{
					charge.Enabled = true;
				}
			},
		},
	};

	/// <summary>
	/// 行为型（**技能**）的处理器 —— 用户原话：「首领行为表本质也是一些技能」。
	/// 签名与增幅项的 <c>RewardEffectRegistry</c> 同构，只是上下文换成 <see cref="BossPerkContext"/>；
	/// 加一条行为型技能 = 加一行 `.tres`；只有**全新的行为种类**才需要在这里加一行。
	/// </summary>
	private static readonly Dictionary<string, Action<BossPerkContext, AbilityConfig>> Handlers = new()
	{
		// 提前觉醒：改 BossPressure 的初始等级。
		["PressureLevel"] = (context, row) =>
		{
			if (context.Pressure != null)
			{
				context.Pressure.InitialLevel += (int)row.Param("InitialLevelAdd", 1f);
			}
		},

		// 召唤师：改 BossPressure 的召唤节奏。
		["PressureSummon"] = (context, row) =>
		{
			if (context.Pressure != null)
			{
				context.Pressure.AddsPerLevel += (int)row.Param("AddsPerLevelAdd", 1f);
				context.Pressure.AddSpawnLevel = Mathf.Max(1, context.Pressure.AddSpawnLevel - (int)row.Param("AddSpawnLevelSub", 1f));
			}
		},

		// 启用某个能力节点（节点名从 Params 的 NodeName 读；找不到写警告，不静默失效）。
		["EnableAbility"] = (context, row) =>
		{
			string node = row.ParamText("NodeName", string.Empty);
			if (context.Boss.GetNodeOrNull<Ability>(node) is { } ability)
			{
				ability.Enabled = true;
				return;
			}

			GD.PushWarning($"[BossPerkCatalog] {row.Id} 要启用的 {node} 在 {context.Boss.Name} 上找不到，已跳过。");
		},
	};

	private static readonly RandomNumberGenerator Rng = new();

	private static IReadOnlyList<BossPerk>? _pressure;
	private static IReadOnlyList<BossPerk>? _behavior;

	/// <summary>
	/// 数值型强化候选（Pressure 池）—— 只有 <see cref="RollPressure"/> 会用它。
	/// 表读不到 → 代码兜底 7 条（4 条数值型参数栏 + 3 条技能参数栏，与表同结构同数值）。
	/// </summary>
	public static IReadOnlyList<BossPerk> PressureOnly => _pressure ?? FallbackPressure;

	/// <summary>行为/技能型候选（Boss Build，界面上叫**技能**）—— 只有 <see cref="Roll"/> 会用它。表读不到 → 代码兜底 4 条。</summary>
	public static IReadOnlyList<BossPerk> BehaviorOnly => _behavior ?? FallbackBehavior;

	/// <summary>全部候选（数值型在前、行为型在后）。**只用于日志与统计**，两个抽取点各自只用自己的那一半。</summary>
	public static IReadOnlyList<BossPerk> All
	{
		get
		{
			var all = new List<BossPerk>(PressureOnly.Count + BehaviorOnly.Count);
			all.AddRange(PressureOnly);
			all.AddRange(BehaviorOnly);
			return all;
		}
	}

	/// <summary>
	/// 读表 → 编译成运行期词条。<paramref name="behaviorRows"/> 是技能表里 <c>ApplyHandler</c> 非空的行，
	/// <paramref name="pressureRows"/> 是 `Config/Tables/Pressure/` 的行。
	/// **两张表各自独立回退**：某一侧读不到就用那一侧的代码兜底，不会因为另一侧有表而变空。
	/// </summary>
	public static void Build(IReadOnlyList<AbilityConfig>? behaviorRows, IReadOnlyList<PressureModConfig>? pressureRows)
	{
		IReadOnlyList<BossPerk>? pressure = CompilePressure(pressureRows);
		IReadOnlyList<BossPerk>? behavior = CompileBehavior(behaviorRows);

		_pressure = pressure;
		_behavior = behavior;

		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"首领强化来源（关卡核心文档 §7.1：两套来源必须能分开）：数值型 Pressure（**词条**）{PressureOnly.Count} 条（来源：{(pressure != null ? "表 Pressure" : "代码兜底")}）/ 行为型 Boss Build（**技能**，用户拍板：行为型的那些本来就是技能）{BehaviorOnly.Count} 条（来源：{(behavior != null ? "表 Skills" : "代码兜底")}）");

		// §7.5「候选内容和权重由配置定义」：把转换池的候选 / 权重 / 由权重算出的概率打出来。
		// 这一行是**确定性**的（不含随机），所以可以直接当回归指标逐字比对。
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"Pressure 转换池（§7.5，阈值达成后立即抽取；只出数值型 = 词条）：{DescribePressurePool(null)}；抽取点 2 个：Boss 开场**技能** Roll() 只抽行为型（{BehaviorOnly.Count} 条），Pressure RollPressure() 只抽数值型（词条）");
	}

	/// <summary>
	/// <b>Boss 开场技能（Boss Build）抽取</b>：随机抽 count 个不重复的**行为/技能型**条目（加权）。
	/// 抽出来的这些在界面上就叫**技能**（用户拍板：行为型的那些本来就是技能，不是"词条"）。
	///
	/// ⚠️ 配置表阶段 6 起这里**只抽行为型**（原来抽的是"数值型 + 行为型"合并的 7 条）。
	/// 这是关卡核心文档 §7.1「Boss Pressure 与 Boss Build 必须保持分离 / 不能合并成一个无法区分来源的
	/// 强化池」的落地：数值型改由 §7.5 的 Pressure 转换池（<see cref="RollPressure"/>）单独负责。
	/// 所以**开场技能的结果会变**（候选集 7 → 4），这是文档要求的行为变化，不是回归。
	/// 随机数发生器仍是本类自己的静态 <see cref="Rng"/>（与 RewardPool 的实例 RNG 分开，
	/// 共用一把会让奖励抽取的随机序列也漂）。
	/// </summary>
	public static List<BossPerk> Roll(int count)
		=> WeightedPick.Pick(BehaviorOnly, perk => perk.Weight, count, Rng);

	/// <summary>
	/// <b>Pressure 转换池（§7.5）抽取</b>：从**数值型**候选里按权重抽 1 条。
	/// 候选先按 <paramref name="tags"/> 过滤（两层合并规则见 <see cref="FilterPressure"/>）；
	/// 池空 → 返回 null（调用方只写日志，不静默变强）。
	///
	/// ⚠️ 这里**只做标签预筛，不判限定词** —— 抽签发生在"一次挑战完成"时，那时首领还不存在
	/// （拿不到"这个 Boss 有没有这个技能"）；限定词一律留到**首领出生**时判（见 <see cref="ApplyWithRefresh"/>）。
	/// 返回的是带 <see cref="BossPerk.RollTags"/> 的一份**副本**（换抽时要用同一组标签、且不能污染编译期实例）。
	/// </summary>
	/// <param name="tags">
	/// 本次抽取允许的标签（来自节点行的 <c>BossModifierTags</c>，其次关卡行的 <c>PressureUpgradeTags</c>）。
	/// 空 = 不限制。
	/// </param>
	public static BossPerk? RollPressure(IReadOnlyList<string>? tags)
		=> RollPressureExcept(tags, System.Array.Empty<string>());

	/// <summary>
	/// 同 <see cref="RollPressure"/>，但可以**排除若干 Id**（限定词判不成立时换抽用：
	/// 把已经判过、已经不合条件的那些排除掉，避免反复抽到同一条而死循环）。池被排空 → 返回 null。
	/// </summary>
	private static BossPerk? RollPressureExcept(IReadOnlyList<string>? tags, IReadOnlyList<string> excludeIds)
	{
		List<BossPerk> pool = FilterPressure(tags);
		if (excludeIds.Count > 0)
		{
			pool.RemoveAll(perk => Contains(excludeIds, perk.Id));
		}

		List<BossPerk> picked = WeightedPick.Pick(pool, perk => perk.Weight, 1, Rng);
		return picked.Count > 0 ? picked[0].AsRolled(tags) : null;
	}

	private static bool Contains(IReadOnlyList<string> ids, string id)
	{
		for (int i = 0; i < ids.Count; i++)
		{
			if (ids[i] == id)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 抽取点统一入口（<see cref="BossPerk.Apply"/> 指向这里）：**在首领出生的这一刻**先做"限定词刷新判定"。
	///
	/// 判定规则：
	/// <list type="number">
	/// <item>成立 → 直接生效（效果本体写在 <see cref="BossPerk.Effect"/> 里）。</item>
	/// <item>不成立 → 写一行日志（说明哪一条限定词不成立、当时的 Boss/关卡是什么），
	///   然后**换抽**另一条（同标签池、排除已经判过的 Id）并重新判定 —— 所以"一抽"不会白费，
	///   也不会因为"抽签时条件成立"就复用那个结论。</item>
	/// <item>池里所有候选都判过一遍仍无适用者 → 只写日志，**不静默变强**（不临时造一条效果出来）。</item>
	/// </list>
	/// 循环次数上界 = 候选池大小（每次换抽都排除掉一个 Id），不会死循环。
	/// </summary>
	internal static void ApplyWithRefresh(BossPerk perk, BossPerkContext context)
	{
		var tried = new List<string>();
		BossPerk? current = perk;

		while (current != null)
		{
			if (Contains(tried, current.Id))
			{
				break;
			}

			tried.Add(current.Id);

			string? reason = current.Qualifier.FailedReason(context);
			if (reason == null)
			{
				current.Effect(context);

				if (!ReferenceEquals(current, perk))
				{
					perk.MarkRedrawn(current);
				}

				if (current.Qualifier.HasAny)
				{
					ActRoguelike.Core.GameLog.Write(
						"Config",
						$"词条限定词（Boss 出生时判定）：{current.Id}「{current.Name}」限定 [{current.Qualifier.Describe()}] → 成立 → 已生效（Boss {PerkQualifier.BossIdentityOf(context.Boss)} · 关卡 {PerkQualifier.CurrentStageText()}）");
				}

				return;
			}

			ActRoguelike.Core.GameLog.Write(
				"Config",
				$"词条限定词（Boss 出生时判定）：{current.Id}「{current.Name}」限定 [{current.Qualifier.Describe()}] → **不成立**（{reason} · Boss {PerkQualifier.BossIdentityOf(context.Boss)} · 关卡 {PerkQualifier.CurrentStageText()}）→ 换抽另一条（每次出生都重新判，不复用抽签时的结论）");

			current = current.RollTags == null ? null : RollPressureExcept(current.RollTags, tried);
		}

		if (tried.Count > 1)
		{
			ActRoguelike.Core.GameLog.Write(
				"Config",
				$"词条限定词：候选 {tried.Count} 条都判过仍无适用者（[{string.Join(",", tried)}]），这次强化未兑现（不静默变强）。");
		}
	}

	/// <summary>本次抽取的候选池（不改任何状态；给日志/自测取证用）。</summary>
	public static List<BossPerk> FilterPressure(IReadOnlyList<string>? tags)
	{
		var pool = new List<BossPerk>();
		foreach (BossPerk perk in PressureOnly)
		{
			if (tags == null || tags.Count == 0)
			{
				pool.Add(perk);
				continue;
			}

			foreach (string tag in tags)
			{
				if (Array.IndexOf(perk.Tags, tag) >= 0)
				{
					pool.Add(perk);
					break;
				}
			}
		}

		return pool;
	}

	/// <summary>
	/// 把 Pressure 转换池的**候选与权重**打成一行（§7.5 要求"候选内容和权重由配置定义"，这一行就是证据：
	/// 权重真的进了概率分母）。括号里是**按权重算出的抽中概率**，不是实测频率。
	/// </summary>
	public static string DescribePressurePool(IReadOnlyList<string>? tags)
	{
		List<BossPerk> pool = FilterPressure(tags);
		if (pool.Count == 0)
		{
			return "候选 0 条（⚠ 这是空池：抽取会返回 null，不会静默变强）";
		}

		float total = 0f;
		foreach (BossPerk perk in pool)
		{
			total += Mathf.Max(WeightedPick.MinWeight, perk.Weight);
		}

		var text = new System.Text.StringBuilder();
		foreach (BossPerk perk in pool)
		{
			if (text.Length > 0)
			{
				text.Append(" · ");
			}

			string perkTags = perk.Tags.Length == 0 ? "无标签" : string.Join(",", perk.Tags);
			string qualifier = perk.Qualifier.HasAny ? $" 限定 [{perk.Qualifier.Describe()}]" : string.Empty;
			text.Append($"{perk.Id} 权重 {perk.Weight:0.##} 标签 [{perkTags}]{qualifier} → {Mathf.Max(WeightedPick.MinWeight, perk.Weight) / total * 100f:0.#}%");
		}

		return $"候选 {pool.Count} 条 [{text}]";
	}

	/// <summary>
	/// 数值型：`Modifiers`（单位属性）+ `AbilityScales` / `AttackScales`（**技能参数**）都挂到 Boss 身上。
	/// 限定词（`BossIds` / `StageIds` / `AbilityNode`）原样带进运行期词条 —— **不在编译期判**，
	/// 留到首领出生时由 <see cref="ApplyWithRefresh"/> 判（这是"刷新判定"的关键）。
	/// </summary>
	private static IReadOnlyList<BossPerk>? CompilePressure(IReadOnlyList<PressureModConfig>? rows)
	{
		if (rows == null || rows.Count == 0)
		{
			return null;
		}

		var built = new List<BossPerk>(rows.Count);
		foreach (PressureModConfig row in rows)
		{
			if (row == null || string.IsNullOrEmpty(row.Id))
			{
				continue;
			}

			PressureModConfig captured = row;
			built.Add(new BossPerk
			{
				Id = captured.Id,
				Name = captured.DisplayName,
				Description = ConfigText.Format(captured.DescriptionTemplate, captured, captured.Params),
				Weight = captured.Weight,
				Tags = captured.Tags,
				Qualifier = new PerkQualifier
				{
					BossIds = captured.BossIds,
					StageIds = captured.StageIds,
					RequiredAbilityNode = captured.AbilityNode,
				},
				Effect = context => ApplyPressureRow(context, captured),
			});
		}

		return built.Count == 0 ? null : built;
	}

	/// <summary>行为/技能型：技能表里 ApplyHandler 非空的行，按处理器注册表编译。</summary>
	private static IReadOnlyList<BossPerk>? CompileBehavior(IReadOnlyList<AbilityConfig>? rows)
	{
		if (rows == null || rows.Count == 0)
		{
			return null;
		}

		var built = new List<BossPerk>(rows.Count);
		foreach (AbilityConfig row in rows)
		{
			if (row == null || !row.IsBossBuild || string.IsNullOrEmpty(row.Id))
			{
				continue;
			}

			if (!Handlers.TryGetValue(row.ApplyHandler, out Action<BossPerkContext, AbilityConfig>? handler))
			{
				GD.PushWarning($"[BossPerkCatalog] {row.Id} 的 ApplyHandler「{row.ApplyHandler}」未注册，已跳过该条。");
				continue;
			}

			AbilityConfig captured = row;
			built.Add(new BossPerk
			{
				Id = captured.Id,
				Name = captured.DisplayName,
				Description = ConfigText.Format(captured.DescriptionTemplate, captured, captured.Params),
				Weight = captured.Weight,
				Effect = context => handler(context, captured),
			});
		}

		return built.Count == 0 ? null : built;
	}

	/// <summary>数值型一行的效果 = 单位属性修正（<c>Modifiers</c>）+ 技能参数（<c>AbilityScales</c> / <c>AttackScales</c>）。</summary>
	private static void ApplyPressureRow(BossPerkContext context, PressureModConfig row)
	{
		foreach (StatOverride entry in row.Modifiers)
		{
			Mod(context, entry.Stat, entry.Operation, entry.Value);
		}

		if (string.IsNullOrEmpty(row.AbilityNode))
		{
			return; // 纯单位属性行（5 条数值型参数栏就是这一类）。
		}

		// 限定词里已经要求"这个 Boss 必须有这个技能节点"，走到这里理论上一定找得到；
		// 但"没找到"必须是**响的**（不静默跳过），所以还是判一次。
		if (context.Boss.GetNodeOrNull<Ability>(row.AbilityNode) is not { } ability)
		{
			GD.PushWarning($"[BossPerkCatalog] {row.Id} 要改的技能节点 {row.AbilityNode} 在 {context.Boss.Name} 上找不到，本条只生效了单位属性部分。");
			return;
		}

		foreach (ParamEntry entry in row.AbilityScales)
		{
			ScaleProperty(row, ability, entry, $"技能 {row.AbilityNode}");
		}

		if (row.AttackScales.Length == 0)
		{
			return;
		}

		// 命中体：与 `AbilityConfig.ApplyAttackParams` 同一套寻址（命中体优先，没有就退到武器组件）。
		Node? strike = (Node?)NodeSearch.FindInSubtree<AttackHitbox>(ability)
			?? NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(ability);
		if (strike == null)
		{
			GD.PushWarning($"[BossPerkCatalog] {row.Id} 填了命中体参数，但 {row.AbilityNode} 下面既没有命中体也没有武器组件，已跳过。");
			return;
		}

		foreach (ParamEntry entry in row.AttackScales)
		{
			ScaleProperty(row, strike, entry, $"技能 {row.AbilityNode} 的命中体");
		}
	}

	/// <summary>
	/// 把一条 <see cref="ParamEntry"/> 当**倍率**乘在目标节点的当前值上（不是绝对赋值）——
	/// 这样同一条词条抽到两次会继续叠乘（尖啸冷却 7 → 5.6 → 4.48），而不是"第二次白抽"。
	/// 属性不存在 / 属于行为型开关（<c>Enabled</c>）都会**写警告并跳过**，绝不静默改坏。
	/// </summary>
	private static void ScaleProperty(PressureModConfig row, Node target, ParamEntry entry, string what)
	{
		if (string.IsNullOrEmpty(entry.Key))
		{
			return;
		}

		// §7.1 的边界：数值池**不许**碰"这个技能开不开"这类行为开关（那是 Boss Build 的 bhv_* 技能行）。
		if (entry.Key is "Enabled" or "ApplyHandler" or "NodeName")
		{
			GD.PushWarning($"[BossPerkCatalog] {row.Id} 想改 {what} 的 {entry.Key} —— 这属于**行为型**（Boss Build），数值型词条不许碰，已跳过。");
			return;
		}

		Variant current = target.Get(entry.Key);
		if (current.VariantType == Variant.Type.Nil)
		{
			GD.PushWarning($"[BossPerkCatalog] {row.Id} 的参数 {entry.Key} 在 {target.GetType().Name} 上不存在，已跳过。");
			return;
		}

		float before = current.AsSingle();
		float after = before * entry.AsFloat(1f);
		target.Set(entry.Key, after);
		ActRoguelike.Core.GameLog.Write("Config", $"Pressure 技能参数：{row.Id}「{row.DisplayName}」→ {what}.{entry.Key} {before:0.###} → {after:0.###}（×{entry.AsFloat(1f):0.###}）");
	}

	/// <summary>兜底词条用：把能力节点上的某个属性按倍率改一次（与表行同一条代码路径，不走 ParamEntry）。</summary>
	private static void ScaleAbility(BossPerkContext context, string abilityNode, string property, float scale)
	{
		if (context.Boss.GetNodeOrNull<Ability>(abilityNode) is not { } ability)
		{
			GD.PushWarning($"[BossPerkCatalog] 兜底词条要改的技能节点 {abilityNode} 在 {context.Boss.Name} 上找不到，已跳过。");
			return;
		}

		ScaleDirect(abilityNode, ability, property, scale);
	}

	/// <summary>兜底词条用：把技能命中体上的某个属性按倍率改一次。</summary>
	private static void ScaleHitbox(BossPerkContext context, string abilityNode, string property, float scale)
	{
		if (context.Boss.GetNodeOrNull<Ability>(abilityNode) is not { } ability)
		{
			GD.PushWarning($"[BossPerkCatalog] 兜底词条要改的技能节点 {abilityNode} 在 {context.Boss.Name} 上找不到，已跳过。");
			return;
		}

		if (NodeSearch.FindInSubtree<AttackHitbox>(ability) is not { } hitbox)
		{
			GD.PushWarning($"[BossPerkCatalog] 兜底词条在 {abilityNode} 下面找不到命中体，已跳过。");
			return;
		}

		ScaleDirect($"{abilityNode} 的命中体", hitbox, property, scale);
	}

	private static void ScaleDirect(string what, Node target, string property, float scale)
	{
		if (target.Get(property).VariantType == Variant.Type.Nil)
		{
			GD.PushWarning($"[BossPerkCatalog] 兜底词条的参数 {property} 在 {target.GetType().Name} 上不存在，已跳过。");
			return;
		}

		float before = target.Get(property).AsSingle();
		target.Set(property, before * scale);
		ActRoguelike.Core.GameLog.Write("Config", $"Pressure 技能参数（兜底）：{what}.{property} {before:0.###} → {before * scale:0.###}（×{scale:0.###}）");
	}

	private static void Mod(BossPerkContext context, string stat, ModifierOperation operation, float value)
	{
		context.Boss.Stats.AddModifier(new StatModifier
		{
			Stat = stat,
			Operation = operation,
			Value = value,
			Source = context.Source,
		});
		context.Boss.RefreshStats();
		context.Boss.FillVitals();
	}
}
