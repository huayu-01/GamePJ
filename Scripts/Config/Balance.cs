using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 平衡配置单例（autoload）。所有系统通过 <see cref="Settings"/> 读数值。
/// 配置文件缺失或字段缺失时回退到 <see cref="BalanceConfig"/> 的代码默认值。
/// </summary>
public partial class Balance : Node
{
	public const string ConfigPath = "res://Config/Balance.tres";

	public static Balance? Instance { get; private set; }

	private static readonly BalanceConfig Fallback = new();

	/// <summary>"敌人倍率来源"只写一次日志（见 <see cref="LogEnemyScalesOnce"/>）。</summary>
	private static bool _enemyScaleLogged;

	public BalanceConfig Config { get; private set; } = new();

	/// <summary>
	/// <see cref="ConfigPath"/> 是否真的加载成功。供日志区分「表已加载」与「整份配置回退到代码默认值」——
	/// 两者在玩法上差别很大（例如 Stages 为空、玩家生命 160 vs 250），必须能一眼看出来。
	/// </summary>
	public bool FileLoaded { get; private set; }

	public static BalanceConfig Settings => Instance?.Config ?? Fallback;

	public override void _Ready()
	{
		Instance = this;
		if (ResourceLoader.Exists(ConfigPath) && GD.Load<BalanceConfig>(ConfigPath) is { } loaded)
		{
			Config = loaded;
			FileLoaded = true;
		}
		else
		{
			GD.PushWarning($"[Balance] 未找到 {ConfigPath}，使用代码默认值。");
		}

		// ⚠️ 必须 defer：`GameLog` 的 autoload 排在 `Balance` **之后**（project.godot 的 [autoload] 顺序），
		// 在 `_Ready` 里直接写日志会被静默丢掉（首轮实测过：日志里一行 [Config] 都没有）。
		// defer 之后所有 autoload 都已就绪，日志能落地，而主场景还没加载，所以不影响任何读取者。
		CallDeferred(nameof(BuildTableIndex));
	}

	/// <summary>
	/// 把分表的索引数组建成按 Id / Slot（以及节点表的按 Kind）查的字典。
	///
	/// **每一步都留痕**：日志里会写明每张表读到几行、来源是"表"还是"代码默认值"，
	/// 这样出问题时不需要猜这一局用的是哪一套数值（上一轮踩过的坑：整份 Balance.tres
	/// 静默回退，只有日志能证明）。
	/// </summary>
	private void BuildTableIndex()
	{
		ConfigRows.Rebuild(Config.Weapons);
		ConfigRows.Rebuild(Config.Characters);
		ConfigRows.Rebuild(Config.Enemies);
		ConfigRows.Rebuild(Config.Effects);
		ConfigRows.Rebuild(Config.Nodes);
		ConfigRows.Rebuild(Config.Upgrades);
		ConfigRows.Rebuild(Config.MetaUpgrades);

		// 节点表额外建"节点类型 → 行 Id"的索引（RewardService / NodePlacement 按类型查）。
		foreach (NodeConfig row in Config.Nodes)
		{
			ConfigRows.SetKind(row.Kind, row.Id);
		}

		// 增幅项要编译成运行期目录（表优先、代码内置兜底），编译结果与来源写在日志里。
		ActRoguelike.Rewards.RewardTable.Build(Config.Upgrades);

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

				GD.PushWarning($"[Balance] 关卡 {stage.Id} 的 AllowedEnemyIds 里有未知敌人 Id「{id}」（敌人表里没有这一行）。");
				ActRoguelike.Core.GameLog.Write("Config", $"⚠ 关卡 {stage.Id} 允许列表里的「{id}」在敌人表里找不到，这一条不会生效。");
			}
		}

		ActRoguelike.Core.GameLog.Write(
			"Config",
			$"配置表：武器 {ConfigRows.Count<WeaponConfig>()} / 角色 {ConfigRows.Count<CharacterConfig>()} / 敌人 {ConfigRows.Count<EnemyConfig>()} / 效果 {ConfigRows.Count<EffectConfig>()} / 节点 {ConfigRows.Count<NodeConfig>()} / 局外成长 {ConfigRows.Count<MetaUpgradeConfig>()}（Balance.tres {(FileLoaded ? "已加载" : "未加载（全部数值回退到代码默认值）")}）");
	}

	/// <summary>
	/// 给生成出来的敌人/Boss 套用全局倍率（生成后立刻调用，会回满生命）。
	/// 倍率**优先读敌人表的全局行**（`Config/Tables/Enemies/enemy_global.tres`），
	/// 读不到才用 <see cref="BalanceConfig"/> 上的同名回退值 —— 这是"读不到表就用自身默认值"的落地。
	///
	/// ⚠️ 回退值只有一个来源（<see cref="BalanceConfig"/> 的 <c>[Export]</c> 初值），且它与表里的值**一致**：
	/// 旧版本这里是 1f / 0.8f，而表里是 0.75 / 0.3，于是"表读不到"会让敌人伤害静默 ×2.67（没有任何日志）。
	/// 现在两处对齐 + <see cref="LogEnemyScalesOnce"/> 留痕。
	/// </summary>
	public static void ScaleEnemy(Unit unit, Node source, bool isBoss)
	{
		BalanceConfig config = Settings;
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
			$"敌人倍率：普通 生命×{enemyHealth:0.##} 伤害×{enemyDamage:0.##} 移速×{speed:0.##} / Boss 生命×{bossHealth:0.##} 伤害×{bossDamage:0.##}（来源：{(row != null ? "表 enemy_global" : "代码默认值 BalanceConfig（表读不到）")}）");
	}
}
