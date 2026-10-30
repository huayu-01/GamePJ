using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;

namespace ActRoguelike.Rewards;

/// <summary>旧字符串处理器只在此翻译成定义；新组合效果直接使用资源，不需要注册名字。</summary>
internal static partial class RewardEffectCompatibility
{
	private static readonly Dictionary<string, Func<RewardConfig, RewardEffectDefinition>> Factories = new()
	{
		["StatSet"] = row => new StatRewardEffect { Modifiers = row.Modifiers },
		["Lifesteal"] = row => new LifestealRewardEffect { Ratio = Number(row, "Ratio", .12f) },
		["HappyRunner"] = row => new HappyRunnerRewardEffect { DamageFreeSeconds = Number(row, "DamageFreeSeconds", 10), HealPerSecond = Number(row, "HealPerSecond", 1), PushSpeed = Number(row, "PushSpeed", 5), PushCooldown = Number(row, "PushCooldown", .5f) },
		["JumpPulse"] = row => new JumpPulseRewardEffect { Radius = Number(row, "Radius", 0), Damage = Number(row, "Damage", 0) },
		["KillAirJumps"] = row => new KillAirJumpRewardEffect { ExtraAirJumps = (int)Number(row, "ExtraAirJumps", 0), JumpCap = (int)Number(row, "JumpCap", 0) },
		["ChainLightning"] = row => new ChainLightningRewardEffect { Damage = Number(row, "Damage", 14), Range = Number(row, "Range", 7), Jumps = (int)Number(row, "Jumps", 3), Cooldown = Number(row, "Cooldown", .12f) },
		["MarkedBonus"] = row => new MarkedBonusRewardEffect
		{
			Status = ConfigRows.ById<EffectConfig>(Text(row, "StatusId", "Mark")), LegacyStatusId = Text(row, "StatusId", "Mark"),
			MarkAbility = row.RequiredEventAbility, BonusAbility = row.BonusAgainstAbility, Duration = Number(row, "Duration", 6),
			Cooldown = Number(row, "Cooldown", 0), FlatBonus = Number(row, "FlatBonus", 0), PerStackBonus = Number(row, "PerStackBonus", 0)
		},
		["BurnOnHit"] = row => new StatusOnHitRewardEffect
		{
			Status = ConfigRows.ById<EffectConfig>(Text(row, "StatusId", "Burn")), LegacyStatusId = Text(row, "StatusId", "Burn"),
			Duration = Number(row, "Duration", 3), Cooldown = Number(row, "Cooldown", 0)
		},
	};
	public static bool Contains(string id) => Factories.ContainsKey(id) || Dedicated.ContainsKey(id);
	public static IEnumerable<string> Ids => Factories.Keys.Concat(Dedicated.Keys);
	public static bool TryCompile(RewardConfig row, out Action<RewardContext>? apply)
	{
		apply = null;
		if (Dedicated.TryGetValue(row.ApplyHandler, out var dedicated)) { apply = RewardEffectCompiler.Compile(row, dedicated(row).ToArray()); return true; }
		if (!Factories.TryGetValue(row.ApplyHandler, out var factory)) return false;
		apply = RewardEffectCompiler.Compile(row, new[] { factory(row) }); return true;
	}
	private static float Number(RewardConfig row, string key, float fallback) => row.HandlerParams.FirstOrDefault(p => p.Key == key)?.AsFloat(fallback) ?? fallback;
	private static string Text(RewardConfig row, string key, string fallback) => row.HandlerParams.FirstOrDefault(p => p.Key == key)?.AsText(fallback) ?? fallback;
}
