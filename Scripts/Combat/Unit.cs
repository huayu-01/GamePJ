using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 统一战斗单位（执行文档 §4.2）。玩家、敌人、精英、Boss、召唤物共用这一层：
/// 生命/护甲/护盾、受伤与防御结算、死亡标记、击退与事件。
/// 行为与决策放在 Controller / Behavior / Ability，不写进这里。
/// </summary>
public partial class Unit : CharacterBody3D
{
	[Export] public Faction Faction { get; set; } = Faction.Enemy;

	[Export] public float MaxHealth { get; set; } = 100f;
	[Export] public float MaxArmor { get; set; }
	[Export] public float MaxShield { get; set; }

	/// <summary>护甲大于 0 时提供的伤害减免。</summary>
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float ArmorDamageReduction { get; set; } = 0.3f;

	/// <summary>护甲之外的额外减伤，与护甲减伤按 1-(1-a)(1-b) 叠加，总量上限 90%。</summary>
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float DamageReduction { get; set; }

	/// <summary>击退抗性，0 = 全额吃击退，1 = 完全免疫。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float KnockbackResistance { get; set; }

	/// <summary>用于兼容性判断的标签（Melee/Gun/Boss/Elite...），不在通用代码里绑定具体单位 ID。</summary>
	[Export] public string[] Tags { get; set; } = Array.Empty<string>();

	/// <summary>AI/输入用的基础移动速度；增益通过 Stats 的 MoveSpeedMultiplier 放大。</summary>
	[Export] public float MoveSpeed { get; set; } = 4f;

	/// <summary>属性表：基础值 + 修正器（Modifier）。增益系统通过它改数值，不改具体脚本。</summary>
	public StatSheet Stats { get; } = new();

	/// <summary>当前生效的状态效果。</summary>
	public IReadOnlyList<ActiveStatus> Statuses => _statuses;

	private readonly List<ActiveStatus> _statuses = new();
	private BlockComponent? _block;

	private float _baseMaxHealth;
	private float _baseMaxArmor;
	private float _baseMaxShield;

	/// <summary>可选的移动能力组件（名为 Movement 的子节点）。没有移动能力的单位该属性为 null。</summary>
	public CharacterMovement? Movement { get; private set; }

	public float Health { get; private set; }
	public float Armor { get; private set; }
	public float Shield { get; private set; }
	public bool IsDead { get; private set; }
	public bool IsInvulnerable { get; set; }

	public event Action<HitInstance>? HitReceived;
	public event Action<DamageResult, HitInstance>? Damaged;
	public event Action<HitInstance>? Died;
	public event Action<Unit, HitInstance>? KillConfirmed;
	public event Action? ArmorBroke;
	public event Action? ShieldBroke;
	public event Action<Vector3>? KnockbackReceived;

	public bool IsValidTarget => !IsDead;
	public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;

	public bool IsHostileTo(Unit other) =>
		Faction != Faction.Neutral && other.Faction != Faction.Neutral && Faction != other.Faction;

	/// <summary>
	/// 基础值必须在 _EnterTree 捕获：子节点的 _Ready 早于父节点，
	/// 精英/装备类组件会在自己的 _Ready 里调用 RefreshStats，那时 MaxHealth 已经被改写过。
	/// </summary>
	public override void _EnterTree()
	{
		_baseMaxHealth = MaxHealth;
		_baseMaxArmor = MaxArmor;
		_baseMaxShield = MaxShield;
	}

	public override void _Ready()
	{
		SeedStats();
		Movement = GetNodeOrNull<CharacterMovement>("Movement");
		CollisionLayer = CombatLayers.Of(Faction);
		// 玩家会被敌人挡住；敌人之间不互相阻挡，避免成堆卡死。
		CollisionMask = Faction == Faction.Player ? CombatLayers.World | CombatLayers.UnitMask : CombatLayers.World;
		// 召唤物单独归组：避免被当成玩家（ResolvePlayer 之类只认 player_units），
		// 也不会被敌人的目标选择盯上（Targeting 按阵营选组）。
		AddToGroup(HasTag("Summon") ? "summon_units" : Faction == Faction.Player ? "player_units" : "enemy_units");
		RefreshStats();
		Health = MaxHealth;
		Armor = MaxArmor;
		Shield = MaxShield;
	}

	/// <summary>把基础值替换为配置值并回满（开局套用平衡配置）。</summary>
	public void ApplyBaseStats(float maxHealth, float maxArmor, float maxShield, float moveSpeed)
	{
		_baseMaxHealth = maxHealth;
		MaxHealth = maxHealth;
		_baseMaxArmor = maxArmor;
		MaxArmor = maxArmor;
		_baseMaxShield = maxShield;
		MaxShield = maxShield;
		MoveSpeed = moveSpeed;
		RefreshStats();
		FillVitals();
	}

	/// <summary>回满生命/护甲/护盾（套用全局倍率后调用）。</summary>
	public void FillVitals()
	{
		Health = MaxHealth;
		Armor = MaxArmor;
		Shield = MaxShield;
	}

	private void SeedStats()
	{
		// 注意：凡是会被当作除数或乘法基数的键都必须有基础值。
		// 少了基础值就会读到 0，除下来变成「装填 7 秒」这种离谱结果（踩过一次）。
		Stats.SetBase("DamageMultiplier", 1f);
		Stats.SetBase("AttackSpeedMultiplier", 1f);
		Stats.SetBase("MoveSpeedMultiplier", 1f);
		Stats.SetBase("ReloadSpeedMultiplier", 1f);
		Stats.SetBase("MagazineSizeAdd", 0f);
		Stats.SetBase("ProjectileCountAdd", 0f);
		Stats.SetBase("PierceAdd", 0f);
		Stats.SetBase("RicochetAdd", 0f);
		Stats.SetBase("ExplosionRadiusAdd", 0f);
		Stats.SetBase("ExplosionDamageScale", 0f);
		Stats.SetBase("CritChanceAdd", 0f);
		Stats.SetBase("CritMultiplierAdd", 0f);
		Stats.SetBase("MaxHealthMultiplier", 1f);
		Stats.SetBase("MaxHealthAdd", 0f);
		Stats.SetBase("MaxArmorMultiplier", 1f);
		Stats.SetBase("MaxArmorAdd", 0f);
		Stats.SetBase("MaxShieldMultiplier", 1f);
		Stats.SetBase("MaxShieldAdd", 0f);
		Stats.SetBase("DamageReductionAdd", 0f);
		Stats.SetBase("KnockbackMultiplier", 1f);
	}

	/// <summary>修正器变化后重算派生属性（增益/精英/状态效果都会调用）。</summary>
	public void RefreshStats()
	{
		MaxHealth = Mathf.Max(1f, (_baseMaxHealth * Stats.Get("MaxHealthMultiplier")) + Stats.Get("MaxHealthAdd"));
		float armorMax = Mathf.Max(0f, (_baseMaxArmor * Stats.Get("MaxArmorMultiplier")) + Stats.Get("MaxArmorAdd"));
		if (armorMax > MaxArmor && Armor > 0f)
		{
			Armor += armorMax - MaxArmor;
		}

		MaxArmor = armorMax;
		Armor = Mathf.Min(Armor, MaxArmor);

		float shieldMax = Mathf.Max(0f, (_baseMaxShield * Stats.Get("MaxShieldMultiplier")) + Stats.Get("MaxShieldAdd"));
		if (shieldMax > MaxShield && Shield > 0f)
		{
			Shield += shieldMax - MaxShield;
		}

		MaxShield = shieldMax;
		Shield = Mathf.Min(Shield, MaxShield);
		Health = Mathf.Min(Health, MaxHealth);
	}

	public float CombinedDamageReduction()
	{
		float armorReduction = Armor > 0f ? ArmorDamageReduction : 0f;
		float extra = Mathf.Clamp(DamageReduction + Stats.Get("DamageReductionAdd"), 0f, 0.9f);
		float total = 1f - (1f - armorReduction) * (1f - extra);
		return Mathf.Clamp(total, 0f, 0.9f);
	}

	/// <summary>统一命中入口。顺序见 战斗规则说明 §12：命中 → 无敌 → 格挡 → 减伤 → 护甲 → 护盾 → 生命。</summary>
	public DamageResult ReceiveHit(HitInstance hit)
	{
		if (IsDead)
		{
			return DamageResult.Miss;
		}

		// OnHit 与 OnDamage 分开：命中无敌单位也算命中（§13）。
		HitReceived?.Invoke(hit);
		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Hit,
			Source = hit.Attacker,
			Target = this,
			Value = hit.Damage,
			Tag = hit.Attack?.Ability ?? string.Empty,
			Position = hit.Position,
			Context = hit.Context,
		});

		if (IsInvulnerable)
		{
			return new DamageResult { Hit = true, Invulnerable = true };
		}

		float blocked = ResolveBlock(hit);
		float damage = Mathf.Max(0f, hit.Damage * blocked * (1f - CombinedDamageReduction()));

		float remaining = damage;
		float armorLost = 0f;
		float shieldLost = 0f;
		float healthLost = 0f;

		if (Armor > 0f)
		{
			armorLost = Mathf.Min(Armor, remaining);
			Armor -= armorLost;
			remaining -= armorLost;
			if (Armor <= 0f)
			{
				ArmorBroke?.Invoke();
			}
		}

		if (remaining > 0f && Shield > 0f)
		{
			shieldLost = Mathf.Min(Shield, remaining);
			Shield -= shieldLost;
			remaining -= shieldLost;
			if (Shield <= 0f)
			{
				ShieldBroke?.Invoke();
			}
		}

		if (remaining > 0f)
		{
			healthLost = Mathf.Min(Health, remaining);
			Health -= healthLost;
		}

		bool killed = Health <= 0f;
		var result = new DamageResult
		{
			Hit = true,
			FinalDamage = damage,
			ArmorLost = armorLost,
			ShieldLost = shieldLost,
			HealthLost = healthLost,
			Blocked = blocked < 1f,
			Critical = hit.Critical,
			Killed = killed,
		};

		Damaged?.Invoke(result, hit);

		// 状态效果与伤害本体分离：命中就附加，与伤害数值无关（§20）。
		if (hit.Attack?.Status is { } status)
		{
			ApplyStatus(status);
		}

		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Damaged,
			Source = hit.Attacker,
			Target = this,
			Value = result.HealthLost,
			Tag = hit.Attack?.Ability ?? string.Empty,
			Position = hit.Position,
			Context = hit.Context,
		});

		if (killed)
		{
			Die(hit);
		}
		else if (damage > 0f && hit.Knockback > 0f)
		{
			float scale = Stats.Get("KnockbackMultiplier");
			KnockbackReceived?.Invoke(hit.Direction.Normalized() * hit.Knockback * scale);
		}

		return result;
	}

	/// <summary>格挡是多单位可用的独立机制（§14）；实际判定在 Block 子组件上，没有就不格挡。</summary>
	protected virtual float ResolveBlock(HitInstance hit) => Block?.ResolveMultiplier(hit) ?? 1f;

	/// <summary>格挡组件（名为 Block 的子节点），守卫类单位使用。</summary>
	public BlockComponent? Block => _block ??= GetNodeOrNull<BlockComponent>("Block");

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead || _statuses.Count == 0)
		{
			return;
		}

		float dt = (float)delta;
		for (int i = _statuses.Count - 1; i >= 0; i--)
		{
			ActiveStatus status = _statuses[i];
			status.Remaining -= dt;

			if (status.Effect.DamagePerSecond > 0f)
			{
				status.TickAccumulator += dt;
				if (status.TickAccumulator >= 1f)
				{
					status.TickAccumulator -= 1f;
					ReceiveHit(new HitInstance
					{
						Target = this,
						Context = EventContext.Create(null, this),
						Damage = status.Effect.DamagePerSecond,
						Direction = Vector3.Up,
					});
				}
			}

			if (status.Remaining <= 0f)
			{
				Stats.RemoveBySource(status);
				_statuses.RemoveAt(i);
				RefreshStats();
			}
		}
	}

	/// <summary>当前生效的状态 Id 列表（便于调试与 MCP 验证）。</summary>
	public string[] GetStatusIds()
	{
		var ids = new string[_statuses.Count];
		for (int i = 0; i < _statuses.Count; i++)
		{
			ids[i] = _statuses[i].Effect.Id;
		}

		return ids;
	}

	/// <summary>施加状态效果；同名刷新时间，不叠加层数。</summary>
	public void ApplyStatus(StatusEffect effect)
	{
		if (IsDead)
		{
			return;
		}

		foreach (ActiveStatus existing in _statuses)
		{
			if (existing.Effect.Id == effect.Id)
			{
				existing.Remaining = Mathf.Max(existing.Remaining, effect.Duration);
				return;
			}
		}

		var active = new ActiveStatus { Effect = effect, Remaining = effect.Duration };
		_statuses.Add(active);
		foreach (StatModifier modifier in effect.Modifiers)
		{
			Stats.AddModifier(new StatModifier
			{
				Stat = modifier.Stat,
				Operation = modifier.Operation,
				Value = modifier.Value,
				Source = active,
			});
		}

		RefreshStats();
	}

	public float Heal(float amount)
	{
		if (IsDead || amount <= 0f)
		{
			return 0f;
		}

		float healed = Mathf.Min(amount, MaxHealth - Health);
		Health += healed;
		return healed;
	}

	/// <summary>立即进入死亡状态：逻辑死亡与视觉死亡分离（§18）。</summary>
	private void Die(HitInstance hit)
	{
		IsDead = true;
		Health = 0f;
		CollisionLayer = 0;
		Died?.Invoke(hit);
		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Died,
			Source = hit.Attacker,
			Target = this,
			Position = GlobalPosition,
			Context = hit.Context,
		});

		if (hit.Attacker != null)
		{
			hit.Attacker.NotifyKill(this, hit);
			GameplayEventBus.Raise(new GameplayEvent
			{
				Kind = GameplayEventKind.Kill,
				Source = hit.Attacker,
				Target = this,
				Position = GlobalPosition,
				Context = hit.Context,
			});
		}
	}

	/// <summary>
	/// 直接造成伤害（环境伤害、DoT、脚本事件用），走统一结算流程。
	/// 返回实际扣掉的生命值（返回值保持 Variant 兼容，便于脚本调用与验证）。
	/// </summary>
	public float ApplyDirectDamage(float amount, Unit? source = null)
	{
		if (IsDead || amount <= 0f)
		{
			return 0f;
		}

		return ReceiveHit(new HitInstance
		{
			Target = this,
			Attacker = source,
			Context = EventContext.Create(source, this),
			Damage = amount,
			Direction = Vector3.Up,
			Position = GlobalPosition,
		}).HealthLost;
	}

	/// <summary>自爆、献祭、脚本击杀等不经过普通伤害的死亡路径。</summary>
	public void ForceKill(Unit? killer = null)
	{
		if (IsDead)
		{
			return;
		}

		var hit = new HitInstance
		{
			Target = this,
			Attacker = killer,
			Context = EventContext.Create(killer, this),
		};
		Damaged?.Invoke(new DamageResult { Hit = true, Killed = true }, hit);
		Die(hit);
	}

	private void NotifyKill(Unit victim, HitInstance hit) => KillConfirmed?.Invoke(victim, hit);
}
