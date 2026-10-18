using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>出手时冻结的攻击侧属性；基础暴击共读当前武器表，增益与局外成长共读属性表。</summary>
public readonly record struct AttackOffense(float DamageMultiplier, float CritChance, float CritMultiplier)
{
	public static AttackOffense Capture(Unit? owner, float fallbackChance = 0f, float fallbackMultiplier = 2f)
	{
		StatSheet? stats = owner?.Stats;
		bool player = owner?.Faction == Faction.Player;
		float chance = player ? stats!.Get("BaseCritChance") : fallbackChance;
		float multiplier = player ? stats!.Get("BaseCritMultiplier") : fallbackMultiplier;
		return new AttackOffense(stats?.Get("DamageMultiplier") ?? 1f,
			Mathf.Clamp(chance + (stats?.Get("CritChanceAdd") ?? 0f), 0f, 1f),
			Mathf.Max(1f, multiplier + (stats?.Get("CritMultiplierAdd") ?? 0f)));
	}
}

/// <summary>
/// 一次攻击的攻击侧快照（战斗规则说明 §3-§6）。
/// 攻击生成时把攻击者当时的攻击侧属性算好，飞行/碰撞期间沿用这份数据；
/// 需要“命中时再读取”的效果属于后续 Trigger/Effect 的职责。
/// </summary>
public sealed class AttackInstance
{
	public Unit? Attacker { get; init; }
	public string Ability { get; init; } = string.Empty;
	/// <summary>发出攻击时冻结的连招段号与总段数；非连招总段数为0。</summary>
	public int ComboStep { get; init; } = -1;
	public int ComboStepCount { get; init; }

	public float BaseDamage { get; init; }
	public bool CanCrit { get; init; }
	/// <summary>状态周期伤害事实；不作为新主攻击再次触发命中增幅。</summary>
	public bool IsDamageOverTime { get; init; }
	public float CritChance { get; init; }
	public float CritMultiplier { get; init; } = 2f;
	public float Knockback { get; init; }

	public HitPolicy Policy { get; init; } = HitPolicy.OncePerTarget;
	public float HitInterval { get; init; } = 0.2f;
	public float Lifetime { get; init; } = 3f;

	/// <summary>一次射击生成的投射物数量（双管齐下等进化）。</summary>
	public int ProjectileCount { get; init; } = 1;

	/// <summary>可穿透的目标数量（0 = 命中即消失）。</summary>
	public int Pierce { get; init; }

	/// <summary>命中后弹射到附近敌人的次数。</summary>
	public int Ricochet { get; init; }

	/// <summary>命中时生成的爆炸半径，0 = 不爆炸。</summary>
	public float ExplosionRadius { get; init; }

	public float ExplosionDamage { get; init; }

	public string[] Tags { get; init; } = System.Array.Empty<string>();

	/// <summary>
	/// **这一次攻击无视格挡**（文档：能量波"能穿透格挡"，剑气"可被格挡"）。
	/// 由攻击侧按武器/技能配置填，命中结算时走 <c>Unit.ResolveBlock</c> —— 是数据，不是特例分支。
	/// </summary>
	public bool BypassBlock { get; init; }

	/// <summary>
	/// 技能身份（`skl_a1_w1_1` 这类技能行 Id），与 <see cref="Ability"/> 并存的**稳定**身份键。
	/// <see cref="Ability"/> 在武器路径上是 `WeaponFire` 这种字符串常量，增幅/统计需要能区分"哪一个技能"。
	/// </summary>
	public string AbilityKind { get; init; } = string.Empty;

	/// <summary>命中后附加的状态效果（可为空）。</summary>
	public StatusEffect? Status { get; init; }

	public bool HasTag(string tag) => System.Array.IndexOf(Tags, tag) >= 0;

	/// <summary>命中判定通过后生成伤害实例；暴击属于攻击侧结算（战斗规则说明 §11）。</summary>
	public HitInstance RollHit(Unit target, Vector3 direction, Vector3 position)
	{
		bool critical = CanCrit && GD.Randf() < CritChance;

		// ⭐ J8：**命中时再读**的那一项 —— 目标身上"这一击之前"已有的「标记」层数所对应的伤害倍率
		// （增幅「心眼」/「发散」，由攻击者侧登记的 `IOutgoingDamageScale` 提供）。
		//
		// ⚠️ 顺序就是语义（用户 2026-10-17 裁定「按本次命中前已有标记结算，第一次加标这次不加伤」）：
		//    这里在 `HitInstance` 生成**之前**问，而"这一击加的标记"是在之后 `Unit.ReceiveHit` 抛
		//    `Hit` 事件时由规则施加的（`MarkedBonusRule.Execute`）—— 所以这一击自己的标记**不可能**被算进来。
		//    攻击者没有登记任何规则（敌人 / 召唤物 / 其他单位 / 没拿增幅的玩家）→ 恒为 1，改动前行为逐位不变。
		float marked = Attacker is { } attacker && GodotObject.IsInstanceValid(attacker)
			? attacker.OutgoingDamageScaleAgainst(target, this)
			: 1f;

		// ⭐ J11：**扁平加伤**（「巨人」符文 = 每次攻击额外造成玩家最大生命值 10% 的伤害）。
		// 与上面那个倍率分成两个口子，是因为它是一笔"与本次伤害无关的固定量"：
		// 走倍率会被暴击倍率一起放大（见 `IOutgoingDamageBonus` 的注释）。
		// 没有任何规则登记时恒为 0，而 `x + 0f == x`（浮点逐位相等）→ 改动前行为逐位不变。
		float flatBonus = Attacker is { } bonusAttacker && GodotObject.IsInstanceValid(bonusAttacker)
			? bonusAttacker.OutgoingDamageBonusAgainst(target, this)
			: 0f;

		return new HitInstance
		{
			Target = target,
			Attacker = Attacker,
			Attack = this,
			Context = EventContext.Create(Attacker, target),
			Damage = (BaseDamage * (critical ? CritMultiplier : 1f) * marked) + flatBonus,
			Critical = critical,
			Direction = direction,
			Position = position,
			Knockback = Knockback,
			BypassBlock = BypassBlock,
		};
	}
}

/// <summary>按 Hit Policy 记录“这个目标是否还能被本次攻击命中”。</summary>
public sealed class HitTracker
{
	private readonly Dictionary<ulong, float> _lastHitTime = new();
	private readonly Dictionary<ulong, int> _lastPhase = new();

	public void Reset()
	{
		_lastHitTime.Clear();
		_lastPhase.Clear();
	}

	public bool TryRegister(Unit target, HitPolicy policy, float interval, float now, int phase)
	{
		ulong id = target.GetInstanceId();
		switch (policy)
		{
			case HitPolicy.MultiHit:
				break;
			case HitPolicy.OncePerTarget:
				if (_lastHitTime.ContainsKey(id))
				{
					return false;
				}
				break;
			case HitPolicy.IntervalPerTarget:
				if (_lastHitTime.TryGetValue(id, out float last) && now - last < interval)
				{
					return false;
				}
				break;
			case HitPolicy.OncePerPhase:
				if (_lastPhase.TryGetValue(id, out int registered) && registered == phase)
				{
					return false;
				}
				break;
		}

		_lastHitTime[id] = now;
		_lastPhase[id] = phase;
		return true;
	}
}
