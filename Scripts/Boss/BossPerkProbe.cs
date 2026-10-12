#if DEBUG
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>
/// 自测探针：Pressure 词条池的构成 + **技能参数词条**真的写进了技能节点 + **限定词的刷新判定**。
///
/// 为什么单独放一个文件：这些是"取证"代码，跟着 SelfTest 走（`#if DEBUG`），但不该把 SelfTest 撑得更长，
/// 也不该占 `Scripts/Config/`（那是配置行类的地盘）。调用点只有一处：<c>SelfTest.ReportTables</c>。
///
/// ⚠️ 探针走的是**与正式路径同一个入口**：`BossPerk.Apply(context)`（`StageController.ApplyPendingPressurePerks`
/// 与 `EncounterController.SpawnBossNow` 调的也是它），上下文按生成路径的顺序准备
/// （实例化 → `UnitConfig.ApplyTo` → 建 `BossPerkContext`），抽签走 `BossPerkCatalog.RollPressure`。
/// </summary>
public static class BossPerkProbe
{
	/// <summary>造一个"首领出生时"的上下文（与 `EncounterController.SpawnBossNow` 同序同形）。</summary>
	private static BossPerkContext ContextOf(Unit boss, Node source)
		=> new()
		{
			Boss = boss,
			Source = source,
			Pressure = boss.GetNodeOrNull<BossPressure>("Pressure"),
		};

	private static BossPerk? Find(string id)
	{
		foreach (BossPerk perk in BossPerkCatalog.PressureOnly)
		{
			if (perk.Id == id)
			{
				return perk;
			}
		}

		return null;
	}

	/// <summary>把一次限定词判定写成一行文本（**成立也要看得见** —— 不能把"通过"写成"词条缺失"）。</summary>
	private static string Judge(PerkQualifier? qualifier, BossPerkContext context)
	{
		if (qualifier == null)
		{
			return "⚠ 词条不在池里";
		}

		return qualifier.FailedReason(context) is { } reason ? $"不成立（{reason}）" : "成立";
	}

	/// <summary>
	/// 把"数值型词条会碰到的那些量"抄一份（换抽前后对比用）—— 既含**单位属性**，
	/// 也含**技能参数**（技能参数词条改的是能力节点/命中体，不看这两个地方会误报"什么都没变"）。
	/// </summary>
	private static List<string> Snapshot(Unit unit)
	{
		var snapshot = new List<string>
		{
			$"生命 {unit.MaxHealth:0.#}",
			$"护甲 {unit.Armor:0.#}",
			$"伤害×{unit.Stats.Get("DamageMultiplier"):0.###}",
			$"攻速×{unit.Stats.Get("AttackSpeedMultiplier"):0.###}",
			$"移速×{unit.Stats.Get("MoveSpeedMultiplier"):0.###}",
		};

		snapshot.Add($"尖啸冷却 {AbilityValue(unit, "AbilityScream", "Cooldown"):0.##}");
		snapshot.Add($"冲锋伤害 {HitboxValue(unit, "AbilityCharge", "Damage"):0.##}");
		snapshot.Add($"召唤冷却 {AbilityValue(unit, "AbilitySummon", "Cooldown"):0.##}");
		return snapshot;
	}

	private static float AbilityValue(Unit unit, string node, string property)
	{
		Ability? ability = unit.GetNodeOrNull<Ability>(node);
		return ability == null || ability.Get(property).VariantType == Variant.Type.Nil ? 0f : ability.Get(property).AsSingle();
	}

	private static float HitboxValue(Unit unit, string node, string property)
	{
		Ability? ability = unit.GetNodeOrNull<Ability>(node);
		if (ability == null || NodeSearch.FindInSubtree<AttackHitbox>(ability) is not { } hitbox)
		{
			return 0f;
		}

		return hitbox.Get(property).VariantType == Variant.Type.Nil ? 0f : hitbox.Get(property).AsSingle();
	}

	/// <summary>对比两份快照，只列出**真的变了**的项（"换抽真的落地了"的直接证据）。</summary>
	private static string Diff(List<string> before, Unit unit)
	{
		List<string> after = Snapshot(unit);
		var changed = new List<string>();
		for (int i = 0; i < before.Count && i < after.Count; i++)
		{
			if (before[i] != after[i])
			{
				changed.Add($"{before[i]} → {after[i]}");
			}
		}

		return changed.Count == 0
			? "什么都没变（⚠ 换抽没落地）"
			: string.Join(" · ", changed);
	}

	/// <summary>
	/// 跑一遍全部取证。`stage` 只当 `BossPerkContext.Source`（与 `StageController.ApplyPendingPressurePerks` 一致）。
	/// </summary>
	public static void Report(Node? stage)
	{
		// ① 池的构成：条数 / 权重 / 概率 / 限定词（确定性，可逐字比对）。
		int withQualifier = 0;
		var perPerk = new List<string>();
		foreach (BossPerk perk in BossPerkCatalog.PressureOnly)
		{
			if (perk.Qualifier.HasAny)
			{
				withQualifier++;
			}

			string qualifier = perk.Qualifier.HasAny ? $" 限定 [{perk.Qualifier.Describe()}]" : string.Empty;
			perPerk.Add($"{perk.Id}「{perk.Name}」权重 {perk.Weight:0.##}{qualifier}");
		}

		GameLog.Write(
			"SelfTest",
			$"Pressure 词条池（数值型 = 界面上的**词条**）：{BossPerkCatalog.PressureOnly.Count} 条（期望 7 = 4 数值型参数栏 + 3 技能参数栏）｜ 带限定词的 {withQualifier} 条（期望 3 = 3 技能参数）｜ 明细：[{string.Join(" · ", perPerk)}]");
		GameLog.Write("SelfTest", $"Pressure 词条池·出率（权重真进分母）：{BossPerkCatalog.DescribePressurePool(null)}");

		if (GD.Load<PackedScene>("res://Scenes/Enemies/Boss.tscn") is not { } bossScene)
		{
			GameLog.Write("SelfTest", "Pressure 词条探针：读不到 Boss.tscn，跳过。");
			return;
		}

		var boss = bossScene.Instantiate<Unit>();
		BossConfig? bossRow = ConfigRows.ByScenePath<BossConfig>("res://Scenes/Enemies/Boss.tscn");

		// 与生成路径同序：先套单位行（生命/技能…），再套词条。
		bossRow?.ApplyTo(boss);
		BossPerkContext context = ContextOf(boss, stage ?? boss);

		// ② 技能参数词条：真的写进了技能节点，而且是**叠乘**（不是绝对赋值 → 抽两次继续变强）。
		if (Find("pmod_scream_echo") is { } echo && boss.GetNodeOrNull<Ability>("AbilityScream") is { } scream)
		{
			float before = scream.Cooldown;
			echo.Apply(context);
			float once = scream.Cooldown;
			echo.Apply(context);
			float twice = scream.Cooldown;
			GameLog.Write(
				"SelfTest",
				$"技能参数词条实测（pmod_scream_echo → AbilityScream.Cooldown）：{before:0.##}（技能行 skl_boss_scream）→ 抽 1 次 {once:0.##} → 抽 2 次 {twice:0.##}（期望 7 → 5.6 → 4.48 = ×0.8 叠乘，不是覆盖）");
		}
		else
		{
			GameLog.Write("SelfTest", "技能参数词条实测：读不到 pmod_scream_echo 或 Boss 上没有 AbilityScream，跳过。");
		}

		if (Find("pmod_charge_impact") is { } impact
			&& boss.GetNodeOrNull<Ability>("AbilityCharge") is { } charge
			&& NodeSearch.FindInSubtree<AttackHitbox>(charge) is { } chargeHit)
		{
			float before = chargeHit.Damage;
			impact.Apply(context);
			GameLog.Write(
				"SelfTest",
				$"技能参数词条实测（pmod_charge_impact → AbilityCharge 命中体.Damage）：{before:0.##} → {chargeHit.Damage:0.##}（期望 20 → 25 = ×1.25）");
		}

		// ③ 限定词的**刷新判定**：同一条词条、同一个进程里判两次，条件不同 → 结果不同（证明不缓存）。
		GameLog.Write(
			"SelfTest",
			$"限定词刷新判定·基准：当前 Boss 身份「{PerkQualifier.BossIdentityOf(boss)}」（期望 boss_final = 单位行 Id）· 当前关卡「{PerkQualifier.CurrentStageText()}」");

		if (GD.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn") is { } chaserScene)
		{
			var chaser = chaserScene.Instantiate<Unit>();

			// 与生成路径同序（`ApplyTo` → `AddChild`；这里不进树，所以只做前一步）——
			// 不做的话 `_baseMaxHealth` 还是 0，`RefreshStats` 会把生命压成 1（探针自己造出来的假象）。
			ConfigRows.ByScenePath<EnemyConfig>("res://Scenes/Enemies/Chaser.tscn")?.ApplyTo(chaser);
			BossPerkContext other = ContextOf(chaser, stage ?? chaser);
			BossPerk? echoPerk = Find("pmod_scream_echo");

			// 同一个词条对象判两次：Boss 上成立（②里已经生效），换成 Chaser 不成立。
			GameLog.Write(
				"SelfTest",
				$"限定词刷新判定·同一条词条两次判定：Boss.tscn → {Judge(echoPerk?.Qualifier, context)} ｜ 换成 Chaser.tscn（身份「{PerkQualifier.BossIdentityOf(chaser)}」）→ {Judge(echoPerk?.Qualifier, other)}（期望：前者成立、后者不成立 → 说明**没有复用**上一次的判定结果）");

			// 换抽：用**抽签真正交给 StageController 的那一份**（带 RollTags 的副本）去 Apply ——
			// 限定词不成立时它应该换抽另一条，而不是硬套在没有这个技能的单位上。
			if (echoPerk != null)
			{
				BossPerk rolled = echoPerk.AsRolled(System.Array.Empty<string>());
				List<string> before = Snapshot(chaser);
				rolled.Apply(other);
				GameLog.Write(
					"SelfTest",
					$"限定词刷新判定·换抽实测（抽签副本 pmod_scream_echo，限定词不成立）：名字被改写为「{rolled.Name}」（期望形如 `尖啸回响→<另一条>`）｜ 该单位身上发生的变化：[{Diff(before, chaser)}]");
			}

			chaser.Free();
		}

		// ④ 关卡限定（`StageIds`）：池里**已经没有行再用它**了 —— 唯一带 StageIds 的那一条
		//    （`pmod_fortress_armor`「铁壁·护甲」）已按用户要求整行删除（"护甲目前就只给精英怪使用，
		//    首领不需要"，护甲现在只来自精英怪的 `EliteModifier`）。
		//    字段与判定逻辑仍然在（以后要给某一条加关卡限定不用重写判定），所以这里用一个**合成限定词**
		//    证明它照样有效：同一个限定词、同一帧，只换"当前关卡"这个条件 → 结论不同（也证明没有缓存）。
		var stageOnly = new PerkQualifier { StageIds = new[] { "stg_core" } };
		string inStage1 = Judge(stageOnly, context);

		// 自测专用开关：把"当前关卡"当成最后一关再判一次（同一个限定词、同一帧）。
		PerkQualifier.DebugStageIdOverride = "stg_core";
		string inStage3 = Judge(stageOnly, context);
		PerkQualifier.DebugStageIdOverride = null;

		GameLog.Write(
			"SelfTest",
			$"限定词刷新判定·关卡限定（合成限定词 StageIds=[stg_core]，池里已无行用它）：当前关卡 → {inStage1} ｜ 把当前关卡换成 stg_core 再判 → {inStage3}（期望：前者不成立、后者成立）");

		// ⑤ 端到端串一遍：真抽 3 次（与 StageController 一样走 RollPressure）→ 在"首领出生"时套用。
		// 这一步是"同一个进程里多次生成"的实测：每次出生都重新判限定词，抽到的名字会漂、判定不会省。
		var applied = new List<string>();
		for (int i = 0; i < 3; i++)
		{
			BossPerk? rolled = BossPerkCatalog.RollPressure(null);
			if (rolled == null)
			{
				applied.Add("（空池）");
				continue;
			}

			List<string> before = Snapshot(boss);
			rolled.Apply(context);
			applied.Add($"{rolled.Name}[{Diff(before, boss)}]");
		}

		GameLog.Write(
			"SelfTest",
			$"限定词刷新判定·端到端 3 次「抽签 → 出生套用」：{string.Join(" ｜ ", applied)} → {Vitals(boss)}（每次出生都重新判；带 `→` 的名字 = 那次因为限定词不成立换了抽）");

		boss.Free();
		GameLog.Write("SelfTest", "限定词刷新判定·还原：DebugStageIdOverride 已复位（正式代码从不设置它）");
	}

	private static string Vitals(Unit boss)
		=> $"Boss 生命 {boss.MaxHealth:0.#} 护甲 {boss.Armor:0.#} 伤害×{boss.Stats.Get("DamageMultiplier"):0.###} 攻速×{boss.Stats.Get("AttackSpeedMultiplier"):0.###} 移速×{boss.Stats.Get("MoveSpeedMultiplier"):0.###}";
}
#endif
