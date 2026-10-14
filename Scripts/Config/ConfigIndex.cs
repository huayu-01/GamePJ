using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 平衡配置单例（autoload）。所有系统通过 <see cref="Settings"/> 读数值。
/// 配置文件缺失或字段缺失时回退到 <see cref="ConfigIndexData"/> 的代码默认值。
/// </summary>
public partial class ConfigIndex : Node
{
	public const string ConfigPath = "res://Config/ConfigIndex.tres";

	public static ConfigIndex? Instance { get; private set; }

	private static readonly ConfigIndexData Fallback = new();

	/// <summary>"敌人倍率来源"只写一次日志（见 <see cref="LogEnemyScalesOnce"/>）。</summary>
	private static bool _enemyScaleLogged;

	public ConfigIndexData Config { get; private set; } = new();

	/// <summary>
	/// <see cref="ConfigPath"/> 是否真的加载成功。供日志区分「表已加载」与「整份配置回退到代码默认值」——
	/// 两者在玩法上差别很大（例如 Stages 为空、玩家生命 160 vs 250），必须能一眼看出来。
	/// </summary>
	public bool FileLoaded { get; private set; }

	public static ConfigIndexData Settings => Instance?.Config ?? Fallback;

	public override void _Ready()
	{
		Instance = this;
		if (ResourceLoader.Exists(ConfigPath) && GD.Load<ConfigIndexData>(ConfigPath) is { } loaded)
		{
			Config = loaded;
			FileLoaded = true;
		}
		else
		{
			GD.PushWarning($"[ConfigIndex] 未找到 {ConfigPath}，使用代码默认值。");
		}

		// ⚠️ 必须 defer：`GameLog` 的 autoload 排在 `ConfigIndex` **之后**（project.godot 的 [autoload] 顺序），
		// 在 `_Ready` 里直接写日志会被静默丢掉（首轮实测过：日志里一行 [Config] 都没有）。
		// defer 之后所有 autoload 都已就绪，日志能落地，而主场景还没加载，所以不影响任何读取者。
		CallDeferred(nameof(BuildTableIndex));
	}

	/// <summary>
	/// 把分表的索引数组建成按 Id / Slot（以及节点表的按 Kind）查的字典。
	///
	/// **每一步都留痕**：日志里会写明每张表读到几行、来源是"表"还是"代码默认值"，
	/// 这样出问题时不需要猜这一局用的是哪一套数值（上一轮踩过的坑：整份 ConfigIndex.tres
	/// 静默回退，只有日志能证明）。
	/// </summary>
	private void BuildTableIndex()
	{
		ConfigRows.Rebuild(Config.Weapons);
		ConfigRows.Rebuild(Config.Characters);
		ConfigRows.Rebuild(Config.Enemies);
		ConfigRows.Rebuild(Config.Bosses);
		ConfigRows.Rebuild(Config.Summons);
		ConfigRows.Rebuild(Config.AiProfiles);
		ConfigRows.Rebuild(Config.Skills);
		// 技能动画表（生成器的配方表）：运行期不"播"它，但索引起来才能被自测/生成器按 Id 取到。
		ConfigRows.Rebuild(Config.SkillAnimations);
		ConfigRows.Rebuild(Config.PressureMods);
		ConfigRows.Rebuild(Config.Effects);
		ConfigRows.Rebuild(Config.Nodes);
		ConfigRows.Rebuild(Config.ValueTags);
		ConfigRows.Rebuild(Config.Upgrades);
		ConfigRows.Rebuild(Config.UpgradePools);
		ConfigRows.Rebuild(Config.MetaUpgrades);
		// 能力卡（局外装备层，见 docs/ability-cards.md）：**与 Skills 是两层**，
		// 所以是独立的一张表 + 独立的一行日志条数，绝不混进技能表的计数里。
		ConfigRows.Rebuild(Config.AbilityCards);

		// 节点表额外建"节点类型 → 行 Id"的索引（RewardService / NodePlacement 按类型查）。
		foreach (NodeConfig row in Config.Nodes)
		{
			ConfigRows.SetKind(row.Kind, row.Id);
		}

		// 节点开几波由**价值标签**驱动（用户需求：高 4 / 中 3 / 低 1，且"不写死"）：
		// 这里把每张标签行的波数写进日志，同时报出"挂了标签但标签行读不到"的脏数据 ——
		// 后者会静默落到节点行的 WaveCount 兜底，不写日志就查不出来。
		ReportValueTags();

		// 增幅项要编译成运行期目录（表优先、代码内置兜底），编译结果与来源写在日志里。
		ActRoguelike.Rewards.RewardTable.Build(Config.Upgrades);

		// 武器 → 升级池 → 选项 的三层关联（配置表阶段 4）：读不到表 / 武器行没填 Pool 也在这里留痕。
		ReportUpgradePools();

		// 单位行 → AI 档案（配置表阶段 5）：读不到表 / 单位行没挂档案也在这里留痕。
		ReportAiProfiles();

		// 能力卡（局外装备层）：卡表行数 + 额度上限公式 + "授予技能"卡指向的技能行在不在索引里。
		ReportAbilityCards();

		// 首领强化：数值型（Pressure 表）与行为型（技能表里 ApplyHandler 非空的行）分别编译，
		// 两者必须能按来源分开（关卡核心文档 §7.1）。
		ActRoguelike.Boss.BossPerkCatalog.Build(Config.Skills, Config.PressureMods);

		// 关卡行里的敌人白名单按**敌人表的行 Id** 匹配：未知 Id 在这里一次性报出来，
		// 不让它变成"这一关静默少了几种敌人"（旧实现按中文显示名匹配，改文案就静默失效）。
		foreach (StageDefinition stage in Config.Stages)
		{
			if (stage == null)
			{
				continue;
			}

			foreach (string id in stage.AllowedEnemyIds)
			{
				if (ConfigRows.ById<EnemyConfig>(id) != null)
				{
					continue;
				}

				GD.PushWarning($"[ConfigIndex] 关卡 {stage.Id} 的 AllowedEnemyIds 里有未知敌人 Id「{id}」（敌人表里没有这一行）。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 关卡 {stage.Id} 允许列表里的「{id}」在敌人表里找不到，这一条不会生效。");
			}
		}

		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"配置表：武器 {ConfigRows.Count<WeaponConfig>()} / 角色 {ConfigRows.Count<CharacterConfig>()} / 敌人 {ConfigRows.Count<EnemyConfig>()} / 首领 {ConfigRows.Count<BossConfig>()} / 召唤物 {ConfigRows.Count<SummonConfig>()} / 技能 {ConfigRows.Count<AbilityConfig>()} / Pressure {ConfigRows.Count<PressureModConfig>()} / 效果 {ConfigRows.Count<EffectConfig>()} / 节点 {ConfigRows.Count<NodeConfig>()} / 局外成长 {ConfigRows.Count<MetaUpgradeConfig>()} / AI 档案 {ConfigRows.Count<AiProfileConfig>()} / 能力卡 {ConfigRows.Count<AbilityCardConfig>()}（ConfigIndex.tres {(FileLoaded ? "已加载" : "未加载（全部数值回退到代码默认值）")}）");
	}

	/// <summary>
	/// 把"价值标签 → 波数"写进日志（用户需求 #7 的"顺藤摸瓜"入口）。
	///
	/// 两条信息缺一不可：① 每张标签行的波数（高 4 / 中 3 / 低 1 是不是真的读到了）；
	/// ② 每个节点行实际算出几波 + 用了哪一层（标签表 / 节点行 WaveCount / 价值层兜底）。
	/// 不写②的话，"节点行挂了一个拼错的标签"就只会静默掉到下一层，改配置的人看不出问题。
	/// </summary>
	private void ReportValueTags()
	{
		var tagText = new System.Text.StringBuilder();
		foreach (ValueTagConfig tag in Config.ValueTags)
		{
			if (tag == null)
			{
				continue;
			}

			if (tagText.Length > 0)
			{
				tagText.Append('、');
			}

			tagText.Append($"{tag.Id}→{tag.WaveCount} 波");
		}

		int tagCount = ConfigRows.Count<ValueTagConfig>();
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"价值标签（节点波数来源）：{tagCount} 行（来源：{(tagCount > 0 ? "表 ValueTags" : "代码默认值（表读不到）")}）—— [{tagText}]");

		var nodeText = new System.Text.StringBuilder();
		foreach (NodeConfig row in Config.Nodes)
		{
			if (row == null)
			{
				continue;
			}

			string tags = row.ValueTags.Length == 0 ? "无标签" : string.Join("+", row.ValueTags);
			nodeText.Append(nodeText.Length > 0 ? " · " : string.Empty);
			nodeText.Append($"{row.Id}（{tags}）{ActRoguelike.Run.NodePlacement.WavesFor(row.Kind)} 波");

			foreach (string tag in row.ValueTags)
			{
				if (ConfigRows.ById<ValueTagConfig>(tag) != null)
				{
					continue;
				}

				GD.PushWarning($"[ConfigIndex] 节点行 {row.Id} 的价值标签「{tag}」在价值标签表里找不到，这一条不会生效（会落到节点行 WaveCount → 价值层兜底）。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 节点行 {row.Id} 的价值标签「{tag}」在 ValueTags 表里找不到。");
			}
		}

		ActRoguelike.Core.GameLog.Write("Config", $"节点波数（高 4 / 中 3 / 低 1）：{nodeText}");
	}

	/// <summary>
	/// 把"武器 → 升级池 → 选项"的关联写进日志（配置表阶段 4 的"顺藤摸瓜"入口，设计文档 §4.5.5）。
	///
	/// 三层回退：① 武器行精确指向的池命中 → ② 标签匹配（现有 4 条进化走这层）→ ③ 通用池
	/// （现有 10 条强化 + 5 条异化）。**回退路径在这里一眼能看出来** —— 池表读不到、或武器行没填 Pool 时，
	/// 条数与"武器指向"列表都会变成空/未填，但**玩法数值不变**（全部落到②③两层，与分表前逐位一致）。
	/// </summary>
	private void ReportUpgradePools()
	{
		var byWeapon = new System.Text.StringBuilder();
		foreach (WeaponConfig row in Config.Weapons)
		{
			if (row == null)
			{
				continue;
			}

			if (byWeapon.Length > 0)
			{
				byWeapon.Append('、');
			}

			byWeapon.Append($"{row.Id}→{(row.Pool == null ? "（未填）" : row.Pool.Id)}");

			// 行文件被引用 ≠ 被索引：池行只被武器引用、却没加进 ConfigIndex.tres 的 UpgradePools 数组时，
			// 池表看上去是空的，但引用仍在。这种不一致必须报出来（和关卡白名单那次同一个纪律）。
			if (row.Pool != null && ConfigRows.ById<UpgradePoolConfig>(row.Pool.Id) == null)
			{
				GD.PushWarning($"[ConfigIndex] 武器 {row.Id} 的 Pool 指向「{row.Pool.Id}」，但这个池不在 ConfigIndex.tres 的 UpgradePools 索引里。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 武器 {row.Id} 指向的升级池「{row.Pool.Id}」不在 UpgradePools 索引里（引用还在，索引里查不到）。");
			}
		}

		int assignedOptions = 0;
		foreach (RewardConfig row in Config.Upgrades)
		{
			if (row?.Pool != null)
			{
				assignedOptions++;
			}
		}

		int poolCount = ConfigRows.Count<UpgradePoolConfig>();
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"升级池：{poolCount} 个（来源：{(poolCount > 0 ? "表 UpgradePools" : "代码默认值（表读不到）")}）—— 武器指向 [{byWeapon}]；选项行里指定了池的 {assignedOptions} 条，其余走「标签匹配 → 通用」两层");
	}

	/// <summary>
	/// 把"单位行 → AI 档案"的关联写进日志（配置表阶段 5 的"顺藤摸瓜"入口）。
	///
	/// **回退路径在这里一眼能看出来**：AI 档案表读不到、或单位行没挂 <see cref="UnitConfig.AiProfile"/> 时，
	/// 档案数为 0、每个单位都显示"（未填）"，此时各单位保留**场景里**的 AI 参数，玩法数值不变。
	/// 与 <see cref="ReportUpgradePools"/> 同一条纪律：引用还在、索引里查不到也要报出来。
	/// </summary>
	private void ReportAiProfiles()
	{
		var links = new System.Text.StringBuilder();
		int unlinked = 0;
		CollectAiLinks(Config.Enemies, links, ref unlinked);
		CollectAiLinks(Config.Bosses, links, ref unlinked);
		CollectAiLinks(Config.Summons, links, ref unlinked);

		int profileCount = ConfigRows.Count<AiProfileConfig>();
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"AI 档案：{profileCount} 个（来源：{(profileCount > 0 ? "表 AiProfiles" : "代码默认值（表读不到）")}）—— 单位指向 [{links}]；有预制体但没挂档案的 {unlinked} 个");
	}

	/// <summary>
	/// 把能力卡表的规模与"额度上限怎么算"写进日志（`docs/ability-cards.md` 的"顺藤摸瓜"入口）。
	///
	/// 三件事必须能一眼看出来：① 读到几张卡（没读到 = 矩阵全是空位，不是崩溃）；
	/// ② 额度上限 = 基础 + 扩增行 × 每级增量（**"额度也能用局外资源升级"的落地形式**）；
	/// ③ "授予技能"卡指向的技能行在不在 <c>Skills</c> 索引里（引用还在、索引查不到也要报出来 ——
	/// 与升级池 / AI 档案同一条纪律）。
	/// </summary>
	private void ReportAbilityCards()
	{
		int cardCount = ConfigRows.Count<AbilityCardConfig>();
		var lines = new System.Text.StringBuilder();
		foreach (AbilityCardConfig row in Config.AbilityCards)
		{
			if (row == null)
			{
				continue;
			}

			if (lines.Length > 0)
			{
				lines.Append('、');
			}

			lines.Append($"{row.Id}（{row.QuotaCost} 点/解锁 {row.UnlockCost}/上限 Lv{row.MaxLevel}）");

			if (row.Effect != AbilityCardEffectKind.GrantSkill)
			{
				continue;
			}

			if (row.Skill == null)
			{
				GD.PushWarning($"[ConfigIndex] 能力卡 {row.Id} 是授予技能型，但没填 Skill（技能行）—— 这张卡激活后不会有任何效果。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 能力卡 {row.Id} 是授予技能型但没填 Skill 列。");
				continue;
			}

			if (ConfigRows.ById<AbilityConfig>(row.Skill.Id) == null)
			{
				GD.PushWarning($"[ConfigIndex] 能力卡 {row.Id} 的 Skill 指向「{row.Skill.Id}」，但这个技能不在 ConfigIndex.tres 的 Skills 索引里。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 能力卡 {row.Id} 指向的技能行「{row.Skill.Id}」不在 Skills 索引里（引用还在，索引里查不到）。");
			}
		}

		MetaUpgradeConfig? quota = ConfigRows.ById<MetaUpgradeConfig>(ActRoguelike.AbilityCards.AbilityStore.QuotaRowId);
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"能力卡（局外装备层，与 Skills 是两层）：{cardCount} 张（来源：{(cardCount > 0 ? "表 AbilityCards" : "代码默认值（表读不到）")}）—— [{lines}]；"
			+ $"额度上限 = 基础 {Config.AbilityQuotaBase} + 扩增行 {(quota == null ? "（读不到 meta_quota，每级 +1）" : $"{quota.Id} 每级 +{quota.PerLevel:0.##}（1 级价格 {quota.BaseCost}，每级 +{quota.CostGrowth}）")}");
	}

	/// <summary>
	/// 把一个单位表里的"行 → AI 档案"关系追加到日志串上。
	/// 只统计**真的会生成**的行：<c>ScenePath</c> 属性不存在（角色表）或留空（<c>enemy_global</c> 全局倍率行）就跳过。
	/// </summary>
	private static void CollectAiLinks<T>(T[] rows, System.Text.StringBuilder links, ref int unlinked) where T : UnitConfig
	{
		foreach (T row in rows)
		{
			if (row == null)
			{
				continue;
			}

			Variant scenePath = row.Get("ScenePath");
			if (scenePath.VariantType is not (Variant.Type.String or Variant.Type.StringName) || string.IsNullOrEmpty(scenePath.AsString()))
			{
				continue;
			}

			if (row.AiProfile == null)
			{
				unlinked++;
				continue;
			}

			if (links.Length > 0)
			{
				links.Append('、');
			}

			links.Append($"{row.Id}→{row.AiProfile.Id}");

			// 行文件被引用 ≠ 被索引（与升级池那次同一个坑）。
			if (ConfigRows.ById<AiProfileConfig>(row.AiProfile.Id) == null)
			{
				GD.PushWarning($"[ConfigIndex] 单位行 {row.Id} 的 AiProfile 指向「{row.AiProfile.Id}」，但这个档案不在 ConfigIndex.tres 的 AiProfiles 索引里。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 单位行 {row.Id} 指向的 AI 档案「{row.AiProfile.Id}」不在 AiProfiles 索引里（引用还在，索引里查不到）。");
			}
		}
	}

	/// <summary>
	/// 给生成出来的敌人/Boss 套用全局倍率（生成后立刻调用，会回满生命）。
	/// 倍率**优先读敌人表的全局行**（`Config/Tables/Enemies/enemy_global.tres`），
	/// 读不到才用 <see cref="ConfigIndexData"/> 上的同名回退值 —— 这是"读不到表就用自身默认值"的落地。
	///
	/// ⚠️ 回退值只有一个来源（<see cref="ConfigIndexData"/> 的 <c>[Export]</c> 初值），且它与表里的值**一致**：
	/// 旧版本这里是 1f / 0.8f，而表里是 0.75 / 0.3，于是"表读不到"会让敌人伤害静默 ×2.67（没有任何日志）。
	/// 现在两处对齐 + <see cref="LogEnemyScalesOnce"/> 留痕。
	/// </summary>
	public static void ScaleEnemy(Unit unit, Node source, bool isBoss)
	{
		ConfigIndexData config = Settings;
		EnemyConfig? row = ConfigRows.ById<EnemyConfig>("enemy_global");

		float enemyHealth = row?.HealthMultiplier ?? config.EnemyHealthMultiplier;
		float enemyDamage = row?.DamageMultiplier ?? config.EnemyDamageMultiplier;
		float bossHealth = config.BossHealthMultiplier;
		float bossDamage = config.BossDamageMultiplier;
		float speed = row?.MoveSpeedMultiplier ?? config.EnemyMoveSpeedMultiplier;

		LogEnemyScalesOnce(row, enemyHealth, enemyDamage, bossHealth, bossDamage, speed);

		float health = isBoss ? bossHealth : enemyHealth;
		float damage = isBoss ? bossDamage : enemyDamage;

		unit.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = health, Source = source });
		unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = damage, Source = source });
		if (!isBoss)
		{
			unit.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = speed, Source = source });
		}

		unit.RefreshStats();
		unit.FillVitals();
	}

	/// <summary>本局第一次套用敌人倍率时把"来源 + 实际数值"写进日志（回退路径必须留痕，只写一次）。</summary>
	private static void LogEnemyScalesOnce(EnemyConfig? row, float enemyHealth, float enemyDamage, float bossHealth, float bossDamage, float speed)
	{
		if (_enemyScaleLogged)
		{
			return;
		}

		_enemyScaleLogged = true;
		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"敌人倍率：普通 生命×{enemyHealth:0.##} 伤害×{enemyDamage:0.##} 移速×{speed:0.##} / Boss 生命×{bossHealth:0.##} 伤害×{bossDamage:0.##}（来源：{(row != null ? "表 enemy_global" : "代码默认值 ConfigIndexData（表读不到）")}）");
	}
}
