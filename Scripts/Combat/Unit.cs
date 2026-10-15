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

	/// <summary>
	/// **开局护甲**（护甲**没有上限** —— 用户 2026-10-12 拍板 C1：「护甲没有上限额度，所以可以删除掉 MaxArmor 的」）。
	///
	/// 这个值只是"进场时给多少甲"：<see cref="_Ready"/> 与 <see cref="FillVitals"/> 把它写进 <see cref="Armor"/>，
	/// 之后护甲就是一个**累加值**（被打就减、<see cref="GrantArmor"/> 可以加），**不存在"最大护甲"**，
	/// 所以它也**不是**护甲条的分母（分母是运行期派生的 <see cref="ArmorPeak"/>）。
	/// </summary>
	[Export] public float StartingArmor { get; set; }

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
	private float _startingArmor;

	/// <summary>
	/// 由 <see cref="GrantArmor"/> 累积的**额外护甲**（精英怪的 <c>EliteModifier.ArmorAdd</c> 走这里）。
	/// 护甲没有上限，所以它不是"上限"、也不进 <c>StatSheet</c>；记一份是为了让
	/// "进树前就加了甲"（子节点 <c>_Ready</c> 早于单位本身）与"进树后加甲"结果一致。
	/// </summary>
	private float _armorBonus;

	private float _baseMaxShield;

	/// <summary>可选的移动能力组件（名为 Movement 的子节点）。没有移动能力的单位该属性为 null。</summary>
	public CharacterMovement? Movement { get; private set; }

	public float Health { get; private set; }

	/// <summary>当前护甲。**没有上限**（用户 C1）：被打就减，只在 <see cref="GrantArmor"/> 时增加。</summary>
	public float Armor { get; private set; }

	/// <summary>
	/// 这个单位**见过的最高护甲值**（运行期派生，只增不减）。
	///
	/// 存在的唯一理由是**表现层需要一个分母**：护甲既然没有上限，<c>Armor</c> 自己没法算出"还剩几成"，
	/// 而护甲条（用户需求 §五 #8 / A6 的那层"塑料"）要显示进度。它**不来自任何配置、不能外部设置、
	/// 不参与战斗结算** —— 严格地说它是"开局护甲 + 加甲"的副产物，不是"最大护甲"这个概念。
	/// </summary>
	public float ArmorPeak { get; private set; }

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

	/// <summary>
	/// **技能动画事件帧的接收口**（生成表 <c>SkillAnimationConfig.EventMethod</c> 写的名字就是它）。
	///
	/// ⚠️ <b>为什么挂在 Unit 上而不是 UnitAnimator 上</b>：方法轨道的 path 是相对
	/// <c>AnimationMixer.RootNode</c> 的，而项目里 <c>AnimationPlayer</c> 在 <c>Model</c>（`.glb` 实例根）
	/// 子树内、<c>root_node</c> 就是 <c>".."</c>，所以轨道 path 写 <c>".."</c> 时**调用落在 Unit 自己身上**。
	/// 这里再转发给 <c>UnitAnimator</c> 的分发口，技能才收得到（见 docs/anim-pipeline.md §3、§4.1）。
	///
	/// 没挂 <c>UnitAnimator</c> 的单位（木桩等）调用它是空操作，不报错。
	/// </summary>
	/// <param name="name">事件名（`ATK` / `Dodge` / `Release` …，来自配置表）。</param>
	public void anim_event(string name)
		=> GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName)?.RaiseAnimationEvent(name);

	public bool IsHostileTo(Unit other) =>
		Faction != Faction.Neutral && other.Faction != Faction.Neutral && Faction != other.Faction;

	/// <summary>
	/// 基础值必须在 _EnterTree 捕获：子节点的 _Ready 早于父节点，
	/// 精英/装备类组件会在自己的 _Ready 里调用 RefreshStats，那时 MaxHealth 已经被改写过。
	/// </summary>
	public override void _EnterTree()
	{
		_baseMaxHealth = MaxHealth;
		_startingArmor = StartingArmor;
		_baseMaxShield = MaxShield;
	}

	/// <summary>
	/// StatSheet 的基础值必须在**任何** <see cref="RefreshStats"/> 之前就位：<c>Get()</c> 对没有基础值的键返回 0，
	/// 所以"进树前先套用配置表的值"（生成敌人时 `row.ApplyTo(unit)` → `AddChild`）会把生命算成
	/// <c>max(1, 生命 × 0) = 1</c>，而 `_EnterTree` 又把这个 1 捕获成基础值 —— 一路静默错到底
	/// （配置表阶段 3 实测踩到：Chaser 生命 45 变成 1）。
	/// 放在构造函数里最省事；`_Ready` 里那次保留（`SetBase` 只改基础值、不动修正器，重复调用是幂等的）。
	/// </summary>
	public Unit() => SeedStats();

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
		FillArmor();
		Shield = MaxShield;
	}

	/// <summary>把基础值替换为配置值并回满（开局套用平衡配置）。</summary>
	public void ApplyBaseStats(float maxHealth, float armor, float maxShield, float moveSpeed)
	{
		_baseMaxHealth = maxHealth;
		MaxHealth = maxHealth;
		_startingArmor = armor;
		StartingArmor = armor;
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
		FillArmor();
		Shield = MaxShield;
	}

	/// <summary>
	/// 护甲归位到"开局护甲 + 累计加甲"（进树 / 回满时调用；护甲没有上限，所以只写一个值、不夹上限）。
	/// </summary>
	private void FillArmor()
	{
		Armor = Mathf.Max(0f, _startingArmor + _armorBonus);
		ArmorPeak = Mathf.Max(ArmorPeak, Armor);
	}

	/// <summary>
	/// **直接加护甲**（护甲没有上限，用户 C1）：精英怪的 <c>EliteModifier.ArmorAdd</c> 走这里，
	/// 取代原来的 <c>MaxArmorAdd</c> 修正器 —— 那个修正是"抬高上限"，现在没有上限这回事了。
	///
	/// 允许在**进树前**调用（精英组件挂在单位下，子节点的 <c>_Ready</c> 早于单位本身的 <c>_Ready</c>）：
	/// 那时先记进 <see cref="_armorBonus"/>，等 <see cref="FillArmor"/> 一并结算。
	/// </summary>
	public void GrantArmor(float amount)
	{
		if (amount <= 0f)
		{
			return;
		}

		_armorBonus += amount;
		Armor = Mathf.Max(0f, Armor + amount);
		ArmorPeak = Mathf.Max(ArmorPeak, Armor);
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
		// ⚠️ 没有 MaxArmorMultiplier / MaxArmorAdd：护甲没有上限（用户 C1），
		// 加甲走 Unit.GrantArmor 这一条路径，不再经 StatSheet 抬高"上限"。
		Stats.SetBase("MaxShieldMultiplier", 1f);
		Stats.SetBase("MaxShieldAdd", 0f);
		Stats.SetBase("DamageReductionAdd", 0f);
		Stats.SetBase("KnockbackMultiplier", 1f);
		// 冷却缩减（CDR）：**单位级常驻**那一路（洞悉 / 速攻这类增幅给单位挂这个修正器）。
		// 它是乘法基数，所以必须有基础值 1 —— 少了会读到 0，变成"零冷却"（见上面那条注意）。
		// 它**不改变任何既有键的语义**，也不参与 RefreshStats 的派生计算（能力在读取时才乘它）。
		Stats.SetBase("CooldownMultiplier", 1f);
	}

	/// <summary>修正器变化后重算派生属性（增益/精英/状态效果都会调用）。</summary>
	public void RefreshStats()
	{
		MaxHealth = Mathf.Max(1f, (_baseMaxHealth * Stats.Get("MaxHealthMultiplier")) + Stats.Get("MaxHealthAdd"));

		// 护甲**不在这里重算**（用户 C1：它是累加值、没有上限）—— 被打掉的甲不会被 RefreshStats 补回来，
		// 加甲只走 GrantArmor。这里只保证不出现负甲。
		Armor = Mathf.Max(0f, Armor);

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
	protected virtual float ResolveBlock(HitInstance hit)
	{
		// "无视格挡"是**这一次命中**的性质（文档：能量波能穿透格挡，剑气不能）。
		// 放在这里而不是给某个子弹类型写死判定，是因为它必须能被任意攻击开启。
		if (hit.BypassBlock)
		{
			return 1f;
		}

		return Block?.ResolveMultiplier(hit) ?? 1f;
	}

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

	/// <summary>
	/// 施加状态效果。
	///
	/// <para><b>默认（<see cref="StatusEffect.MaxStacks"/> ≤ 1）：同名刷新时间，不叠加层数</b> ——
	/// 与改动前逐位一致。</para>
	///
	/// <para><b>可叠加（&gt; 1）</b>：每次施加 <b>+1 层</b>（封顶），时间同样刷新，并按层数重算修正器
	/// （每层一份）。设计文档的「标记」是第一条这样的状态 ——「可叠加且不限定单个目标的 buff」，
	/// 所以层数存在<b>每个单位自己身上</b>（`_statuses` 是单位级容器），天然"不限单个目标"。</para>
	/// </summary>
	public void ApplyStatus(StatusEffect effect)
	{
		if (IsDead)
		{
			return;
		}

		foreach (ActiveStatus existing in _statuses)
		{
			if (existing.Effect.Id != effect.Id)
			{
				continue;
			}

			// ⚠️ 这一行就是"不叠加层数"那条老路径，一个字都没动（默认语义逐位不变）。
			if (!effect.IsStackable)
			{
				existing.Remaining = Mathf.Max(existing.Remaining, effect.Duration);
				return;
			}

			int capped = Mathf.Max(1, effect.MaxStacks);
			int before = existing.Stacks;
			existing.Stacks = Mathf.Min(capped, before + 1);
			existing.Remaining = Mathf.Max(existing.Remaining, effect.Duration);
			existing.Effect = effect;
			if (existing.Stacks != before)
			{
				ApplyStatusModifiers(existing);
			}

			return;
		}

		var active = new ActiveStatus { Effect = effect, Remaining = effect.Duration, Stacks = 1 };
		_statuses.Add(active);
		ApplyStatusModifiers(active);

		RefreshStats();
	}

	/// <summary>
	/// 把某条状态的修正器按**当前层数**写进 <see cref="Stats"/>（每层一份）。
	///
	/// 层数为 1 时与改动前的写法逐字等价（每个 Modifier 加一次，Source = 状态本身）；层数变化时
	/// 先按 Source 撤掉旧的那一份再重加 —— 这就是"可叠加"的落地方式（不需要给 StatSheet 加层数概念）。
	/// </summary>
	private void ApplyStatusModifiers(ActiveStatus active)
	{
		Stats.RemoveBySource(active);
		int stacks = Mathf.Max(1, active.Stacks);
		foreach (StatModifier modifier in active.Effect.Modifiers)
		{
			for (int layer = 0; layer < stacks; layer++)
			{
				Stats.AddModifier(new StatModifier
				{
					Stat = modifier.Stat,
					Operation = modifier.Operation,
					Value = modifier.Value,
					Source = active,
				});
			}
		}
	}

	/// <summary>某个状态的**层数**（没有这条状态返回 0）。可叠加的「标记」靠它读。</summary>
	public int GetStatusStacks(string statusId)
	{
		if (string.IsNullOrEmpty(statusId))
		{
			return 0;
		}

		foreach (ActiveStatus status in _statuses)
		{
			if (status.Effect.Id == statusId)
			{
				return Mathf.Max(1, status.Stacks);
			}
		}

		return 0;
	}

	/// <summary>身上有没有这条状态。</summary>
	public bool HasStatus(string statusId) => GetStatusStacks(statusId) > 0;

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

