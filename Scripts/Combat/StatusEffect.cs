using System;

namespace ActRoguelike.Combat;

/// <summary>
/// 状态效果（战斗规则说明 §20）：与伤害本体分离，通过 Modifier 改数值或按秒造成伤害。
/// 同名状态刷新持续时间而不是叠加层数（原型阶段先这么定）。
/// </summary>
public sealed class StatusEffect
{
	public required string Id { get; init; }
	public float Duration { get; init; }
	public StatModifier[] Modifiers { get; init; } = Array.Empty<StatModifier>();
	public float DamagePerSecond { get; init; }
}

/// <summary>单位身上正在生效的状态。</summary>
public sealed class ActiveStatus
{
	public required StatusEffect Effect { get; init; }
	public float Remaining { get; set; }
	public float TickAccumulator { get; set; }
}

/// <summary>
/// 状态预设：按 Id 取，新增状态只加一行，不在结算代码里分支。
///
/// **取值顺序（配置表阶段 2）**：先查效果表（`Config/Tables/Effects/eff_*.tres`，
/// 由 <c>Balance</c> 建索引），读不到才用下面代码内置的兜底 —— 这就是
/// "初始化时读表，读不到则使用自身默认值"那条回退语义。
/// 效果表里的每一行都是 <c>EffectConfig</c>，数值（减速 0.55 / 燃烧 6 dps / 眩晕 0.12·0.5 …）
/// 全部可以在 Inspector 里单独调，不需要重新编译。
/// </summary>
public static class StatusPresets
{
	public static StatusEffect? Create(string id, float duration)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}

		ActRoguelike.Config.EffectConfig? row = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.EffectConfig>(id);
		if (row is { IsStatus: true })
		{
			return row.ToStatus(duration);
		}

		return Fallback(id, duration);
	}

	/// <summary>代码内置兜底目录（表读不到时用；数值与分表前逐位一致）。</summary>
	private static StatusEffect? Fallback(string id, float duration) => id switch
	{
		"Slow" => new StatusEffect
		{
			Id = "Slow",
			Duration = duration,
			Modifiers = new[]
			{
				new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 0.55f },
			},
		},
		"Weaken" => new StatusEffect
		{
			Id = "Weaken",
			Duration = duration,
			Modifiers = new[]
			{
				new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = 0.75f },
			},
		},
		"Burn" => new StatusEffect
		{
			Id = "Burn",
			Duration = duration,
			DamagePerSecond = 6f,
		},
		"Empowered" => new StatusEffect
		{
			Id = "Empowered",
			Duration = duration,
			Modifiers = new[]
			{
				new StatModifier { Stat = "AttackSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 1.3f },
				new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = 1.15f },
			},
		},
		"Stun" => new StatusEffect
		{
			Id = "Stun",
			Duration = duration,
			Modifiers = new[]
			{
				new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 0.12f },
				new StatModifier { Stat = "AttackSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 0.5f },
			},
		},
		_ => null,
	};
}
