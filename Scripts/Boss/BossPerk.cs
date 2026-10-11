using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>Boss 词条（策划文档 §12 Boss 构筑）：让 Boss 也有自己的「构筑」，而不是只有血量差异。</summary>
public sealed class BossPerk
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public string Description { get; init; } = string.Empty;
	public float Weight { get; init; } = 1f;

	/// <summary>
	/// **数值型**候选的适用标签（关卡核心文档 §7.4/§7.5 的"强化池权重或适用规则"）。
	/// 只有 Pressure 转换池会用它筛候选；行为型词条留空（Boss Build 不做标签过滤）。
	/// </summary>
	public string[] Tags { get; init; } = System.Array.Empty<string>();

	public required Action<BossPerkContext> Apply { get; init; }
}

/// <summary>套用词条时能拿到的东西。</summary>
public sealed class BossPerkContext
{
	public required Unit Boss { get; init; }
	public required Node Source { get; init; }
	public BossPressure? Pressure { get; init; }
}

/// <summary>
/// Boss 词条目录。加内容只加一行定义；效果走 StatSheet 的 Modifier 或直接改 Boss 的组件参数，
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
/// <item><b>数值型</b> → `Config/Tables/Pressure/` 的 <c>pmod_*.tres</c>
///   （<c>pmod_berserk</c> / <c>pmod_fortress</c> / <c>pmod_swift</c>）。</item>
/// </list>
/// <see cref="Build"/> 把两张表分别编译，<see cref="All"/> 按"数值型在前、行为型在后"合并
/// （保留 `All` 只为日志与统计；**它已经不是任何抽取点的候选集**）。
///
/// <b>两个抽取点彻底分开（配置表阶段 6，关卡核心文档 §7.1/§7.5 的硬要求）</b>：
/// <list type="bullet">
/// <item><b>Boss 开场词条 = Boss Build</b> → <see cref="Roll"/>，**只从行为型**里抽
///   （<c>EncounterController.SpawnBossNow</c> 调用，词条数 = <c>StageDefinition.BossPerkCount</c>）。</item>
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
	/// 数值型（Pressure 池）的代码兜底 —— 表读不到时用它，逐条与分表前一致（含标签与权重，
	/// 与 `Config/Tables/Pressure/pmod_*.tres` 三行对齐）。**不要删**，这是回退路径。
	/// </summary>
	private static readonly BossPerk[] FallbackPressure =
	{
		new BossPerk
		{
			Id = "berserk",
			Name = "狂暴",
			Description = "伤害 +25%，攻速 +20%",
			Weight = 3f,
			Tags = new[] { "Offense" },
			Apply = context =>
			{
				Mod(context, "DamageMultiplier", ModifierOperation.Multiply, 1.25f);
				Mod(context, "AttackSpeedMultiplier", ModifierOperation.Multiply, 1.2f);
			},
		},
		new BossPerk
		{
			Id = "fortress",
			Name = "铁壁",
			Description = "最大生命 +40%，护甲 +150",
			Weight = 2f,
			Tags = new[] { "Defense" },
			Apply = context =>
			{
				Mod(context, "MaxHealthMultiplier", ModifierOperation.Multiply, 1.4f);
				Mod(context, "MaxArmorAdd", ModifierOperation.Add, 150f);
			},
		},
		new BossPerk
		{
			Id = "swift",
			Name = "疾行",
			Description = "移速 +35%",
			Weight = 2f,
			Tags = new[] { "Mobility" },
			Apply = context => Mod(context, "MoveSpeedMultiplier", ModifierOperation.Multiply, 1.35f),
		},
	};

	/// <summary>行为/技能型（Boss Build）的代码兜底 —— 表读不到时用它，逐条与分表前一致。</summary>
	private static readonly BossPerk[] FallbackBehavior =
	{
		new BossPerk
		{
			Id = "awakened",
			Name = "提前觉醒",
			Description = "开场即带 1 级压力",
			Apply = context =>
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
			Apply = context =>
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
			Apply = context =>
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
			Apply = context =>
			{
				if (context.Boss.GetNodeOrNull<Ability>("AbilityCharge") is { } charge)
				{
					charge.Enabled = true;
				}
			},
		},
	};

	/// <summary>
	/// 行为型词条的处理器（"首领行为表本质也是一些技能"—— 签名与增幅项的 <c>RewardEffectRegistry</c>
	/// 同构，只是上下文换成 <see cref="BossPerkContext"/>）。
	/// 加一条行为型词条 = 加一行 `.tres`；只有**全新的行为种类**才需要在这里加一行。
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

	/// <summary>数值型强化候选（Pressure 池）—— 只有 <see cref="RollPressure"/> 会用它。表读不到 → 代码兜底 3 条。</summary>
	public static IReadOnlyList<BossPerk> PressureOnly => _pressure ?? FallbackPressure;

	/// <summary>行为/技能型词条候选（Boss Build）—— 只有 <see cref="Roll"/> 会用它。表读不到 → 代码兜底 4 条。</summary>
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
			$"首领强化来源（关卡核心文档 §7.1：两套来源必须能分开）：数值型 Pressure {PressureOnly.Count} 条（来源：{(pressure != null ? "表 Pressure" : "代码兜底")}）/ 行为型 Boss Build {BehaviorOnly.Count} 条（来源：{(behavior != null ? "表 Skills" : "代码兜底")}）");

		// §7.5「候选内容和权重由配置定义」：把转换池的候选 / 权重 / 由权重算出的概率打出来。
		// 这一行是**确定性**的（不含随机），所以可以直接当回归指标逐字比对。
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"Pressure 转换池（§7.5，阈值达成后立即抽取；只出数值型）：{DescribePressurePool(null)}；抽取点 2 个：Boss 开场词条 Roll() 只抽行为型（{BehaviorOnly.Count} 条），Pressure RollPressure() 只抽数值型");
	}

	/// <summary>
	/// <b>Boss 开场词条（Boss Build）抽取</b>：随机抽 count 个不重复的**行为/技能型**词条（加权）。
	///
	/// ⚠️ 配置表阶段 6 起这里**只抽行为型**（原来抽的是"数值型 + 行为型"合并的 7 条）。
	/// 这是关卡核心文档 §7.1「Boss Pressure 与 Boss Build 必须保持分离 / 不能合并成一个无法区分来源的
	/// 强化池」的落地：数值型改由 §7.5 的 Pressure 转换池（<see cref="RollPressure"/>）单独负责。
	/// 所以**开场词条的结果会变**（候选集 7 → 4），这是文档要求的行为变化，不是回归。
	/// 随机数发生器仍是本类自己的静态 <see cref="Rng"/>（与 RewardPool 的实例 RNG 分开，
	/// 共用一把会让奖励抽取的随机序列也漂）。
	/// </summary>
	public static List<BossPerk> Roll(int count)
		=> WeightedPick.Pick(BehaviorOnly, perk => perk.Weight, count, Rng);

	/// <summary>
	/// <b>Pressure 转换池（§7.5）抽取</b>：从**数值型**候选里按权重抽 1 条。
	/// 候选先按 <paramref name="tags"/> 过滤（两层合并规则见 <see cref="FilterPressure"/>）；
	/// 池空 → 返回 null（调用方只写日志，不静默变强）。
	/// </summary>
	/// <param name="tags">
	/// 本次抽取允许的标签（来自节点行的 <c>BossModifierTags</c>，其次关卡行的 <c>PressureUpgradeTags</c>）。
	/// 空 = 不限制。
	/// </param>
	public static BossPerk? RollPressure(IReadOnlyList<string>? tags)
	{
		List<BossPerk> picked = WeightedPick.Pick(FilterPressure(tags), perk => perk.Weight, 1, Rng);
		return picked.Count > 0 ? picked[0] : null;
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
			text.Append($"{perk.Id} 权重 {perk.Weight:0.##} 标签 [{perkTags}] → {Mathf.Max(WeightedPick.MinWeight, perk.Weight) / total * 100f:0.#}%");
		}

		return $"候选 {pool.Count} 条 [{text}]";
	}

	/// <summary>数值型：Modifiers 里的每条 StatOverride 都挂到 Boss 的 StatSheet 上。</summary>
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
				Apply = context => ApplyModifiers(context, captured),
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
				Apply = context => handler(context, captured),
			});
		}

		return built.Count == 0 ? null : built;
	}

	private static void ApplyModifiers(BossPerkContext context, PressureModConfig row)
	{
		foreach (StatOverride entry in row.Modifiers)
		{
			Mod(context, entry.Stat, entry.Operation, entry.Value);
		}
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
