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

/// <summary>状态预设表：按 Id 取，新增状态只加一行，不在结算代码里分支。</summary>
public static class StatusPresets
{
	public static StatusEffect? Create(string id, float duration)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}

		return id switch
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
}
