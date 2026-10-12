using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.AbilityCards;

/// <summary>
/// 一次能力卡操作的结果。**UI 只按它决定"播哪个动画 + 放哪个音"** ——
/// 判定规则全部在 <see cref="AbilityStore"/> 里，界面不重复实现一遍（否则两处必然漂移）。
/// </summary>
public enum AbilityCardResult
{
	/// <summary>成功。</summary>
	Ok,

	/// <summary>这张卡不在表里（行被删了 / 索引里没有）——界面照旧显示，只是点了没反应。</summary>
	NoCard,

	/// <summary>还没解锁（升级 / 激活 / 重置都要求先解锁）。</summary>
	Locked,

	/// <summary>已经解锁过了。</summary>
	AlreadyUnlocked,

	/// <summary>碎片不够（解锁 / 升级 / 扩增额度都走这一条）。</summary>
	NotEnoughShards,

	/// <summary>额度点数不够（激活时占用超过剩余额度）。</summary>
	QuotaFull,

	/// <summary>已经到等级上限。</summary>
	MaxLevel,

	/// <summary>没升过级，没什么可重置的。</summary>
	NothingToReset,

	/// <summary>额度上限已到顶（没有可扩增的空间）。</summary>
	QuotaMaxed,
}

/// <summary>
/// 能力卡的**状态层**：解锁 / 激活 / 等级 / 额度点数的记账与存盘。
///
/// <list type="bullet">
/// <item><b>存哪</b>：`user://ability_cards.cfg`（Godot 原生 <see cref="ConfigFile"/>）——
///   与 `user://meta.cfg`（局外成长）/ `user://settings.cfg`（设置）**分文件**，三者互不影响：
///   `res://Config/` 是**表**（只读），`user://` 是**玩家进度**。见 `docs/ability-cards.md`。</item>
/// <item><b>资源</b>：花的/退的都是 <see cref="MetaProgress"/> 的**碎片**（局外资源），
///   本类**不另建一套货币** —— 需求里的"局外资源"就是它。</item>
/// <item><b>额度上限</b>：<c>ConfigIndexData.AbilityQuotaBase</c> + 局外成长行 <c>meta_quota</c> 的每级增量，
///   所以"额度上限本身也能用局外资源扩增"是**同一套局外成长表**里的第 6 行，不是新机制。</item>
/// <item><b>纪律</b>：任何一步失败都**不扣钱、不改状态**，并返回一个明确的
///   <see cref="AbilityCardResult"/> 让界面去震动元素（绝不静默失败）。</item>
/// </list>
/// </summary>
public static class AbilityStore
{
	/// <summary>玩家进度的存档文件（**不进仓库**，与 `user://meta.cfg` 同级）。</summary>
	public const string SavePath = "user://ability_cards.cfg";

	/// <summary>额度上限的局外成长行 Id（`Config/Tables/Meta/meta_quota.tres`）。</summary>
	public const string QuotaRowId = "meta_quota";

	private const string CardSection = "cards";
	private const string QuotaSection = "quota";
	private const string QuotaKey = "quota_level";

	/// <summary>卡的等级：<c>0 = 未解锁</c>，解锁后从 1 起。</summary>
	private static readonly System.Collections.Generic.Dictionary<string, int> Levels = new();

	/// <summary>已激活的卡 id。</summary>
	private static readonly System.Collections.Generic.HashSet<string> ActiveIds = new();

	private static int _quotaLevel;
	private static bool _loaded;
	private static bool _suppressSave;
	private static bool _quietLog;

	/// <summary>
	/// 付钱 / 退钱的口子。默认走 <see cref="MetaProgress"/> 的碎片；
	/// **换成别的钱包是本类唯一的依赖注入点**：自测用它换成内存钱包，
	/// 整段冒烟测试因此**一个字节都不会写进玩家的 `user://meta.cfg`**。
	/// </summary>
	public static System.Func<int, bool> Spend { get; set; } = MetaProgress.TrySpend;

	/// <summary>退钱的实现（默认 <see cref="MetaProgress.AddShards"/>；语义同上）。</summary>
	public static System.Action<int> Refund { get; set; } = MetaProgress.AddShards;

	// ───────────────────────── 表 / 额度上限 ─────────────────────────

	/// <summary>卡表（顺序 = `ConfigIndex.tres` 的 `AbilityCards` 数组顺序 = 矩阵里的排布顺序）。</summary>
	public static System.Collections.Generic.IReadOnlyList<AbilityCardConfig> Rows
		=> ConfigIndex.Settings.AbilityCards;

	/// <summary>按 Id 取一行（读不到返回 null）。</summary>
	public static AbilityCardConfig? Row(string? id) => ConfigRows.ById<AbilityCardConfig>(id);

	/// <summary>额度上限的初始值（`ConfigIndex.tres` 的全局字段）。</summary>
	public static int QuotaBase => Mathf.Max(0, ConfigIndex.Settings.AbilityQuotaBase);

	/// <summary>额度上限的局外成长行（`Config/Tables/Meta/meta_quota.tres`；读不到返回 null）。</summary>
	public static MetaUpgradeConfig? QuotaRow => ConfigRows.ById<MetaUpgradeConfig>(QuotaRowId);

	/// <summary>额度扩增的等级。</summary>
	public static int QuotaLevel => _quotaLevel;

	/// <summary>每级扩增多少额度（读不到 `meta_quota` 行时每级 +1，与表里的值一致）。</summary>
	public static float QuotaPerLevel => QuotaRow?.PerLevel ?? 1f;

	/// <summary>额度上限 = 基础 + 扩增等级 × 每级增量。</summary>
	public static int QuotaCap => QuotaBase + Mathf.RoundToInt(_quotaLevel * QuotaPerLevel);

	/// <summary>已占用的额度点数（激活中的卡各占 <see cref="AbilityCardConfig.QuotaCost"/>）。</summary>
	public static int QuotaUsed
	{
		get
		{
			int used = 0;
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null && IsActive(row.Id))
				{
					used += Mathf.Max(0, row.QuotaCost);
				}
			}

			return used;
		}
	}

	/// <summary>剩余额度点数。</summary>
	public static int QuotaLeft => Mathf.Max(0, QuotaCap - QuotaUsed);

	/// <summary>下一次扩增额度的价格（走局外成长行，读不到就用代码兜底）。</summary>
	public static int QuotaUpgradeCost => QuotaRow?.CostOf(_quotaLevel) ?? (30 + (20 * _quotaLevel));

	// ───────────────────────── 查询 ─────────────────────────

	/// <summary>这张卡的等级（0 = 未解锁）。</summary>
	public static int LevelOf(string? id)
		=> string.IsNullOrEmpty(id) ? 0 : (Levels.TryGetValue(id, out int level) ? level : 0);

	/// <summary>已解锁吗（等级 &gt; 0）。</summary>
	public static bool IsUnlocked(string? id) => LevelOf(id) > 0;

	/// <summary>激活中吗（占着额度）。</summary>
	public static bool IsActive(string? id) => !string.IsNullOrEmpty(id) && ActiveIds.Contains(id);

	/// <summary>已解锁但没激活（界面上的"恢复颜色"状态）。</summary>
	public static bool IsIdle(string? id) => IsUnlocked(id) && !IsActive(id);

	/// <summary>解锁张数 / 激活张数（自检与日志用）。</summary>
	public static int UnlockedCount
	{
		get
		{
			int count = 0;
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null && IsUnlocked(row.Id))
				{
					count++;
				}
			}

			return count;
		}
	}

	/// <summary>激活张数。</summary>
	public static int ActiveCount
	{
		get
		{
			int count = 0;
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null && IsActive(row.Id))
				{
					count++;
				}
			}

			return count;
		}
	}

	// ───────────────────────── 存 / 读 ─────────────────────────

	/// <summary>读档（第一次访问时自动调用；文件不存在 = 全新玩家，一切为 0）。</summary>
	public static void Load()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;

		// 碎片在 MetaProgress 自己的文件里，读卡状态前先把它读进来（界面上要显示碎片）。
		MetaProgress.Load();

		var config = new ConfigFile();
		if (config.Load(SavePath) != Error.Ok)
		{
			return;
		}

		_quotaLevel = (int)config.GetValue(QuotaSection, QuotaKey, 0);
		foreach (string id in config.GetSectionKeys(CardSection) ?? System.Array.Empty<string>())
		{
			if (string.IsNullOrEmpty(id))
			{
				continue;
			}

			if (id.EndsWith("_active", System.StringComparison.Ordinal))
			{
				string cardId = id[..^"_active".Length];
				if ((bool)config.GetValue(CardSection, id, false))
				{
					ActiveIds.Add(cardId);
				}

				continue;
			}

			if (id.EndsWith("_level", System.StringComparison.Ordinal))
			{
				Levels[id[..^"_level".Length]] = (int)config.GetValue(CardSection, id, 0);
			}
		}
	}

	/// <summary>写档（每次成功的操作都会调用）。</summary>
	public static void Save()
	{
		if (_suppressSave)
		{
			return;
		}

		var config = new ConfigFile();
		foreach (System.Collections.Generic.KeyValuePair<string, int> entry in Levels)
		{
			config.SetValue(CardSection, $"{entry.Key}_level", entry.Value);
		}

		foreach (string id in ActiveIds)
		{
			config.SetValue(CardSection, $"{id}_active", true);
		}

		config.SetValue(QuotaSection, QuotaKey, _quotaLevel);
		Error error = config.Save(SavePath);
		if (error != Error.Ok)
		{
			GD.PushWarning($"[AbilityCards] 存档写入失败（{SavePath}）：{error}");
		}
	}

	// ───────────────────────── 操作（界面唯一入口）─────────────────────────

	/// <summary>解锁一张卡（等级 0 → 1），消耗 <see cref="AbilityCardConfig.UnlockCost"/> 碎片。</summary>
	public static AbilityCardResult TryUnlock(AbilityCardConfig? row)
	{
		Load();
		if (row == null)
		{
			return AbilityCardResult.NoCard;
		}

		if (IsUnlocked(row.Id))
		{
			return AbilityCardResult.AlreadyUnlocked;
		}

		if (!PayCost(row.UnlockCost))
		{
			return AbilityCardResult.NotEnoughShards;
		}

		Levels[row.Id] = 1;
		Save();
		Log($"解锁「{row.DisplayName}」（{row.Id}）费用 {row.UnlockCost} 碎片 → Lv1");
		return AbilityCardResult.Ok;
	}

	/// <summary>升级一张卡（等级 +1），消耗 <see cref="AbilityCardConfig.UpgradeCostOf"/> 碎片。</summary>
	public static AbilityCardResult TryUpgrade(AbilityCardConfig? row)
	{
		Load();
		if (row == null)
		{
			return AbilityCardResult.NoCard;
		}

		if (!IsUnlocked(row.Id))
		{
			return AbilityCardResult.Locked;
		}

		int level = LevelOf(row.Id);
		if (level >= Mathf.Max(1, row.MaxLevel))
		{
			return AbilityCardResult.MaxLevel;
		}

		int cost = row.UpgradeCostOf(level);
		if (!PayCost(cost))
		{
			return AbilityCardResult.NotEnoughShards;
		}

		Levels[row.Id] = level + 1;
		Save();
		Log($"升级「{row.DisplayName}」Lv{level} → Lv{level + 1}，费用 {cost} 碎片");
		return AbilityCardResult.Ok;
	}

	/// <summary>激活 / 关闭一张卡（唯一的"占用额度"入口）。</summary>
	public static AbilityCardResult TryToggleActive(AbilityCardConfig? row)
	{
		Load();
		if (row == null)
		{
			return AbilityCardResult.NoCard;
		}

		if (!IsUnlocked(row.Id))
		{
			return AbilityCardResult.Locked;
		}

		if (ActiveIds.Contains(row.Id))
		{
			ActiveIds.Remove(row.Id);
			Save();
			Log($"关闭「{row.DisplayName}」（{row.Id}）→ 额度 {QuotaUsed}/{QuotaCap}");
			return AbilityCardResult.Ok;
		}

		if (Mathf.Max(0, row.QuotaCost) > QuotaLeft)
		{
			return AbilityCardResult.QuotaFull;
		}

		ActiveIds.Add(row.Id);
		Save();
		Log($"激活「{row.DisplayName}」（{row.Id}）占用 {row.QuotaCost} 点 → 额度 {QuotaUsed}/{QuotaCap}");
		return AbilityCardResult.Ok;
	}

	/// <summary>重置升级进度（等级回到 1）并**全额归还**已花掉的升级费（解锁费不退）。</summary>
	public static AbilityCardResult TryResetUpgrades(AbilityCardConfig? row)
	{
		Load();
		if (row == null)
		{
			return AbilityCardResult.NoCard;
		}

		if (!IsUnlocked(row.Id))
		{
			return AbilityCardResult.Locked;
		}

		int level = LevelOf(row.Id);
		if (level <= 1)
		{
			return AbilityCardResult.NothingToReset;
		}

		int refund = row.RefundFor(level);
		Levels[row.Id] = 1;
		Refund(refund);
		Save();
		Log($"重置「{row.DisplayName}」Lv{level} → Lv1，归还升级费 {refund} 碎片");
		return AbilityCardResult.Ok;
	}

	/// <summary>用局外资源扩增额度上限（+1 级 = <see cref="QuotaPerLevel"/> 点）。</summary>
	public static AbilityCardResult TryBuyQuota()
	{
		Load();
		if (QuotaRow == null)
		{
			return AbilityCardResult.NoCard;
		}

		int cost = QuotaUpgradeCost;
		if (!PayCost(cost))
		{
			return AbilityCardResult.NotEnoughShards;
		}

		_quotaLevel++;
		Save();
		Log($"扩增额度 +{QuotaPerLevel:0.##} → 上限 {QuotaCap}（Lv{_quotaLevel}），费用 {cost} 碎片");
		return AbilityCardResult.Ok;
	}

	/// <summary>付钱（0 或负数视为免费，直接放行）。</summary>
	private static bool PayCost(int cost) => cost <= 0 || Spend(cost);

	// ───────────────────────── 自检 / 探针专用（不改玩家的档）─────────────────────────

	/// <summary>
	/// 直接把一张卡设成某个状态。<b>只给自检与拍摄台用</b>：
	/// <paramref name="save"/> = false 时连存档都不写，所以冒烟测试可以随便摆状态、跑完原样还原。
	/// </summary>
	public static void DebugSetState(string id, int level, bool active, bool save = false)
	{
		Load();
		bool oldSuppress = _suppressSave;
		_suppressSave = !save;
		if (level <= 0)
		{
			Levels.Remove(id);
		}
		else
		{
			Levels[id] = level;
		}

		if (active)
		{
			ActiveIds.Add(id);
		}
		else
		{
			ActiveIds.Remove(id);
		}

		if (save)
		{
			Save();
		}

		_suppressSave = oldSuppress;
	}

	/// <summary>给自检 / 拍摄台用的**沙箱**是否开着（开着时所有操作都不落盘、也不花真实碎片）。</summary>
	public static bool SandboxActive => _sandboxDepth > 0;

	/// <summary>**拍摄台 / 探针用**：开一个沙箱 —— 把钱换成内存钱包、并禁止落盘。
	/// 于是界面上真的走完整的解锁 / 升级 / 激活 / 扩增链路（视觉与判定都是真代码），
	/// 但玩家的 `user://meta.cfg` 与 `user://ability_cards.cfg` **一个字节都不会变**。</summary>
	/// <param name="shards">内存钱包里的钱。</param>
	/// <param name="allowSave">true = 仍然允许落盘（端到端验证"激活的卡在下局开局生效"时才用，
	/// 钱还是内存钱包，所以不会花掉玩家的碎片）。</param>
	public static void DebugBeginSandbox(int shards = 1000, bool allowSave = false)
	{
		Load();
		if (_sandboxDepth++ > 0)
		{
			return;
		}

		_sandboxWallet = new Wallet(shards);
		_sandboxOldSpend = Spend;
		_sandboxOldRefund = Refund;
		_sandboxOldSuppress = _suppressSave;
		Spend = _sandboxWallet.TrySpend;
		Refund = _sandboxWallet.Refund;
		_suppressSave = !allowSave;
	}

	/// <summary>关掉沙箱（恢复真实钱包与落盘）。</summary>
	public static void DebugEndSandbox()
	{
		if (_sandboxDepth == 0 || --_sandboxDepth > 0)
		{
			return;
		}

		Spend = _sandboxOldSpend;
		Refund = _sandboxOldRefund;
		_suppressSave = _sandboxOldSuppress;
		_sandboxWallet = null;
	}

	/// <summary>**拍摄台用**：把额度扩增等级直接摆成某个值（不落盘）。</summary>
	public static void DebugSetQuotaLevel(int level)
	{
		Load();
		_quotaLevel = Mathf.Max(0, level);
	}

	private static int _sandboxDepth;
	private static Wallet? _sandboxWallet;
	private static System.Func<int, bool> _sandboxOldSpend = MetaProgress.TrySpend;
	private static System.Action<int> _sandboxOldRefund = MetaProgress.AddShards;
	private static bool _sandboxOldSuppress;

	/// <summary>把整份状态快照成一行文本（日志用）。</summary>
	public static string Summary()	{
		Load();
		var text = new System.Text.StringBuilder();
		foreach (AbilityCardConfig row in Rows)
		{
			if (row == null)
			{
				continue;
			}

			if (text.Length > 0)
			{
				text.Append(" · ");
			}

			text.Append($"{row.Id} Lv{LevelOf(row.Id)}{(IsActive(row.Id) ? "(激活)" : string.Empty)}");
		}

		return $"额度 {QuotaUsed}/{QuotaCap}（扩增 Lv{_quotaLevel}）｜碎片 {MetaProgress.Shards}｜{text}";
	}

	/// <summary>
	/// **状态层冒烟测试**（自测直接把它打出来的几行贴进日志）：
	/// 解锁 → 重复解锁 → 激活 → 额度不足 → 升级 → 上限 → 重置归还 → 未解锁路径 → 额度扩增。
	///
	/// ⚠️ 全程用**内存钱包**（换掉 <see cref="Spend"/> / <see cref="Refund"/>）+ 不落盘，
	/// 跑完把状态逐张还原 —— 玩家的 `user://meta.cfg` / `user://ability_cards.cfg` **一个字节都不动**。
	/// </summary>
	public static System.Collections.Generic.List<string> DebugSelfTestLines()
	{
		Load();
		var lines = new System.Collections.Generic.List<string>();

		AbilityCardConfig? card = ResolveSampleCard();
		AbilityCardConfig? grant = ResolveGrantCard();
		AbilityCardConfig? locked = Rows.Count > 1 ? Rows[1] : null;
		if (card == null)
		{
			lines.Add("配置表比对·能力卡：表里一张卡都没有（期望 ≥ 1）—— 检查 Config/Tables/AbilityCards/ 与 ConfigIndex.tres 的 AbilityCards 数组。");
			return lines;
		}

		lines.Add(
			$"配置表比对·能力卡：{Rows.Count} 张（来源：{(Rows.Count > 0 ? "表 AbilityCards" : "代码默认值（表读不到）")}）"
			+ $" 额度上限 {QuotaCap} = 基础 {QuotaBase} + 扩增 Lv{_quotaLevel} × {QuotaPerLevel:0.##}"
			+ $"（扩增行 {(QuotaRow == null ? "读不到" : QuotaRow.Id + " 费用 " + QuotaUpgradeCost)}）"
			+ $" 全部卡 Id [{string.Join(",", Ids())}]");

		// ——— 换内存钱包 + 禁用落盘，跑完整条链路 ———
		System.Func<int, bool> oldSpend = Spend;
		System.Action<int> oldRefund = Refund;
		bool oldSuppress = _suppressSave;
		bool oldQuiet = _quietLog;
		var wallet = new Wallet(1000);
		Spend = wallet.TrySpend;
		Refund = wallet.Refund;
		_suppressSave = true;
		_quietLog = true;

		// 逐张记住原状态，跑完还原（哪怕中途抛异常也走 finally）。
		var snapshot = new System.Collections.Generic.List<(string Id, int Level, bool Active)>();
		foreach (AbilityCardConfig row in Rows)
		{
			if (row != null)
			{
				snapshot.Add((row.Id, LevelOf(row.Id), IsActive(row.Id)));
			}
		}

		int quotaSnapshot = _quotaLevel;

		try
		{
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null)
				{
					DebugSetState(row.Id, 0, false);
				}
			}

			lines.Add($"能力卡·冒烟① 起点：碎片 {wallet.Shards}（内存钱包）｜{Summary()}");

			AbilityCardResult r1 = TryUnlock(card);
			lines.Add(
				$"能力卡·冒烟② 解锁「{card.DisplayName}」→ {r1}（期望 Ok）｜Lv{LevelOf(card.Id)}（期望 1）"
				+ $"｜碎片 {wallet.Shards}（期望 {1000 - card.UnlockCost}）｜解锁费 {card.UnlockCost}");

			AbilityCardResult r2 = TryUnlock(card);
			lines.Add($"能力卡·冒烟③ 重复解锁 → {r2}（期望 AlreadyUnlocked）｜碎片 {wallet.Shards}（期望不变 {1000 - card.UnlockCost}）");

			int shardsBeforeUpgrade = wallet.Shards;
			AbilityCardResult r3 = TryUpgrade(card);
			int cost1 = card.UpgradeCostOf(1);
			lines.Add(
				$"能力卡·冒烟④ 升级 Lv1→Lv2 → {r3}（期望 Ok）｜Lv{LevelOf(card.Id)}（期望 2）"
				+ $"｜碎片 {wallet.Shards}（期望 {shardsBeforeUpgrade - cost1}）｜升级费 {cost1}");

			// 一路升到上限，验证"到顶再升 = MaxLevel"。
			int guard = 0;
			while (LevelOf(card.Id) < Mathf.Max(1, card.MaxLevel) && guard++ < 16)
			{
				TryUpgrade(card);
			}

			AbilityCardResult r4 = TryUpgrade(card);
			lines.Add(
				$"能力卡·冒烟⑤ 升到上限（Lv{LevelOf(card.Id)}/上限 {card.MaxLevel}）再升 → {r4}（期望 MaxLevel）"
				+ $"｜重置应归还 {card.RefundFor(LevelOf(card.Id))} 碎片");

			int shardsBeforeReset = wallet.Shards;
			int refund = card.RefundFor(LevelOf(card.Id));
			AbilityCardResult r5 = TryResetUpgrades(card);
			lines.Add(
				$"能力卡·冒烟⑥ 长按右键重置 → {r5}（期望 Ok）｜Lv{LevelOf(card.Id)}（期望 1）"
				+ $"｜碎片 {wallet.Shards}（期望 {shardsBeforeReset + refund}）");

			AbilityCardResult r6 = TryResetUpgrades(card);
			lines.Add($"能力卡·冒烟⑦ 没升过级就重置 → {r6}（期望 NothingToReset）｜碎片 {wallet.Shards}（期望不变）");

			// 激活 → 额度记账。
			int quotaBefore = QuotaUsed;
			AbilityCardResult r7 = TryToggleActive(card);
			lines.Add(
				$"能力卡·冒烟⑧ 左键激活 → {r7}（期望 Ok）｜额度 {quotaBefore} → {QuotaUsed}/{QuotaCap}"
				+ $"（期望 +{card.QuotaCost}）｜IsActive={IsActive(card.Id)}");

			AbilityCardResult r8 = TryToggleActive(card);
			lines.Add($"能力卡·冒烟⑨ 再按一次关闭 → {r8}（期望 Ok）｜额度回到 {QuotaUsed}/{QuotaCap}（期望 {quotaBefore}）");

			// 未解锁路径：升级 / 激活 / 重置三条都必须被挡住（"失效的交互都要震动元素"的数据侧）。
			if (locked != null && locked.Id != card.Id)
			{
				DebugSetState(locked.Id, 0, false);
				lines.Add(
					$"能力卡·冒烟⑩ 未解锁「{locked.DisplayName}」：升级 {TryUpgrade(locked)} / 激活 {TryToggleActive(locked)} / 重置 {TryResetUpgrades(locked)}"
					+ $"（期望全部 Locked）｜碎片 {wallet.Shards}、额度 {QuotaUsed}（期望均不变）");
			}

			// 额度不足：把卡一张一张激活到装不下为止，再激活下一张必然是 QuotaFull（"必须取舍"的数据侧）。
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null)
				{
					DebugSetState(row.Id, 1, false);
				}
			}

			int filled = 0;
			AbilityCardConfig? blocked = null;
			foreach (AbilityCardConfig row in Rows)
			{
				if (row == null)
				{
					continue;
				}

				AbilityCardResult fit = TryToggleActive(row);
				if (fit == AbilityCardResult.QuotaFull)
				{
					blocked = row;
					break;
				}

				if (fit != AbilityCardResult.Ok)
				{
					break;
				}

				filled++;
			}

			lines.Add(
				$"能力卡·冒烟⑪ 额度不足（上限 {QuotaCap}）：装下 {filled} 张后额度 {QuotaUsed}/{QuotaCap}，"
				+ (blocked == null
					? "全部卡都装得下（期望至少有一张装不下 → QuotaFull）"
					: $"再激活「{blocked.DisplayName}」（要 {blocked.QuotaCost} 点）→ QuotaFull（期望 QuotaFull）")
				+ $"｜额度未变 {QuotaUsed}/{QuotaCap}");

			// 把状态摆回 ⑫ 期望的样子（全部归零，只留被测的那一张 Lv1）。
			foreach (AbilityCardConfig row in Rows)
			{
				if (row != null)
				{
					DebugSetState(row.Id, 0, false);
				}
			}

			DebugSetState(card.Id, 1, false);

			// 碎片不足。
			wallet.Shards = 0;
			if (locked != null && locked.Id != card.Id)
			{
				lines.Add($"能力卡·冒烟⑫ 碎片 0 时解锁 → {TryUnlock(locked)}（期望 NotEnoughShards）｜Lv{LevelOf(locked.Id)}（期望 0）");
			}

			lines.Add($"能力卡·冒烟⑬ 碎片 0 时升级 → {TryUpgrade(card)}（期望 NotEnoughShards）｜Lv{LevelOf(card.Id)}（期望 1）");
			wallet.Shards = 1000;

			// 额度扩增（"额度上限本身也能用局外资源扩增"）。
			int capBefore = QuotaCap;
			int quotaCost = QuotaUpgradeCost;
			AbilityCardResult r9 = TryBuyQuota();
			lines.Add(
				$"能力卡·冒烟⑭ 扩增额度（{QuotaRow?.DisplayName ?? "meta_quota"}）→ {r9}（期望 Ok）"
				+ $"｜上限 {capBefore} → {QuotaCap}（期望 +{QuotaPerLevel:0.##}）｜费用 {quotaCost}｜扩增 Lv{_quotaLevel}");

			if (grant != null)
			{
				lines.Add(
					$"能力卡·授予技能卡：「{grant.DisplayName}」（{grant.Id}）指向技能行 {grant.Skill?.Id}（节点 {grant.Skill?.NodeName}）"
					+ $"，Lv1 冷却 {grant.GrantedCooldown(1):0.##}s → Lv{grant.MaxLevel} 冷却 {grant.GrantedCooldown(grant.MaxLevel):0.##}s");
			}

			if (locked != null)
			{
				lines.Add(
					$"能力卡·示例卡面：「{locked.DisplayName}」额度 {locked.QuotaCost} 点 · 解锁 {locked.UnlockCost} ·"
					+ $" 升级 {locked.UpgradeBaseCost}+{locked.UpgradeCostGrowth}/级 · 上限 Lv{locked.MaxLevel} · 效果 {locked.EffectText(1)}");
			}
		}
		finally
		{
			foreach ((string id, int level, bool active) in snapshot)
			{
				DebugSetState(id, level, active);
			}

			_quotaLevel = quotaSnapshot;
			Spend = oldSpend;
			Refund = oldRefund;
			_suppressSave = oldSuppress;
			_quietLog = oldQuiet;
		}

		// 还原之后必须与起点逐位一致 —— 这一行就是"自测没污染玩家存档"的证据。
		lines.Add($"能力卡·冒烟⑮ 还原后：{Summary()}（期望与起点一致；内存钱包剩 {wallet.Shards}，玩家的 user:// 文件未改动）");
		return lines;
	}

	private static System.Collections.Generic.List<string> Ids()
	{
		var ids = new System.Collections.Generic.List<string>();
		foreach (AbilityCardConfig row in Rows)
		{
			if (row != null)
			{
				ids.Add(row.Id);
			}
		}

		return ids;
	}

	/// <summary>挑一张"只改数值"的卡做冒烟测试（优先第一张非授予技能卡）。</summary>
	private static AbilityCardConfig? ResolveSampleCard()
	{
		foreach (AbilityCardConfig row in Rows)
		{
			if (row != null && row.Effect != AbilityCardEffectKind.GrantSkill && row.MaxLevel >= 3)
			{
				return row;
			}
		}

		foreach (AbilityCardConfig row in Rows)
		{
			if (row != null)
			{
				return row;
			}
		}

		return null;
	}

	/// <summary>挑一张"授予技能"的卡（没有就返回 null）。</summary>
	private static AbilityCardConfig? ResolveGrantCard()
	{
		foreach (AbilityCardConfig row in Rows)
		{
			if (row is { Effect: AbilityCardEffectKind.GrantSkill, Skill: not null })
			{
				return row;
			}
		}

		return null;
	}

	private static void Log(string message)
	{
		// 冒烟测试期间静音：那一段自己有逐条 [SelfTest] 证据行，
		// 再打一遍"解锁了 X"会让人误以为玩家的存档真的被改了。
		if (_quietLog)
		{
			return;
		}

		GameLog.Write("Ability", message);
	}

	/// <summary>冒烟测试用的内存钱包（**不碰玩家的 `user://meta.cfg`**）。</summary>
	private sealed class Wallet
	{
		public Wallet(int shards) => Shards = shards;

		public int Shards { get; set; }

		public bool TrySpend(int cost)
		{
			if (cost <= 0)
			{
				return true;
			}

			if (Shards < cost)
			{
				return false;
			}

			Shards -= cost;
			return true;
		}

		public void Refund(int amount)
		{
			if (amount > 0)
			{
				Shards += amount;
			}
		}
	}
}
