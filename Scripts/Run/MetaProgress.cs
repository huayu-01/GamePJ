using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Run;

/// <summary>
/// 局外成长（策划文档 §11）：碎片货币与永久强化，存到 user://meta.cfg。
/// 结算按进度给碎片，Base 界面消费碎片换永久等级，等级在下局开局折算成 Modifier。
/// </summary>
public static class MetaProgress
{
	private const string SavePath = "user://meta.cfg";

	private static int _shards;
	private static int _healthLevel;
	private static int _damageLevel;
	private static int _speedLevel;
	private static int _critLevel;
	private static int _reloadLevel;
	private static bool _loaded;

	public static int Shards => _shards;
	public static int HealthLevel => _healthLevel;
	public static int DamageLevel => _damageLevel;
	public static int SpeedLevel => _speedLevel;

	/// <summary>
	/// 永久强化的效果（下局开局应用）。**系数优先读局外成长表**
	/// （`Config/Tables/Meta/meta_*.tres`），读不到才用下面这些代码内置值 ——
	/// 值逐条对应：血量 10 / 伤害 0.05 / 移速 0.03 / 暴击 0.03 / 装填 0.06。
	/// </summary>
	public static float PerLevel(string id, float fallback)
		=> ConfigRows.ById<MetaUpgradeConfig>(id)?.PerLevel ?? fallback;

	/// <summary>查表 + 兜底的价格。表里的默认就是 `BaseCost=10 / CostGrowth=10`，与原来的 `10 * (level + 1)` 等值。</summary>
	private static int CostOf(string id, int level)
		=> ConfigRows.ById<MetaUpgradeConfig>(id)?.CostOf(level) ?? (10 * (level + 1));

	public static float BonusMaxHealth => _healthLevel * PerLevel("meta_health", 10f);

	public static float BonusDamageMultiplier => 1f + (_damageLevel * PerLevel("meta_damage", 0.05f));

	public static float BonusMoveSpeedMultiplier => 1f + (_speedLevel * PerLevel("meta_speed", 0.03f));

	/// <summary>暴击率 +3%/级。</summary>
	public static float BonusCritChance => _critLevel * PerLevel("meta_crit", 0.03f);

	/// <summary>装弹速度 +6%/级。</summary>
	public static float BonusReloadSpeed => 1f + (_reloadLevel * PerLevel("meta_reload", 0.06f));

	public static int HealthCost => CostOf("meta_health", _healthLevel);
	public static int DamageCost => CostOf("meta_damage", _damageLevel);
	public static int SpeedCost => CostOf("meta_speed", _speedLevel);
	public static int CritCost => CostOf("meta_crit", _critLevel);
	public static int ReloadCost => CostOf("meta_reload", _reloadLevel);
	public static int CritLevel => _critLevel;
	public static int ReloadLevel => _reloadLevel;

	public static void Load()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
		var config = new ConfigFile();
		if (config.Load(SavePath) != Error.Ok)
		{
			return;
		}

		_shards = (int)config.GetValue("meta", "shards", 0);
		_healthLevel = (int)config.GetValue("meta", "health_level", 0);
		_damageLevel = (int)config.GetValue("meta", "damage_level", 0);
		_speedLevel = (int)config.GetValue("meta", "speed_level", 0);
		_critLevel = (int)config.GetValue("meta", "crit_level", 0);
		_reloadLevel = (int)config.GetValue("meta", "reload_level", 0);
	}

	public static void Save()
	{
		var config = new ConfigFile();
		config.SetValue("meta", "shards", _shards);
		config.SetValue("meta", "health_level", _healthLevel);
		config.SetValue("meta", "damage_level", _damageLevel);
		config.SetValue("meta", "speed_level", _speedLevel);
		config.SetValue("meta", "crit_level", _critLevel);
		config.SetValue("meta", "reload_level", _reloadLevel);
		config.Save(SavePath);
	}

	public static void AddShards(int amount)
	{
		Load();
		if (amount < 0)
		{
			return;
		}

		// 即使本次收益为 0 也落盘一次，保证结算后存档文件一定存在。
		_shards += amount;
		Save();
	}

	public static bool BuyHealth() => Buy("meta_health", ref _healthLevel);

	public static bool BuyDamage() => Buy("meta_damage", ref _damageLevel);

	public static bool BuySpeed() => Buy("meta_speed", ref _speedLevel);

	public static bool BuyCrit() => Buy("meta_crit", ref _critLevel);

	public static bool BuyReload() => Buy("meta_reload", ref _reloadLevel);

	/// <summary>花掉碎片（商店节点用）。碎片不足返回 false 且不扣。</summary>
	public static bool TrySpend(int cost)
	{
		Load();
		if (cost <= 0 || _shards < cost)
		{
			return false;
		}

		_shards -= cost;
		Save();
		return true;
	}

	private static bool Buy(string id, ref int level)
	{
		Load();
		int cost = CostOf(id, level);
		if (_shards < cost)
		{
			return false;
		}

		_shards -= cost;
		level++;
		Save();
		return true;
	}
}
