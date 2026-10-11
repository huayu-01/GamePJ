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
/// （顺序与分表前的 7 条完全一致，所以开场抽取的随机序列不变）；
/// <see cref="PressureOnly"/> / <see cref="BehaviorOnly"/> 让两套来源**可独立追踪**，
/// 将来 Pressure 转换池接线时按来源各抽各的即可。
/// ⚠️ 仍然**没有**为 Pressure 设计"可被玩家增幅修改"的接口（文档明令禁止）。
/// </remarks>
public static class BossPerkCatalog
{
	/// <summary>
	/// 数值型（Pressure 池）的代码兜底 —— 表读不到时用它，逐条与分表前一致。**不要删**，这是回退路径。
	/// </summary>
	private static readonly BossPerk[] FallbackPressure =
	{
		new BossPerk
		{
			Id = "berserk",
			Name = "狂暴",
			Description = "伤害 +25%，攻速 +20%",
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

	/// <summary>数值型强化候选（Pressure 池）。表读不到 → 代码兜底 3 条。</summary>
	public static IReadOnlyList<BossPerk> PressureOnly => _pressure ?? FallbackPressure;

	/// <summary>行为/技能型词条候选（Boss Build）。表读不到 → 代码兜底 4 条。</summary>
	public static IReadOnlyList<BossPerk> BehaviorOnly => _behavior ?? FallbackBehavior;

	/// <summary>全部候选（数值型在前、行为型在后 —— 与分表前的 7 条顺序一致）。</summary>
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
	}

	/// <summary>随机抽 count 个不重复词条（加权）。顺序 = 数值型在前、行为型在后，与分表前一致。</summary>
	public static List<BossPerk> Roll(int count)
	{
		// 加权抽样走公共工具（Config/WeightedPick.cs）。⚠️ 本目录自己的静态 Rng 必须传进去：
		// 它与 RewardPool 的实例 RNG 不是同一把，共用一把会让随机序列变、自测基线漂。
		return WeightedPick.Pick(All, perk => perk.Weight, count, Rng);
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
