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
	private static bool _loaded;
#if DEBUG
	private static bool _selfTestMemoryOnly;
	/// <summary>完整自测沿用玩家数值，但收益／消费只在本进程内生效。</summary>
	internal static void UseInMemoryStoreForSelfTest() { Load(); _selfTestMemoryOnly = true; }
#endif

	public static int Shards => _shards;
	public static int HealthLevel => _healthLevel;
	public static int DamageLevel => _damageLevel;
	public static int SpeedLevel => _speedLevel;

	/// <summary>
	/// 永久强化的效果（下局开局应用）。**系数优先读局外成长表**
	/// （`Config/Tables/Meta/meta_*.tres`），读不到才用下面这些代码内置值 ——
	/// 值逐条对应：血量 10 / 伤害 0.05 / 移速 0.03 / 暴击 0.03。
	/// ⚠️ P6b：「装填」那一条（`meta_reload`，0.06）随"没有弹匣的概念了"整条下线。
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

	public static int HealthCost => CostOf("meta_health", _healthLevel);
	public static int DamageCost => CostOf("meta_damage", _damageLevel);
	public static int SpeedCost => CostOf("meta_speed", _speedLevel);
	public static int CritCost => CostOf("meta_crit", _critLevel);
	public static int CritLevel => _critLevel;

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
	}

	public static void Save()
	{
#if DEBUG
		if (_selfTestMemoryOnly) return;
#endif
		var config = new ConfigFile();
		config.SetValue("meta", "shards", _shards);
		config.SetValue("meta", "health_level", _healthLevel);
		config.SetValue("meta", "damage_level", _damageLevel);
		config.SetValue("meta", "speed_level", _speedLevel);
		config.SetValue("meta", "crit_level", _critLevel);
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

	// ⚠️ P6b：`BuyHealth` / `BuyDamage` / `BuySpeed` / `BuyCrit` / `BuyReload` 五个方法**整组删除**
	// （用户裁定：它们随 `BaseScreen`（旧的"永久强化"页）一起成了**死 API**，没有任何调用者）。
	// 详细购买逻辑原来落在私有 `Buy(id, ref level)` 里，也一并删除 —— 现在局外消费碎片的唯一入口是
	// `TrySpend`（能力卡界面走 `AbilityStore.Spend`）。
	//
	// ⚚ **`Bonus*` 必须留**：`RunController.ApplyBalance` 每局开局仍在读它们（`BonusMaxHealth` /
	// `BonusDamageMultiplier` / `BonusMoveSpeedMultiplier` / `BonusCritChance`）。
	// `*Cost` / `*Level` 这几个只读访问器本轮**没有调用者但也没被裁定删**，先留着（`_healthLevel` 等字段
	// 仍由 `Load/Save` 维护）。

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
}
