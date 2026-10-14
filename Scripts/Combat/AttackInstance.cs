using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 一次攻击的攻击侧快照（战斗规则说明 §3-§6）。
/// 攻击生成时把攻击者当时的攻击侧属性算好，飞行/碰撞期间沿用这份数据；
/// 需要“命中时再读取”的效果属于后续 Trigger/Effect 的职责。
/// </summary>
public sealed class AttackInstance
{
	public Unit? Attacker { get; init; }
	public string Ability { get; init; } = string.Empty;

	public float BaseDamage { get; init; }
	public bool CanCrit { get; init; }
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
		return new HitInstance
		{
			Target = target,
			Attacker = Attacker,
			Attack = this,
			Context = EventContext.Create(Attacker, target),
			Damage = BaseDamage * (critical ? CritMultiplier : 1f),
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
