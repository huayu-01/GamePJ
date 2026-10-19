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
		CollisionLayer = CombatLayers.BodyLayerOf(Faction);
		// 共用身体层负责单位间阻挡；阵营层只供攻击盒和弹道选择目标。
		CollisionMask = CombatLayers.BodyMask;
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
		Armor = _armorHealthRatio > 0f ? 0f : Mathf.Max(0f, _startingArmor + _armorBonus);
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

		if (_armorHealthRatio > 0f) { ConvertArmorToHealth(amount); return; }
		_armorBonus += amount;
		Armor = Mathf.Max(0f, Armor + amount);
		ArmorPeak = Mathf.Max(ArmorPeak, Armor);
	}
	private float _armorHealthRatio;
	public void ResetArmorHealthConversion()
	{
		_armorHealthRatio = 0f; Stats.RemoveBySource(this);
	}
	/// <summary>血铠：已有甲立刻转换，以后所有GrantArmor同路转换。</summary>
	public void ConfigureArmorHealthConversion(float ratio)
	{
		_armorHealthRatio = Mathf.Max(0f, ratio);
		if (_armorHealthRatio <= 0f) return;
		float armor = Armor; Armor = 0f;
		ConvertArmorToHealth(armor);
	}
	private void ConvertArmorToHealth(float amount)
	{
		float health = amount * _armorHealthRatio;
		if (health <= 0f) return;
		Stats.AddModifier(new StatModifier { Stat = "MaxHealthAdd", Value = health, Source = this });
		RefreshStats();
	}

	private void SeedStats()
	{
		// 注意：凡是会被当作除数或乘法基数的键都必须有基础值。
		// 少了基础值就会读到 0，除下来会算出离谱结果（踩过一次：装填时间变成 7 秒）。
		// ⚠️ P6b：`ReloadSpeedMultiplier` / `MagazineSizeAdd` 两个键**随弹匣/装填一起删除**
		//（全项目已无读取端 —— `Weapon` 的装填逻辑、两张能力卡、`meta_reload`、局外加成都清了）。
		Stats.SetBase("DamageMultiplier", 1f);
		Stats.SetBase("AttackSpeedMultiplier", 1f);
		Stats.SetBase("MoveSpeedMultiplier", 1f);
		Stats.SetBase("ProjectileCountAdd", 0f);
		Stats.SetBase("PierceAdd", 0f);
		Stats.SetBase("RicochetAdd", 0f);
		Stats.SetBase("ExplosionRadiusAdd", 0f);
		Stats.SetBase("ExplosionDamageScale", 0f);
		Stats.SetBase("CritChanceAdd", 0f);
		// 玩家出手共读武器表基础值；构造期兜底，配装时由当前武器行覆盖。
		Stats.SetBase("BaseCritChance", 0.05f);
		Stats.SetBase("BaseCritMultiplier", 2f);
		Stats.SetBase("CritMultiplierAdd", 0f);
		Stats.SetBase("MaxHealthMultiplier", 1f);
		Stats.SetBase("MaxHealthAdd", 0f);
		// ⚠️ 没有 MaxArmorMultiplier / MaxArmorAdd：护甲没有上限（用户 C1），
		// 加甲走 Unit.GrantArmor 这一条路径，不再经 StatSheet 抬高"上限"。
		Stats.SetBase("MaxShieldMultiplier", 1f);
		Stats.SetBase("MaxShieldAdd", 0f);
		Stats.SetBase("DamageReductionAdd", 0f);
		Stats.SetBase("KnockbackMultiplier", 1f);
		// 跳跃高度倍率（强化「攀高」）：1 = 不变。它是乘法基数，必须有基础值。
		// 消费者是 `CharacterMovement.EffectiveJumpVelocity`（只作用在玩家/单位的 `TryJump` 上，
		// 不改敌人扑击那些各自场景上的固定值）。
		Stats.SetBase("JumpVelocityMultiplier", 1f);
		// 近战攻击范围倍率（异化「大刀阔斧」）——消费者是 `MeleeComboAbility.MeleeRangeMultiplier`。
		Stats.SetBase("AttackRangeMultiplier", 1f);
		// 近战突进距离（异化「剑突」）——消费者是 `MeleeComboAbility.BeginLunge`；0 = 不突进（默认）。
		Stats.SetBase("MeleeLungeAdd", 0f);
		// 蓄力时间的单位级覆盖（`<A2限定>`「心流」/「花心」）——0 = 不覆盖（默认），
		// 消费者是 `Ability.ChargeBaseSecondsEffective` / `ChargeStepSecondsEffective`。
		Stats.SetBase("ChargeBaseSecondsOverride", 0f);
		Stats.SetBase("ChargeStepSecondsOverride", 0f);
		Stats.SetBase("ChargeStepSecondsMultiplier", 1f);
		// 齐射发数覆盖（`<A2限定>`「专一」）——0 = 不覆盖（默认）；消费者是 `ArrowVolleyAbility.FireVolley`。
		Stats.SetBase("VolleyCountOverride", 0f);
		Stats.SetBase("VolleyDamagePerLostArrow", 0f);
		// 冷却缩减（CDR）：**单位级常驻**那一路（洞悉 / 速攻这类增幅给单位挂这个修正器）。
		// 它是乘法基数，所以必须有基础值 1 —— 少了会读到 0，变成"零冷却"（见上面那条注意）。
		// 它**不改变任何既有键的语义**，也不参与 RefreshStats 的派生计算（能力在读取时才乘它）。
		Stats.SetBase("CooldownMultiplier", 1f);

		// ── J11（符文系统）新增的四个键。**全部是"只加不改"的键**：
		//    基础值取中性值，没有任何符文时读取端得到的就是改动前的那个数（见每条的说明）。
		// ① 生命上限**硬夹**（符文「雷之印」：最大生命值限制为 30）。
		//    0 = 不夹（默认）。>0 时 RefreshStats 把 MaxHealth 夹到该值。
		Stats.SetBase("MaxHealthCap", 0f);
		// ② 受到伤害的倍率（符文「战神印」：受到伤害 +300% → 填 4 = 承受 4 倍）。
		//    1 = 不变（默认）；乘法基数，所以必须有基础值。消费者：ReceiveHit。
		Stats.SetBase("IncomingDamageMultiplier", 1f);
		// ③ 正常下落速度上限的倍率（符文「飞翼」：降低正常下落速度 → 填 0.8）。
		//    1 = 不变（默认）；消费者：CharacterMovement 的下落夹速。
		Stats.SetBase("MaxFallSpeedMultiplier", 1f);
		// 下落攻击速度独立于正常下落夹速；飞翼等只需在表中加乘法修饰器。
		Stats.SetBase("DiveSpeedMultiplier", 1f);
		// ④ 治疗量的倍率（符文「死铠」：无法恢复生命值 → 填 0）。
		//    1 = 不变（默认）；≤0 = 完全禁疗。消费者：Heal。
		//    ⚠️ 因为"0"在这里是**有意义的值**（禁疗）而不是"没配"，所以基础值必须在构造函数里就位
		//    （SeedStats 在 `Unit()` 里无条件跑一次），读取端不能再用"读到 0 就按 1 算"那套兜底。
		Stats.SetBase("HealMultiplier", 1f);
	}

	/// <summary>修正器变化后重算派生属性（增益/精英/状态效果都会调用）。</summary>
	public void RefreshStats()
	{
		MaxHealth = Mathf.Max(1f, (_baseMaxHealth * Stats.Get("MaxHealthMultiplier")) + Stats.Get("MaxHealthAdd"));

		// ⭐ J11：**生命上限硬夹**（符文「雷之印」= 最大生命值限制为 30）。
		// `MaxHealthCap` 的基础值是 0 = 不夹；没有任何符文时这一段不进，
		// 上面那行表达式原样保留（逐位不变）。
		float healthCap = Stats.Get("MaxHealthCap");
		if (healthCap > 0f)
		{
			MaxHealth = Mathf.Max(1f, Mathf.Min(MaxHealth, healthCap));
		}

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

	/// <summary>纯击退入口，复用单位乘区、无敌与移动组件抗性。</summary>
	public bool ReceiveKnockback(Vector3 impulse)
	{
		if (IsDead || IsInvulnerable || impulse.LengthSquared() < 0.000001f) return false;
		// 纯击退不伪造伤害／命中事件；抗性仍由移动组件消费。
		KnockbackReceived?.Invoke(impulse * Stats.Get("KnockbackMultiplier"));
		return true;
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
			IsSecondary = hit.Attack?.IsSecondary == true,
			IsDamageOverTime = hit.Attack?.IsDamageOverTime == true,
			Source = hit.Attacker,
			Target = this,
			Value = hit.Damage,
			Tag = hit.Attack?.Ability ?? string.Empty,
			Position = hit.Position,
			Context = hit.Context,
		});

		if (IsInvulnerable || hit.Negated)
		{
			return new DamageResult { Hit = true, Invulnerable = true };
		}

		float blocked = ResolveBlock(hit);
		// ⭐ J11：「战神印」的"受到的伤害 +300%"（= 承受 4 倍）。基础值 1，`x × 1.0f` 与改动前逐位相等。
		float incoming = Stats.Get("IncomingDamageMultiplier");
		float damage = Mathf.Max(0f, hit.Damage * blocked * (1f - CombinedDamageReduction()) * (incoming > 0f ? incoming : 1f));

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
				// P6a：把"护甲被打光"抛到事件总线 —— **抛点唯一**（就在这一处，与 `Parried` 同一个标准）。
				// 在此之前 `ArmorBroken` 这个枚举值全项目一个抛点都没有（只有 `GameLog` 的 switch 认得它），
				// 所以订它的增幅（如「能量护盾」的"护甲破碎"那一路）根本收不到事件。
				GameplayEventBus.Raise(new GameplayEvent
				{
					Kind = GameplayEventKind.ArmorBroken,
					Source = hit.Attacker,
					Target = this,
					Value = armorLost,
					Tag = hit.Attack?.Ability ?? string.Empty,
					Position = hit.Position,
					Context = hit.Context,
				});
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
				// P6a：同上 —— `ShieldBroken` 的**唯一**抛点（护盾被打光那一刻）。
				GameplayEventBus.Raise(new GameplayEvent
				{
					Kind = GameplayEventKind.ShieldBroken,
					Source = hit.Attacker,
					Target = this,
					Value = shieldLost,
					Tag = hit.Attack?.Ability ?? string.Empty,
					Position = hit.Position,
					Context = hit.Context,
				});
			}
		}

		if (remaining > 0f)
		{
			healthLost = Mathf.Min(Health, remaining);
			Health -= healthLost;
		}

		bool killed = Health <= 0f;

		// ⭐ J11：**死亡避免**（符文「生死」= 无效化这次致命伤并恢复 30% 生命）。
		// 钩子必须落在 `killed` 判定之后、`result` 构造之前 —— 见 IDeathAvoid 的注释
		// （`Damaged` 事件是在 `killed` 算完之后才抛的，那时已经拦不住 `Die`）。
		// 没有登记任何规则时 `_deathAvoids.Count == 0`，这一段不进（改动前逐位不变）。
		if (killed && _deathAvoids.Count > 0)
		{
			foreach (IDeathAvoid avoid in _deathAvoids.ToArray())
			{
				if (!avoid.TryAvoidDeath(this))
				{
					continue;
				}

				killed = false;
				break;
			}
		}

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
			ApplyStatus(status, hit.Attacker, string.IsNullOrEmpty(hit.Attack.AbilityKind) ? hit.Attack.Ability : hit.Attack.AbilityKind);
		}

		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Damaged,
			IsSecondary = hit.Attack?.IsSecondary == true,
			IsDamageOverTime = hit.Attack?.IsDamageOverTime == true,
			Source = hit.Attacker,
			Target = this,
			Value = result.HealthLost,
			Tag = hit.Attack?.Ability ?? string.Empty,
			Position = hit.Position,
			Context = hit.Context,
		});

		// P6a：暴击命中 → **唯一的** `Critical` 抛点（就在结算完这一次命中之后）。
		// 判据用的是 `HitInstance.Critical`（= 攻击方按 `CritChance` 掷出来的那一次），
		// 不是"伤害数字大" —— 语义与 `DamageResult.Critical` 同一份来源，不会漂移。
		// 触发者判据（Source == 玩家）交给订阅方（`TablePulseRule` 的 `mine`），所以敌人的暴击也不会误触发玩家的增幅。
		if (hit.Critical && damage > 0f)
		{
			GameplayEventBus.Raise(new GameplayEvent
			{
				Kind = GameplayEventKind.Critical,
				IsSecondary = hit.Attack?.IsSecondary == true,
				Source = hit.Attacker,
				Target = this,
				Value = damage,
				Tag = hit.Attack?.Ability ?? string.Empty,
				Position = hit.Position,
				Context = hit.Context,
			});
		}

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
					Unit? source = status.Source is { } original && IsInstanceValid(original) ? original : null;
					var attack = new AttackInstance
					{
						Attacker = source, Ability = $"Status:{status.Effect.Id}", AbilityKind = status.SourceAbilityKind,
						BaseDamage = status.Effect.DamagePerSecond, IsDamageOverTime = true,
						Tags = new[] { "Status", "DamageOverTime" },
					};
					ReceiveHit(new HitInstance
					{
						Target = this,
						Attacker = source,
						Attack = attack,
						Context = EventContext.Create(source, this),
						Damage = status.Effect.DamagePerSecond,
						Direction = Vector3.Up,
						Position = GlobalPosition,
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
	public void ApplyStatus(StatusEffect effect, Unit? source = null, string sourceAbilityKind = "")
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
			// 刷新归属于这次施加者；环境／未知来源刷新也不能冒领旧玩家的后续击杀。
			existing.Source = source;
			existing.SourceAbilityKind = sourceAbilityKind;

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

		var active = new ActiveStatus { Effect = effect, Remaining = effect.Duration, Stacks = 1, Source = source, SourceAbilityKind = sourceAbilityKind };
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
	/// <summary>移除已主动引爆的状态并撤销其修正器。</summary>
	public void RemoveStatus(string statusId)
	{
		for (int i = _statuses.Count - 1; i >= 0; i--)
			if (_statuses[i].Effect.Id == statusId) { Stats.RemoveBySource(_statuses[i]); _statuses.RemoveAt(i); }
		RefreshStats();
	}

	/// <summary>
	/// 这个单位作为**攻击者**登记的"按目标状态加伤"规则（J8：增幅「心眼」「发散」）。
	/// 只由规则节点自己在 <c>_Ready</c> / <c>_ExitTree</c> 成对登记/注销 —— 不做子树扫描（免扫描开销、
	/// 也不会因为"规则比第一次命中晚挂上"而读到过期缓存）。
	/// </summary>
	private readonly List<IOutgoingDamageScale> _outgoingDamageScales = new();

	/// <summary>登记一条攻击者侧伤害缩放（幂等：同一实例不会进两次）。</summary>
	public void AddOutgoingDamageScale(IOutgoingDamageScale scale)
	{
		if (!_outgoingDamageScales.Contains(scale))
		{
			_outgoingDamageScales.Add(scale);
		}
	}

	/// <summary>注销（规则退树时调用；不注销会让已释放的节点留在表里）。</summary>
	public void RemoveOutgoingDamageScale(IOutgoingDamageScale scale) => _outgoingDamageScales.Remove(scale);

	/// <summary>
	/// 这个单位作为**攻击者**登记的"扁平加伤"规则（J11：「巨人」符文）。
	/// 与 <see cref="_outgoingDamageScales"/> 同一条纪律：只由规则节点自己在 `_Ready` / `_ExitTree`
	/// 成对登记/注销。**没有任何登记时恒为 0**（改动前逐位不变）。
	/// </summary>
	private readonly List<IOutgoingDamageBonus> _outgoingDamageBonuses = new();

	/// <summary>登记一条攻击者侧扁平加伤（幂等：同一实例不会进两次）。</summary>
	public void AddOutgoingDamageBonus(IOutgoingDamageBonus bonus)
	{
		if (!_outgoingDamageBonuses.Contains(bonus))
		{
			_outgoingDamageBonuses.Add(bonus);
		}
	}

	/// <summary>注销（规则退树时调用）。</summary>
	public void RemoveOutgoingDamageBonus(IOutgoingDamageBonus bonus) => _outgoingDamageBonuses.Remove(bonus);

	/// <summary>
	/// 本次命中的**额外伤害**（0 = 不变；多条规则相加）。
	/// 调用点只有 <see cref="AttackInstance.RollHit"/> 一处，位置在既有的倍率结算之后。
	/// </summary>
	public float OutgoingDamageBonusAgainst(Unit target, AttackInstance attack)
	{
		if (_outgoingDamageBonuses.Count == 0)
		{
			return 0f;
		}

		float bonus = 0f;
		foreach (IOutgoingDamageBonus rule in _outgoingDamageBonuses)
		{
			bonus += rule.BonusAgainst(target, attack);
		}

		return bonus;
	}

	/// <summary>
	/// 这个单位登记的**死亡避免**规则（J11：「生死」符文）。
	/// **没有登记时 <c>ReceiveHit</c> 里那一段整段不进** —— 改动前"血量归零即死"逐位不变。
	/// </summary>
	private readonly List<IDeathAvoid> _deathAvoids = new();

	/// <summary>登记一条死亡避免（幂等）。</summary>
	public void AddDeathAvoid(IDeathAvoid avoid)
	{
		if (!_deathAvoids.Contains(avoid))
		{
			_deathAvoids.Add(avoid);
		}
	}

	/// <summary>注销死亡避免（规则退树时调用）。</summary>
	public void RemoveDeathAvoid(IDeathAvoid avoid) => _deathAvoids.Remove(avoid);

	/// <summary>
	/// **把生命值直接设成某个数**（J11：「生死」符文在致命伤那一刻恢复到最大生命值的 30%）。
	///
	/// <para>为什么需要它：<see cref="Health"/> 的 setter 是私有的（外部只能通过
	/// <see cref="Heal"/> / <see cref="ApplyDirectDamage"/> 改），而"死亡避免"要把生命**从 0 拉回**一个值 ——
	/// <see cref="Heal"/> 在那一刻算得出 <c>MaxHealth - Health</c>，但它读的是 <c>HealMultiplier</c>
	/// （「死铠」的禁疗也会拦住它，那是错的：死亡避免不是治疗）。所以单开一个受控的口子，并且
	/// **只在 <see cref="IDeathAvoid"/> 的实现里用**（其它地方请继续走 Heal / ReceiveHit）。</para>
	/// </summary>
	public void RestoreHealthForDeathAvoid(float health)
	{
		if (IsDead)
		{
			return;
		}

		Health = Mathf.Clamp(health, 0f, Mathf.Max(0f, MaxHealth));
	}

	/// <summary>当前还剩几条死亡避免规则登记着（自测/日志用）。</summary>
	public int DeathAvoidRuleCount => _deathAvoids.Count;

	/// <summary>
	/// 本次命中对 <paramref name="target"/> 的伤害倍率（1 = 不变；多条规则相乘）。
	///
	/// <para>调用点**只有一个**：<see cref="AttackInstance.RollHit"/> —— 它在
	/// <see cref="HitInstance"/> 生成（= <c>Hit</c> 事件抛出、状态施加）**之前**跑，
	/// 所以读到的层数就是"这一击之前已有"的层数。没有任何规则登记时恒为 1（改动前逐位不变）。</para>
	/// </summary>
	public float OutgoingDamageScaleAgainst(Unit target, AttackInstance attack)
	{
		if (_outgoingDamageScales.Count == 0)
		{
			return 1f;
		}

		float scale = 1f;
		foreach (IOutgoingDamageScale rule in _outgoingDamageScales)
		{
			scale *= Mathf.Max(0f, rule.ScaleAgainst(target, attack));
		}

		return scale;
	}

	public float Heal(float amount)
	{
		if (IsDead || amount <= 0f)
		{
			return 0f;
		}

		// ⭐ J11：治疗量倍率（符文「死铠」= 无法恢复生命值 → 0 = 完全禁疗）。
		// 基础值 1 在 `SeedStats` 里无条件就位，`amount × 1.0f` 与改动前逐位相等。
		float scale = Stats.Get("HealMultiplier");
		if (scale <= 0f)
		{
			return 0f;
		}

		amount *= scale;
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
			IsDamageOverTime = hit.Attack?.IsDamageOverTime == true,
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
				IsDamageOverTime = hit.Attack?.IsDamageOverTime == true,
				ComboStep = hit.Attack?.ComboStep ?? -1,
				ComboStepCount = hit.Attack?.ComboStepCount ?? 0,
				Source = hit.Attacker,
				Target = this,
				Tag = hit.Attack?.Ability ?? string.Empty,
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

