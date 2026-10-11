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
	}

	/// <summary>给生成出来的敌人/Boss 套用全局倍率（生成后立刻调用，会回满生命）。</summary>
	public static void ScaleEnemy(Unit unit, Node source, bool isBoss)
	{
		BalanceConfig config = Settings;
		float health = isBoss ? config.BossHealthMultiplier : config.EnemyHealthMultiplier;
		float damage = isBoss ? config.BossDamageMultiplier : config.EnemyDamageMultiplier;

		unit.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = health, Source = source });
		unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = damage, Source = source });
		if (!isBoss)
		{
			unit.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = config.EnemyMoveSpeedMultiplier, Source = source });
		}

		unit.RefreshStats();
		unit.FillVitals();
	}
}
