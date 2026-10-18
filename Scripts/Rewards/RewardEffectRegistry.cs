using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
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
		["StatSet"] = row => context => ApplyModifiers(row, context),
		["ChargeBehavior"] = row => context =>
		{
			if (row.ChargeBehavior is not { TargetAbility: { } target } tuning
				|| Ability.FindByAbilityId(context.Player, target.Id) is not { } ability)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少有效蓄力进化参数或目标技能。");
				return;
			}
			ability.ConfigureChargeBehavior(tuning);
		},
		["TeleportJump"] = row => context => context.Player.Movement?.ConfigureTeleportJump(true),
		["WeaponRecall"] = row => context =>
		{
			if (row.WeaponRecall is not { TargetAbility: { } target } tuning
				|| Ability.FindByAbilityId(context.Player, target.Id) is not SpearThrowAbility spear)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少有效长枪召回参数或目标技能。");
				return;
			}
			float scale = Mathf.Max(0.01f, tuning.ChargeTimeScale);
			// 始终从原技能表计算，避免重复应用把时间再次减半。
			spear.ChargeBaseSeconds = target.ChargeBaseSeconds * scale;
			spear.ChargeStepSeconds = target.ChargeStepSeconds * scale;
			spear.ChargeMaxSeconds = target.ChargeMaxSeconds * scale;
			spear.RecallOnSecondPress = tuning.RecallOnSecondPress;
			spear.AutoRecallOnSettle = tuning.AutoRecallOnSettle;
		},
		["WeaponSeek"] = row => context =>
		{
			if (row.WeaponSeek is not { TargetAbility: { } } tuning
				|| Ability.FindByAbilityId(context.Player, tuning.TargetAbility.Id) is not ScytheThrowAbility scythe)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少有效飞镰索敌参数或目标技能。");
				return;
			}
			scythe.ConfigureSeeking(tuning.IntervalSeconds, tuning.TargetHeight);
		},
		["MeleeProjectile"] = row => context =>
		{
			if (row.MeleeProjectile is not { TargetAbility: { }, ProjectileScene: { } } tuning
				|| Ability.FindByAbilityId(context.Player, tuning.TargetAbility.Id) is not MeleeComboAbility melee)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少有效近战额外投射物参数。");
				return;
			}
			melee.ConfigureExtraProjectile(tuning);
		},
		["SkillEnhancement"] = row => context =>
		{
			if (row.SkillEnhancement is not { TargetAbility: { } } tuning)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少目标技能增强参数。");
				return;
			}
			Ability? ability = Ability.FindByAbilityId(context.Player, tuning.TargetAbility.Id);
			if (ability is SwordQiAbility draw) draw.ConfigureEnhancement(tuning.DamageScale, tuning.SizeScale, tuning.ProjectileSweepMaxHits, tuning.ProjectileSweepRadius);
			else if (ability is LandingBurstAbility burst) burst.ConfigureEnhancement(tuning.DamageScale, tuning.SizeScale);
			else if (ability is MeleeComboAbility combo) combo.ConfigureSingleStep(tuning.ComboStepIndex, tuning.DamageScale, tuning.SizeScale, tuning.ForwardReachScale);
			else if (ability is FallAttackAbility fall && tuning.LandingKnockback >= 0f) fall.ConfigureLandingKnockback(tuning.LandingKnockback);
			else { GD.PushWarning($"[Reward] {row.Id} 的增强目标技能不存在或类型不支持。"); return; }
			if (tuning.CooldownSeconds >= 0f) ability.Cooldown = tuning.CooldownSeconds;
		},
		["VolleyRamp"] = row => context =>
		{
			if (row.VolleyRamp is not { TargetAbility: { } target } tuning
				|| Ability.FindByAbilityId(context.Player, target.Id) is not ArrowVolleyAbility volley)
			{
				GD.PushWarning($"[Reward] {row.Id}缺少有效的连射参数或目标技能。");
				return;
			}
			volley.ConfigureRamp(tuning.MaxCount, tuning.WindowSeconds, tuning.ShotIntervalSeconds);
		},

		// ⭐ J9：**蓄力阈值弹**（「龙之箭」）—— 松手那一刻蓄力 ≥ 阈值的那一发更大 + 多穿透 N 个目标。
		//   参数全部来自行上的强类型 `ChargedShot` 资源（目标技能 / 蓄力上限 / 档数上限 / 阈值 / 尺寸 / 穿透数），
		//   通过 `ArrowVolleyAbility.ConfigureChargedShot` 这个**公开强类型方法**接进去 ——
		//   没有 `Godot.Set("属性名", 值)` 那种字符串反射（architecture.md 第 12 条）。
		["ChargedShot"] = row => context =>
		{
			if (row.ChargedShot is not { TargetAbility: { } target } tuning
				|| Ability.FindByAbilityId(context.Player, target.Id) is not ArrowVolleyAbility volley)
			{
				GD.PushWarning($"[Reward] {row.Id} 缺少有效的蓄力阈值弹参数（ChargedShot）或目标技能，已跳过。");
				return;
			}

			volley.ConfigureChargedShot(
				tuning.MaxChargeSeconds,
				tuning.MaxSteps,
				tuning.ThresholdSeconds,
				tuning.SizeScale,
				tuning.PierceCount);
			GameLog.Write(
				"Reward",
				$"增幅·蓄力阈值弹：{row.Id} → 技能行 {target.Id} 蓄力上限 {volley.ChargeMaxSeconds:0.##}s（档数上限 {(volley.ChargeMaxSteps > 0 ? volley.ChargeMaxSteps + " 档" : "不限")}）"
				+ $"／松手 ≥ {volley.ChargedShotThresholdSeconds:0.##}s 的大箭：尺寸 ×{volley.ChargedShotSizeScale:0.##} 额外穿透 {volley.ChargedShotPierce} 个");
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
		// P5 第二批（只加一列）：`Cooldown` —— 文档「冰晶」的"每 10 秒最多冰冻一次"就是它。
		// 不填 → 0 = 改动前那条路（一次都不拦），逐位不变。
		["BurnOnHit"] = row => context =>
			context.Player.AddChild(new BurnOnHitRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
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

		// ⭐ J8：**「标记」类增幅**（「发散」/「心眼」）。与上面的 `BurnOnHit` 的区别是两件事都收在数据里：
		//   ① 挂标记的来源 = 行上的 `RequiredEventAbility`（强类型资源引用 → 技能行 Id）——
		//      旧实现复用 `BurnOnHit` 只有"来源是自己"，于是后撤/其它武技的命中也会挂标记（来源过滤不完整）；
		//   ② 加伤只对哪个技能生效 = 行上的 `BonusAgainstAbility`（空 = 玩家的各种伤害），
		//      按**命中前**目标身上的层数结算（`AttackInstance.RollHit` 那一刻问，见 `IOutgoingDamageScale`）。
		["MarkedBonus"] = row => context =>
			context.Player.AddChild(new MarkedBonusRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
				StatusId = ParamText(row, "StatusId", "Mark"),
				Duration = Param(row, "Duration", 6f),
				MarkAbilityId = row.RequiredEventAbility?.Id ?? string.Empty,
				BonusAbilityId = row.BonusAgainstAbility?.Id ?? string.Empty,
				FlatBonus = Param(row, "FlatBonus", 0f),
				PerStackBonus = Param(row, "PerStackBonus", 0f),
			}),

		// ⚠️ P6b：这里原来还有一条 `["AmmoOnKill"]`（击杀补满弹匣 → `AmmoOnKillRule`）——
		// 随"没有弹匣的概念了"整条删除：行文件（`mut_supply`）P6a 已删、`Weapon.RefillMagazine` 本批已删。
		// `ChainLightningRule` / `LifestealRule` / `BurnOnHitRule` / `SummonOnKillRule` 四条**保留**
		// （效果与弹匣无关，是可复用基础设施）。

		// 强化「护甲」：护甲没有上限（用户 C1），所以**加甲**走 `Unit.GrantArmor` 这一条（不是 StatSheet 抬高上限）。
		// 数值来自表（`Armor`），组件零个 —— 这是一条**纯表驱动**的处理器。
		["GrantArmor"] = row => context => context.Player.GrantArmor(Param(row, "Armor", 0f)),

		// ⭐ P5 第二批（只加）：**表驱动的"改某一条技能行"** ——
		// HandlerParams 里除 AbilityId 之外的每一条都是「Ability 上的一个 [Export] 属性名 = 值」，
		// 用 Godot 自己的 `GodotObject.Set()` 套用（不写反射、不为每个字段写一条特例）。
		//
		// 服务这些行（数值全在表里，代码里一个数字都没有）：
		//   有备无患（后撤 MaxCharges=3 / RechargeSeconds=0.5）。专注已迁ChargeBehavior明确完美标志，
		//   地爆已迁SkillEnhancement，气贯长虹已迁强类型WeaponRecall。
		//   龙之箭已迁ChargedShot；大拔刀术／神降术已迁强类型SkillEnhancement。
		//
		// ⚠️ 属性名写错**不静默**：先按 `GetPropertyList()` 查存在性，不存在就打警告并跳过这一条。
		["AbilityOverride"] = row => context =>
		{
			string abilityId = ParamText(row, "AbilityId", string.Empty);
			if (string.IsNullOrEmpty(abilityId))
			{
				GD.PushWarning($"[Config] 增幅项 {row.Id} 的 AbilityOverride 没有填 AbilityId，已跳过（这一条不会生效）。");
				return;
			}

			if (Ability.FindByAbilityId(context.Player, abilityId) is not { } ability)
			{
				GD.PushWarning($"[Config] 增幅项 {row.Id} 要改的技能行 {abilityId} 不在这位玩家身上，已跳过。");
				return;
			}

			// 兼容旧行上的全局修饰器；技能专属增强另走强类型处理器，避免污染其他技能。
			ApplyModifiers(row, context);

			var applied = new List<string>();
			foreach (ParamEntry entry in row.HandlerParams)
			{
				if (entry.Key == "AbilityId")
				{
					continue;
				}

				if (!HasProperty(ability, entry.Key))
				{
					GD.PushWarning($"[Config] 增幅项 {row.Id} 的参数 \"{entry.Key}\" 在 {abilityId} 上不存在，已跳过这一条参数。");
					continue;
				}

				ability.Set(entry.Key, entry.Value);
				applied.Add($"{entry.Key}={entry.Value}");
			}

			GameLog.Write("Reward", $"增幅·技能覆盖：{row.Id} → 技能行 {abilityId} 覆盖了 [{string.Join(" ", applied)}]");
		},

		// ⭐ P5 第二批（只加）：**表驱动的"战斗事件 → 效果"**（一个类服务 6 条行，见 TablePulseRule 的注释）。
		["TablePulse"] = row => context =>
			context.Player.AddChild(new TablePulseRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
				Trigger = ParamText(row, "Trigger", "Kill"),
				AbilityId = ParamText(row, "AbilityId", string.Empty),
				RequiredAbilityId = row.RequiredEventAbility?.Id ?? string.Empty,
				RequireFinalComboStep = row.RequireFinalComboStep,
				RequireHover = ParamBool(row, "RequireHover", false),
				CooldownFraction = Param(row, "CooldownFraction", 0f),
				CooldownSeconds = Param(row, "CooldownSeconds", 0f),
				RefreshAbility = ParamBool(row, "RefreshAbility", false),
				Heal = Param(row, "Heal", 0f),
				HoverSeconds = Param(row, "HoverSeconds", 0f),
				ExtraAirJumps = (int)Param(row, "ExtraAirJumps", 0f),
				JumpCap = (int)Param(row, "JumpCap", 3f),
				Radius = Param(row, "Radius", 0f),
				Damage = Param(row, "Damage", 0f),
			}),

		// 杀戮践踏：成功起跳的事实，不用落地轮询或隐藏冷却替代。
		["JumpPulse"] = row => context =>
			context.Player.AddChild(new JumpPulseRule
			{
				RewardId = row.Id,
				Radius = Param(row, "Radius", 0f),
				Damage = Param(row, "Damage", 0f),
			}),

		// ⭐ P6b：**暴击 → 巡飞弹**（「巡飞弹」）。复用 P3 的追踪弹通路：给最近的敌人挂一个
		// **专用信标状态**，再发一枚 `HomingStatusId` = 那个状态的飞弹。
		["HomingMissileOnCrit"] = row => context =>
			context.Player.AddChild(new HomingMissileOnCritRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
				MissileScene = LoadScene(ParamText(row, "ProjectileScene", "res://Scenes/Combat/Bullet.tscn")),
				Damage = Param(row, "Damage", 50f),
				Speed = Param(row, "Speed", 60f),
				SeekRange = Param(row, "SeekRange", 18f),
				Lifetime = Param(row, "Lifetime", 4f),
				BeaconStatusId = ParamText(row, "BeaconStatusId", "MissileBeacon"),
				BeaconSeconds = Param(row, "BeaconSeconds", 1.5f),
				TurnRateDegrees = Param(row, "TurnRateDegrees", 360f),
			}),

		// ⭐ P6d：**奔跑 → 获得一次格挡**（「护卫」）。触发靠轮询 P6d 新加的 `SprintController.IsSprinting`
		// （"奔跑"是文档第 19 行那条全角色通用机制），格挡走**既有**的 `BlockComponent`；
		// 数值（间隔 / 挡后剩伤 / 正面阈值）全来自行。
		["GuardOnSprint"] = row => context =>
			context.Player.AddChild(new GuardOnSprintRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 5f),
				BlockDamageMultiplier = Param(row, "BlockDamageMultiplier", 0f),
				FrontDotThreshold = Param(row, "FrontDotThreshold", 1f),
			}),

		// ⭐ P6d：**武技1 命中 → 给敌人挂"落日剑"**（P6d 新增的那条异化）。
		// "哪个技能算武技1"由 `TriggerInputAction`（按键动作名）反查技能行的 InputAction 列决定 ——
		// 换武器 / 加武器都不用改代码；数值（悬挂 / 落下 / 半径 / 伤害 / 高度）全来自行。
		["SunsetSword"] = row => context =>
			context.Player.AddChild(new SunsetSwordRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
				TriggerInputAction = ParamText(row, "TriggerInputAction", "weapon_skill_1"),
				HoverSeconds = Param(row, "HoverSeconds", 1.5f),
				FallSeconds = Param(row, "FallSeconds", 0.35f),
				Radius = Param(row, "Radius", 2.5f),
				Damage = Param(row, "Damage", 40f),
				HoverHeight = Param(row, "HoverHeight", 3.2f),
			}),

		// ⭐ P6d：**击杀 → 有概率掉一个"灵魂"**（「灵魂收割」）。灵魂自己每帧轮询拾取半径
		// （不建 Area3D、不进物理世界），拾取即 `Unit.Heal`。数值（概率 / 回血 / 半径 / 存活）全来自行。
		["SoulHarvestOnKill"] = row => context =>
			context.Player.AddChild(new SoulHarvestRule
			{
				RewardId = row.Id,
				Cooldown = Param(row, "Cooldown", 0f),
				Chance = Param(row, "Chance", 0.3f),
				Heal = Param(row, "Heal", 8f),
				PickupRadius = Param(row, "PickupRadius", 1.2f),
				LifetimeSeconds = Param(row, "LifetimeSeconds", 15f),
				PickupDelaySeconds = Param(row, "PickupDelaySeconds", 0f),
			}),

		// ⭐ J11：**符文**（开局携带一件的信物）。服务 5 枚"需要一个行为"的符文行，
		// 具体做哪几件事完全由行上的 HandlerParams 决定 —— 见 ApplyRuneEffect 的注释。
		["RuneEffect"] = row => context => ApplyRuneEffect(row, context),
	};

	/// <summary>
	/// ⭐ **J11：符文**（开局携带一件的信物）—— 一个处理器服务**全部**"需要一个行为"的符文行，
	/// 与 <c>TablePulse</c> 一个类服务 6 条行是同一条纪律：**差别全在数据里**。
	///
	/// <list type="number">
	/// <item><b>Modifiers</b>（先套）：符文的纯数值部分（`MaxHealthCap` / `IncomingDamageMultiplier` /
	///   `CooldownMultiplier` / `MoveSpeedMultiplier` / `DamageMultiplier` / `JumpVelocityMultiplier` /
	///   `MaxFallSpeedMultiplier` / `HealMultiplier`）—— 与 `StatSet` **同一段代码**，不是第二份实现；</item>
	/// <item><c>Armor</c>：立刻 <c>Unit.GrantArmor</c>（「死铠」的初始护甲）；</item>
	/// <item><c>ArmorPerKills</c> + <c>ArmorPerTrigger</c>：<see cref="RuneArmorOnKillRule"/>（每 N 杀 +甲）；</item>
	/// <item><c>DeathAvoid</c> + <c>DeathAvoidHealFraction</c>：<see cref="RuneDeathAvoidRule"/>（「生死」）；</item>
	/// <item><c>NearRange</c> / <c>NearScale</c> / <c>FarRange</c> / <c>FarScale</c>：
	///   <see cref="RuneDistanceDamageRule"/>（「狙神」）；</item>
	/// <item><c>MaxHealthDamageRatio</c>：<see cref="RuneMaxHealthDamageRule"/>（「巨人」）；</item>
	/// <item><c>PixieIntervalSeconds</c> / <c>PixieRadius</c> / <c>PixieDamage</c> / …：
	///   <see cref="PixieCompanion"/>（「小精灵」）。</item>
	/// </list>
	///
	/// <para>⚠️ **缺哪个参数就不建哪个**（`Param(row, 键, 0)` 的兜底是 0 / 空），所以一行只写它自己要的键；
	/// 也给不出"表里写了但代码没读"的静默状态 —— 每个键都在上面这张清单里有名字。</para>
	///
	/// <para>⚠️ 建出来的节点都挂了 <c>Rune_</c> 前缀 + 符文 Id，由 `RunController` 在换符文 /
	/// 重新开局时按名字摘掉（`FreeRuneNodes`），不会重复叠加。</para>
	/// </summary>
	private static void ApplyRuneEffect(RewardConfig row, RewardContext context)
	{
		// ① 纯数值部分先套（与 `StatSet` 共用同一份实现，所以 `Source` 语义也一致）。
		ApplyModifiers(row, context);

		Unit player = context.Player;
		var applied = new List<string>();

		// ② 立刻加甲（「死铠」的初始护甲）。
		float armor = Param(row, "Armor", 0f);
		if (armor > 0f)
		{
			player.GrantArmor(armor);
			applied.Add($"护甲 +{armor:0.##}");
		}

		// ③ 每 N 杀加甲。
		float armorPerKills = Param(row, "ArmorPerKills", 0f);
		if (armorPerKills > 0f)
		{
			var rule = new RuneArmorOnKillRule
			{
				Name = $"Rune_{row.Id}_ArmorOnKill",
				RewardId = row.Id,
				KillsPerTrigger = (int)armorPerKills,
				ArmorPerTrigger = Param(row, "ArmorPerTrigger", 1f),
			};
			player.AddChild(rule);
			applied.Add($"每 {rule.KillsPerTrigger} 杀 +{rule.ArmorPerTrigger:0.##} 甲");
		}

		// ④ 死亡避免。
		float deathAvoid = Param(row, "DeathAvoid", 0f);
		if (deathAvoid > 0f)
		{
			var rule = new RuneDeathAvoidRule
			{
				Name = $"Rune_{row.Id}_DeathAvoid",
				Charges = (int)deathAvoid,
				HealFraction = Param(row, "DeathAvoidHealFraction", 0.3f),
			};
			player.AddChild(rule);
			applied.Add($"{rule.Charges} 次死亡避免（恢复 {rule.HealFraction:0.##} 生命）");
		}

		// ⑤ 按距离加伤（「狙神」）。
		float farRange = Param(row, "FarRange", 0f);
		if (farRange > 0f)
		{
			var rule = new RuneDistanceDamageRule
			{
				Name = $"Rune_{row.Id}_DistanceDamage",
				NearRange = Param(row, "NearRange", 0f),
				NearScale = Param(row, "NearScale", 1f),
				FarRange = farRange,
				FarScale = Param(row, "FarScale", 1f),
			};
			player.AddChild(rule);
			applied.Add($"{rule.NearRange:0.##}m ×{rule.NearScale:0.##} → {rule.FarRange:0.##}m ×{rule.FarScale:0.##}（按距离）");
		}

		// ⑥ 按最大生命值加伤（「巨人」）。
		float healthRatio = Param(row, "MaxHealthDamageRatio", 0f);
		if (healthRatio > 0f)
		{
			var rule = new RuneMaxHealthDamageRule
			{
				Name = $"Rune_{row.Id}_MaxHealthDamage",
				Ratio = healthRatio,
			};
			player.AddChild(rule);
			applied.Add($"每次攻击额外 {healthRatio:0.###}×最大生命值");
		}

		// ⑦ 轨道小精灵（「小精灵」）。
		float pixieInterval = Param(row, "PixieIntervalSeconds", 0f);
		if (pixieInterval > 0f)
		{
			var pixie = new PixieCompanion
			{
				Name = $"Rune_{row.Id}_Pixie",
				IntervalSeconds = pixieInterval,
				SearchRadius = Param(row, "PixieRadius", 8f),
				Damage = Param(row, "PixieDamage", 20f),
				OrbitRadius = Param(row, "PixieOrbitRadius", 1.6f),
				OrbitDegreesPerSecond = Param(row, "PixieOrbitSpeed", 90f),
				AbilityId = row.Id,
			};
			player.AddChild(pixie);
			applied.Add($"每 {pixie.IntervalSeconds:0.##}s 打 {pixie.SearchRadius:0.##}m 内一个敌人（{pixie.Damage:0.##}）");
		}

		GameLog.Write(
			"Rune",
			$"符文·生效：{row.Id}（{row.DisplayName}）→ {player.Name}｜{(applied.Count == 0 ? "只有 StatSheet 修正（见上一行）" : string.Join(" / ", applied))}");
	}

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

	/// <summary>
	/// 「把行上的 Modifiers 套到玩家身上」那一段 —— `StatSet` 与 `AbilityOverride` **共用同一份**（只加不删：
	/// 原来内联在 `StatSet` 里的代码原样搬过来，语义逐位不变；`Source` 仍然是那个 `RunBuild`，
	/// 所以自测里 `RemoveBySource` 还能按组还原）。
	/// </summary>
	private static void ApplyModifiers(RewardConfig row, RewardContext context)
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

	/// <summary>读一个布尔参数（`RequireHover` / `RefreshAbility` 这种开关）。</summary>
	private static bool ParamBool(RewardConfig row, string key, bool fallback)
	{
		foreach (ParamEntry entry in row.HandlerParams)
		{
			if (entry.Key == key)
			{
				return entry.AsBool(fallback);
			}
		}

		return fallback;
	}

	/// <summary>
	/// 目标对象上**有没有这个 [Export] 属性**（`AbilityOverride` 用）。
	///
	/// <para>为什么要问这一句：Godot 的 `Set("写错的属性名", …)` 是**静默无效**的 ——
	/// 那会让"表里写了参数、游戏里什么也没发生"变成一个查不出来的哑故障（`architecture.md` 第 8 条
	/// 同一条纪律：宁可大声留痕）。`GetPropertyList()` 是 Godot 自己的属性清单，不引入反射。</para>
	/// </summary>
	private static bool HasProperty(GodotObject target, string propertyName)
	{
		foreach (Godot.Collections.Dictionary property in target.GetPropertyList())
		{
			if (property.TryGetValue("name", out Variant name) && name.AsString() == propertyName)
			{
				return true;
			}
		}

		return false;
	}

	private static PackedScene? LoadScene(string path)
		=> string.IsNullOrEmpty(path) ? null : RuntimeResources.Load<PackedScene>(path);
}
