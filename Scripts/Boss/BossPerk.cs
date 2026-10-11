using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
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
/// 即：数值型强化的候选/权重不得写进本目录，行为型词条也不得混进 Pressure 强化池。
/// （当前本目录确实同时含数值词条（狂暴/铁壁/疾行/提前觉醒）与行为词条（召唤师/尖啸/冲锋），
/// 把它们按来源拆开是设计文档 §5 阶段 5 / T9 的工作，本阶段不动。）
/// </remarks>
public static class BossPerkCatalog
{
	public static readonly BossPerk[] All =
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

	private static readonly RandomNumberGenerator Rng = new();

	/// <summary>随机抽 count 个不重复词条（加权）。</summary>
	public static List<BossPerk> Roll(int count)
	{
		// 加权抽样走公共工具（Config/WeightedPick.cs）。⚠️ 本目录自己的静态 Rng 必须传进去：
		// 它与 RewardPool 的实例 RNG 不是同一把，共用一把会让随机序列变、自测基线漂。
		return ActRoguelike.Config.WeightedPick.Pick(All, perk => perk.Weight, count, Rng);
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
