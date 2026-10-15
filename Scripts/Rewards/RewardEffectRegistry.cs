using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 增幅项效果的**处理器注册表**（设计文档 config-schema.md §6.1 方案 A）：
/// 表里只写 <c>ApplyHandler</c> 字符串 + 数值，逻辑留在一行行 C# 里。
///
/// 这样表仍然是**可校验的数据** —— <see cref="IsRegistered"/> 在读表时就能把拼错的 handler id 抓出来，
/// 而不是等到玩家抽到那一条才在运行时炸。
/// </summary>
public static class RewardEffectRegistry
{
	/// <summary>处理器：把"表里那一行"翻译成一个能套用到本局的委托。</summary>
	public delegate Action<RewardContext> Handler(RewardConfig row);

	private static readonly Dictionary<string, Handler> Handlers = new()
	{
		// 通用的"改 StatSheet"—— 19 条里有 14 条走它，只靠 Modifiers 里的数据表达，不需要新代码。
		["StatSet"] = row => context =>
		{
			foreach (StatOverride entry in row.Modifiers)
			{
				context.Player.Stats.AddModifier(new StatModifier
				{
					Stat = entry.Stat,
					Operation = entry.Operation,
					Value = entry.Value,
					Source = context.Build,
				});
			}

			context.Player.RefreshStats();
		},

		// 异化：击杀触发连锁闪电（数值全部来自表的 HandlerParams）。
		["ChainLightning"] = row => context =>
			context.Player.AddChild(new ChainLightningRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0.12f),
				Damage = Param(row, "Damage", 14f),
				Range = Param(row, "Range", 7f),
				Jumps = (int)Param(row, "Jumps", 3f),
			}),

		// 异化：吸血（比例来自表）。
		["Lifesteal"] = row => context =>
			context.Player.AddChild(new LifestealRule
			{
				RewardId = row.Id,
				Ratio = Param(row, "Ratio", 0.12f),
			}),

		// 异化：命中挂燃烧（时长来自表；每秒伤害在效果表的 Burn 行里）。
		["BurnOnHit"] = row => context =>
			context.Player.AddChild(new BurnOnHitRule
			{
				RewardId = row.Id,
				Duration = Param(row, "Duration", 3f),
				StatusId = ParamText(row, "StatusId", "Burn"),
			}),

		// 异化：击杀召唤（上限与预制体来自表）。
		["SummonOnKill"] = row => context =>
			context.Player.AddChild(new SummonOnKillRule
			{
				RewardId = row.Id,
				SummonScene = LoadScene(ParamText(row, "SummonScene", "res://Scenes/Summons/Drone.tscn")),
				MaxSummons = (int)Param(row, "MaxSummons", 2f),
			}),

		// 异化：击杀补满弹匣（无参数）。
		["AmmoOnKill"] = row => context =>
			context.Player.AddChild(new AmmoOnKillRule { RewardId = row.Id }),

		// 强化「护甲」：护甲没有上限（用户 C1），所以**加甲**走 `Unit.GrantArmor` 这一条（不是 StatSheet 抬高上限）。
		// 数值来自表（`Armor`），组件零个 —— 这是一条**纯表驱动**的处理器。
		["GrantArmor"] = row => context => context.Player.GrantArmor(Param(row, "Armor", 0f)),
	};

	/// <summary>这个 handler id 注册过吗（读表校验用）。</summary>
	public static bool IsRegistered(string handler)
		=> !string.IsNullOrEmpty(handler) && Handlers.ContainsKey(handler);

	/// <summary>所有已注册的 handler id（开场日志用，便于人肉核对表里写了什么）。</summary>
	public static IEnumerable<string> RegisteredIds => Handlers.Keys;

	/// <summary>
	/// 把一行翻译成运行期的 <c>RewardDefinition</c>。
	/// handler 未注册 → 返回 null（调用方警告并跳过这一行，绝不让它静默变成"没有效果的奖励"）。
	/// </summary>
	public static RewardDefinition? Compile(RewardConfig row)
	{
		if (!Handlers.TryGetValue(row.ApplyHandler, out Handler? handler))
		{
			GD.PushWarning($"[Config] 增幅项 {row.Id} 的 ApplyHandler \"{row.ApplyHandler}\" 未注册，已跳过这一行。");
			return null;
		}

		return new RewardDefinition
		{
			Id = row.Id,
			Name = string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName,
			Description = row.DescriptionTemplate,
			Kind = row.RewardKind,
			ApplicablePoolIds = PoolIdsOf(row),
			RequiredWeaponTags = row.RequiredWeaponTags,
			RequiredCharacterIds = row.RequiredCharacterIds,
			Requires = row.Requires,
			ConflictsWith = row.ConflictsWith,
			Excludes = row.Excludes,
			Weight = row.Weight,
			MaxStacks = row.MaxStacks,
			RequiresSummoner = row.RequiresSummoner,
			Apply = handler(row),
		};
	}

	/// <summary>
	/// 把行上的"专属升级池"引用编译成池 Id 列表（<c>RewardDefinition.ApplicablePoolIds</c>）。
	///
	/// 只把 **Id 字符串**交给运行期视图，不带 `Config.UpgradePoolConfig` 这个磁盘类型过去 ——
	/// 运行期视图不认识配置文件类（config-schema.md §2.2 的分层）；而"引用会不会悬空"的问题
	/// 在**磁盘侧**已经由"资源引用"解决了（池行被删 → 这里收到 null → 返回空 → 落到②③两层）。
	/// </summary>
	private static string[] PoolIdsOf(RewardConfig row)
	{
		if (row.Pool == null)
		{
			return Array.Empty<string>();
		}

		if (string.IsNullOrEmpty(row.Pool.Id))
		{
			GD.PushWarning($"[Config] 增幅项 {row.Id} 指向的升级池行没有填 Id，已忽略（这一条会落到「标签匹配 → 通用」两层）。");
			return Array.Empty<string>();
		}

		return new[] { row.Pool.Id };
	}

	/// <summary>读一个数字参数，缺失就用调用方给的兜底值（缺参数不是错误 —— 这行不需要它）。</summary>
	private static float Param(RewardConfig row, string key, float fallback)
	{
		foreach (ParamEntry entry in row.HandlerParams)
		{
			if (entry.Key == key)
			{
				return entry.AsFloat(fallback);
			}
		}

		return fallback;
	}

	/// <summary>读一个文本参数（场景路径 / 状态 Id）。</summary>
	private static string ParamText(RewardConfig row, string key, string fallback)
	{
		foreach (ParamEntry entry in row.HandlerParams)
		{
			if (entry.Key == key)
			{
				return entry.AsText(fallback);
			}
		}

		return fallback;
	}

	private static PackedScene? LoadScene(string path)
		=> string.IsNullOrEmpty(path) ? null : GD.Load<PackedScene>(path);
}
