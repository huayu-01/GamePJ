using System;
using System.Collections.Generic;
using ActRoguelike.Boss;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Encounter;

/// <summary>
/// Encounter + Spawn Budget（策划文档 §6.5 / 执行文档 §四）。
///
/// 两个量分开管：
/// - 总预算 TotalBudget：整场 Encounter 的生成额度，生成时按 Cost 扣减，**不会再生**；归零且场上清空即完成。
/// - 可支配额度：受 BudgetCap 限制、按 BudgetRegenPerSecond 再生的即时额度，用来控制生成节奏。
///
/// 敌人强度由 Stage/时间决定，不靠 Cost；Cost 只表示“占多少预算”。
/// </summary>
public partial class EncounterController : Node3D
{
	[Export] public float TotalBudget { get; set; } = 30f;
	[Export] public float BudgetRegenPerSecond { get; set; } = 1.5f;
	[Export] public float BudgetCap { get; set; } = 10f;
	[Export] public int MaxConcurrent { get; set; } = 6;
	[Export] public float SpawnInterval { get; set; } = 1.4f;
	[Export] public float SpawnRadius { get; set; } = 22f;
	[Export] public float MinSpawnDistance { get; set; } = 11f;
	[Export] public bool AutoStart { get; set; } = true;

	/// <summary>Encounter 完成后延迟出场的 Boss（阶段 6）。</summary>
	[Export] public PackedScene? BossScene { get; set; }

	[Export] public PackedScene? BossGuardScene { get; set; }
	[Export] public int BossGuardCount { get; set; } = 2;
	[Export] public float BossSpawnDelay { get; set; } = 2.5f;
	[Export] public float BossSpawnDistance { get; set; } = 16f;

	/// <summary>Boss 战初始压力等级（Stage 的免费挑战额度超出部分传进来，策划文档 §6.3）。</summary>
	public int BossInitialPressure { get; set; }

	/// <summary>本关的精英概率附加缩放（来自 StageDefinition）。</summary>
	public float EliteChanceScale { get; set; }

	/// <summary>本关敌人的生命/伤害缩放（在全局平衡倍率之上再乘一次）。</summary>
	public float EnemyHealthScale { get; set; } = 1f;
	public float EnemyDamageScale { get; set; } = 1f;

	public float BossHealthScale { get; set; } = 1f;
	public float BossDamageScale { get; set; } = 1f;

	/// <summary>Boss 词条数量（策划文档 §12 Boss 构筑），由 Stage 配置决定。</summary>
	public int BossPerkCount { get; set; } = 2;

	/// <summary>波次生成模式（用户需求：一波一波刷，剩 ≤1 只才刷下一波）。</summary>
	[Export] public bool WaveMode { get; set; } = true;
	[Export] public int WaveMinEnemies { get; set; } = 4;
	[Export] public int WaveMaxEnemies { get; set; } = 15;

	/// <summary>本场 Encounter 的波数上限（0 = 不限）。关卡核心文档 L225-227：高价值 3 波 / 中价值 2 波 / 低价值 1 波。</summary>
	[Export] public int WaveCount { get; set; }

	/// <summary>每一波最多出现几种不同类型的敌人。</summary>
	[Export] public int WaveMaxTypes { get; set; } = 3;

	/// <summary>本次 Boss 抽到的词条名，供 HUD 显示。</summary>
	private readonly List<EnemySpawnEntry> _waveEntries = new();
	private int _waveRemaining;
	private int _waveIndex;

	/// <summary>
	/// 本次 Boss 抽到的**行为型**（Boss Build）词条，供 HUD 与首领预览面板显示。
	///
	/// ⚠️ 只含行为型 —— 数值型（Pressure）由 <c>StageController.PressurePerks</c> 单独持有，
	/// 两套来源必须能区分，不许合并（关卡核心文档 §7.1）。
	/// 这里给的是词条对象（含 <c>Name</c> / <c>Description</c>），不是光秃秃的名字 ——
	/// HUD 与预览面板要显示的是同两份数据。
	/// </summary>
	public IReadOnlyList<BossPerk> BossPerks => _bossPerks;

	private readonly List<BossPerk> _bossPerks = new();

	[Signal]
	public delegate void BossSpawnedEventHandler();

	[Signal]
	public delegate void EncounterCompletedEventHandler();

	[Signal]
	public delegate void EncounterFailedEventHandler();

	public event Action<bool>? Finished;

	public bool IsRunning { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool BossActive { get; private set; }

	/// <summary>Boss 的引用，供 HUD 显示。</summary>
	public Unit? Boss { get; private set; }

	/// <summary>该节点是否是 Boss 节点。</summary>
	public bool IsBossNode => BossScene != null;

	/// <summary>「白板首领副本」（关卡核心文档 L231/L315）：每个 Encounter 的最终阶段都要打一个无压力、无词条的弱化首领。</summary>
	[Export] public PackedScene? EliteBossScene { get; set; }

	/// <summary>白板首领的血量倍率（相对普通首领）。</summary>
	[Export] public float EliteBossHealthScale { get; set; } = 0.35f;

	private bool _eliteBossSpawned;
	private Unit? _eliteBoss;

	/// <summary>节点是否打完：预算耗尽且场上清空；Boss 节点还要等 Boss 阵亡。</summary>
	public bool IsCleared
	{
		get
		{
			if (!IsCompleted)
			{
				return false;
			}

			if (!BossActive)
			{
				return true;
			}

			return Boss == null || !IsInstanceValid(Boss) || Boss.IsDead;
		}
	}
	public float RemainingBudget { get; private set; }
	public int AliveCount { get; private set; }
	public float ElapsedSeconds { get; private set; }
	public int TotalSpawned { get; private set; }

	private readonly List<EnemySpawnEntry> _entries = new();
	private readonly List<Unit> _spawned = new();
	private float _spendable;
	private float _spawnTimer;
	private float _bossPending = -1f;
	private bool _started;
	private Unit? _player;

	/// <summary>玩家阵亡 → Encounter 失败（阶段 7 的死亡结算会接这里）。</summary>
	private void OnPlayerDied(HitInstance hit) => FailEncounter();

	public override void _Ready()
	{
		AddToGroup("encounters");
		foreach (Node child in GetChildren())
		{
			if (child is EnemySpawnEntry { Enabled: true, Scene: not null } entry)
			{
				_entries.Add(entry);
			}
		}

		RemainingBudget = TotalBudget;
		_spendable = BudgetCap;

		// 注意：不在 _Ready 里按 AutoStart 启动。
		// 子节点的 _Ready 早于父节点，StageController 要到自己的 _Ready 才会把 AutoStart 关掉 ——
		// 在这里启动会让所有节点同时刷怪。改到首帧判断。
	}

	public void Start()
	{
		IsRunning = true;
		IsCompleted = false;
		_started = true;
		_spawnTimer = 0.3f;
	}

	public void Stop() => IsRunning = false;

	/// <summary>诊断用（自测/日志）：可生成项数量。</summary>
	public int EntryCount => _entries.Count;

	/// <summary>诊断用：是否已启动。</summary>
	public bool Started => _started;

	/// <summary>诊断用：波次模式开关。</summary>
	public bool WaveModeOn => WaveMode;

	/// <summary>诊断用：本波还剩多少只没出。</summary>
	public int WaveRemaining => _waveRemaining;

	/// <summary>诊断用：已开过几波。</summary>
	public int WaveIndex => _waveIndex;

	/// <summary>
	/// 诊断用：本关通过白名单筛选后实际可生成的敌人（写的是**敌人表的行 Id**）。
	/// 这条字符串就是"白名单按 Id 匹配、不再按中文显示名匹配"的证据。
	/// </summary>
	public string AllowedEntrySummary
	{
		get
		{
			var parts = new System.Collections.Generic.List<string>();
			foreach (EnemySpawnEntry entry in _entries)
			{
				EnemyConfig? row = EnemyRow(entry);
				parts.Add($"{row?.Id ?? entry.DisplayName}(成本{entry.Cost})");
			}

			return parts.Count == 0 ? "无" : string.Join(", ", parts);
		}
	}

	/// <summary>
	/// 彻底停止生成（开 Boss 门 / Boss 阵亡时调用）：冻结波次与预算，并标记本场结束。
	/// 只翻 <see cref="IsRunning"/> 不够——波次系统会在清场后又开新波，所以这里直接把 IsCompleted 立起来。
	/// </summary>
	public void StopSpawning()
	{
		IsRunning = false;
		_waveRemaining = 0;
		IsCompleted = true;
		ActRoguelike.Core.GameLog.Write("Spawn", "冻结生成：波次与预算停止（StopSpawning）");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_started)
		{
			// 首帧：这时父节点（StageController）已经关掉了被它接管的节点的 AutoStart。
			if (!AutoStart)
			{
				return;
			}

			Start();
		}

		PruneSpawned();


		float dt = (float)delta;

		if (_bossPending > 0f && !BossActive)
		{
			_bossPending -= dt;
			if (_bossPending <= 0f)
			{
				SpawnBossNow();
			}
		}

		if (!IsRunning || IsCompleted)
		{
			return;
		}
		// 完成判定必须放在并发上限与生成计时之前：
		// 否则场上敌人多于 MaxConcurrent 时会被 `AliveCount >= MaxConcurrent` 的 return 挡住，永远结算不了。
		if (TotalSpawned > 0 && AliveCount == 0 && (!WaveMode || _waveRemaining == 0))
		{
			CheckCompleted();
			return;
		}

		ElapsedSeconds += dt;
		_spendable = Mathf.Min(BudgetCap, _spendable + (BudgetRegenPerSecond * dt));
		RetireUnaffordableBudget();

		if (_player == null || !IsInstanceValid(_player) || _player.IsDead)
		{
			_player = FindPlayer();
			if (_player == null)
			{
				return;
			}

			_player.Died += OnPlayerDied;
		}

		if (WaveMode)
		{
			// 上一波只剩 ≤1 只（或已清空）时才开下一波；残留的那只不计入新波的类型限制。
			// 白板首领已经出场后不再开常规波（否则会「首领+杂兵」一起打）。
			if (_waveRemaining <= 0 && AliveCount <= 1 && !_eliteBossSpawned)
			{
				BeginWave();
			}

			if (_waveRemaining <= 0 && TotalSpawned > 0)
			{
				CheckCompleted();
				return;
			}
		}

		if (AliveCount >= MaxConcurrent)
		{
			return;
		}

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
		{
			return;
		}

		EnemySpawnEntry? entry = WaveMode ? PickWaveEntry() : PickEntry();
		if (entry == null)
		{
			if (WaveMode)
			{
				// 本波的类型都买不起（预算不足）：本波到此为止，等场上清空后结算。
				_waveRemaining = 0;
			}

			// 总预算还没花完但暂时抽不出（额度不够/时间门槛未到），稍后再试。
			_spawnTimer = 0.4f;
			CheckCompleted();
			return;
		}

		Spawn(entry);
		_spawnTimer = SpawnInterval;
		CheckCompleted();
	}

	private EnemySpawnEntry? PickEntry()
	{
		float totalWeight = 0f;
		foreach (EnemySpawnEntry entry in _entries)
		{
			if (!IsAvailable(entry))
			{
				continue;
			}

			totalWeight += Mathf.Max(0.01f, entry.Weight);
		}

		if (totalWeight <= 0f)
		{
			return null;
		}

		float roll = (float)GD.RandRange(0d, totalWeight);
		foreach (EnemySpawnEntry entry in _entries)
		{
			if (!IsAvailable(entry))
			{
				continue;
			}

			roll -= Mathf.Max(0.01f, entry.Weight);
			if (roll <= 0f)
			{
				return entry;
			}
		}

		return null;
	}

	/// <summary>开一波新的：随机挑 1~WaveMaxTypes 种类型，并决定这一波要出多少只。</summary>
	private void BeginWave()
	{
		if (_entries.Count == 0)
		{
			return;
		}

		// 到达波数上限就不再开新波（_waveRemaining 保持 0 → 清完场上就结算）。
		if (WaveCount > 0 && _waveIndex >= WaveCount)
		{
			return;
		}

		_waveIndex++;
		_waveEntries.Clear();

		var pool = new List<EnemySpawnEntry>(_entries);
		int maxTypes = Mathf.Clamp(WaveMaxTypes, 1, pool.Count);
		int types = GD.RandRange(1, maxTypes);
		for (int i = 0; i < types && pool.Count > 0; i++)
		{
			int pick = (int)(GD.Randi() % (uint)pool.Count);
			_waveEntries.Add(pool[pick]);
			pool.RemoveAt(pick);
		}

		_waveRemaining = SpawnPlanner.RollWaveSize(WaveMinEnemies, WaveMaxEnemies);
		ActRoguelike.Core.GameLog.Write("Spawn", $"第 {_waveIndex} 波开始：{_waveEntries.Count} 种类型（{string.Join("/", _waveEntries.ConvertAll(e => e.DisplayName))}）× 目标 {_waveRemaining} 只");

		// 用户需求：一波敌人一次性刷出来，不要逐只滴灌（生成间隔只用于"下一波"）。
		while (_waveRemaining > 0)
		{
			EnemySpawnEntry? burst = PickWaveEntry();
			if (burst == null)
			{
				_waveRemaining = 0;
				break;
			}

			Spawn(burst);
			_waveRemaining--;
		}

		ActRoguelike.Core.GameLog.Write("Spawn", $"第 {_waveIndex} 波已全部出场，场上 {AliveCount} 只");
	}

	private EnemySpawnEntry? PickWaveEntry()
	{
		var usable = new List<EnemySpawnEntry>();
		foreach (EnemySpawnEntry entry in _waveEntries)
		{
			if (IsAvailable(entry))
			{
				usable.Add(entry);
			}
		}

		return usable.Count == 0 ? null : usable[(int)(GD.Randi() % (uint)usable.Count)];
	}

	private bool IsAvailable(EnemySpawnEntry entry)
	{
		if (entry.Scene == null || RemainingBudget < entry.Cost || _spendable < entry.Cost)
		{
			return false;
		}

		return ElapsedSeconds >= entry.MinElapsedSeconds;
	}

	private void Spawn(EnemySpawnEntry entry)
	{
		if (entry.Scene == null || _player == null)
		{
			return;
		}

		RemainingBudget -= entry.Cost;
		_spendable -= entry.Cost;

		var enemy = entry.Scene.Instantiate<Unit>();

		// 单位表（`Config/Tables/Enemies/enemy_*.tres`）：生命/护甲/移速/攻击/持有技能/类标签。
		// ⚠️ 顺序必须是 实例化 → ApplyTo → AddChild：`Unit._EnterTree` 在 AddChild 时捕获基础值，
		// 晚套用会让"回满生命"算错（见 config-tables-inventory.md #10）。
		EnemyRow(entry)?.ApplyTo(enemy);

		AddChild(enemy);
		enemy.GlobalPosition = SpawnPlanner.FindSpawnPoint(_player?.GlobalPosition ?? GlobalPosition, MinSpawnDistance, SpawnRadius);

		// 精英概率倍率优先读敌人表的全局行（`Config/Tables/Enemies/enemy_global.tres`），读不到才用 Balance 的旧字段。
		float eliteScale = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.EnemyConfig>("enemy_global")?.EliteChanceMultiplier
			?? Balance.Settings.EliteChanceMultiplier;
		float eliteChance = (entry.EliteChance + EliteChanceScale) * eliteScale;
		if (eliteChance > 0f && GD.Randf() < eliteChance)
		{
			enemy.AddChild(new EliteModifier());
		}

		// 全局难度倍率在生成后套用（会回满生命）；再乘本关自己的缩放。
		Balance.ScaleEnemy(enemy, this, false);
		if (!Mathf.IsEqualApprox(EnemyHealthScale, 1f))
		{
			enemy.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = EnemyHealthScale, Source = this });
		}

		if (!Mathf.IsEqualApprox(EnemyDamageScale, 1f))
		{
			enemy.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = EnemyDamageScale, Source = this });
		}

		enemy.RefreshStats();
		enemy.FillVitals();

		_spawned.Add(enemy);
		ActRoguelike.Core.GameLog.Write("Spawn", $"生成 {entry.DisplayName}（剩余预算 {RemainingBudget:0}，场上 {AliveCount}）");
		TotalSpawned++;
		AliveCount++;
	}


	private void PruneSpawned()
	{
		int alive = 0;
		for (int i = _spawned.Count - 1; i >= 0; i--)
		{
			Unit unit = _spawned[i];
			if (!IsInstanceValid(unit) || unit.IsDead)
			{
				_spawned.RemoveAt(i);
				continue;
			}

			alive++;
		}

		AliveCount = alive;
	}

	/// <summary>
	/// 剩余预算买不起任何「当前已解锁」的敌人时，把它当作已花完。
	/// 否则会出现「预算剩 1、场上没人、既不刷怪也不算完成」的死锁（踩过一次）。
	/// 只统计已过时间门槛的条目：更便宜的敌人还没解锁时，等待是合理的。
	/// </summary>
	private void RetireUnaffordableBudget()
	{
		if (RemainingBudget <= 0f)
		{
			return;
		}

		float cheapest = float.MaxValue;
		foreach (EnemySpawnEntry entry in _entries)
		{
			if (entry.Scene != null && ElapsedSeconds >= entry.MinElapsedSeconds && entry.Cost < cheapest)
			{
				cheapest = entry.Cost;
			}
		}

		// 没有任何可生成的条目（或余额不足）→ 余额作废，让完成判定生效。
		if (RemainingBudget < cheapest)
		{
			RemainingBudget = 0f;
		}
	}

	/// <summary>
	/// 由 StageController 每帧驱动的完成判定：场上清空且波次出完就结算。
	/// 之所以从外部驱动，是因为 EncounterController 自身的 _PhysicsProcess 里存在提前返回路径，
	/// 导致内部的 CheckCompleted() 在某些状态下永远不会被调用（详见 docs/level-core-doc-gap.md）。
	/// </summary>
	public bool TryCompleteIfClear()
	{
		if (IsCompleted || !IsRunning || TotalSpawned <= 0 || AliveCount > 0)
		{
			return false;
		}

		if (WaveMode && _waveRemaining > 0)
		{
			return false;
		}

		CheckCompleted();
		return true;
	}

	private void CheckCompleted()
	{
		// 关卡核心文档 L196-198：只要求「本次生成的敌人全部击杀」，预算没花完也可以结束。
		// 注意：「还没生成过任何敌人」的判断只能放在 TryCompleteIfClear()（外部驱动入口），
		// 不能放在这里 —— 本方法也被生成循环调用，加守卫会 return 掉整帧的生成。
		if (AliveCount > 0)
		{
			return;
		}

		// 关卡核心文档 L231/L315：常规波清空后先进入「白板首领副本」，打完它才算完成。
		// 它独立于常规波数，也独立于 Boss Door 的最终首领（BossScene）——
		// 普通 Encounter 的 BossScene 是 null，所以这段不能写在 if (BossScene != null) 里面。
		if (EliteBossScene != null && !_eliteBossSpawned)
		{
			_eliteBossSpawned = true;
			ActRoguelike.Core.GameLog.Write("Spawn", "常规波已清空 → 进入白板首领副本");
			SpawnEliteBossNow();
			return;
		}

		IsRunning = false;
		IsCompleted = true;
		ActRoguelike.Core.GameLog.Write("Spawn", $"Encounter 清场完成：共生成 {TotalSpawned}，用时 {ElapsedSeconds:0}s");
		EmitSignal(SignalName.EncounterCompleted);
		Finished?.Invoke(true);

		if (BossScene != null)
		{
			_bossPending = BossSpawnDelay;
		}
	}

	/// <summary>
	/// 重置整场战斗（切关时调用）。必须把 Boss 状态一起清掉：
	/// 否则下一关会拿着上一关的「已死 Boss」引用，一进关就被判定胜利（踩过一次）。
	/// </summary>
	public void ResetEncounter()
	{
		IsRunning = false;
		IsCompleted = false;
		_started = false;
		BossActive = false;
		Boss = null;
		_bossPending = -1f;
		_eliteBossSpawned = false;
		_eliteBoss = null;
		RemainingBudget = TotalBudget;
		_spendable = BudgetCap;
		_spawnTimer = 0.3f;
		ElapsedSeconds = 0f;
		TotalSpawned = 0;
		AliveCount = 0;
		_spawned.Clear();

		// 波次状态也必须复位，否则上一场的 _waveIndex 会顶到 WaveCount，
		// 下一场的 BeginWave() 直接被「到达波数上限」拦掉 ——
		// 表现为「后续每次节点交互都直接出白板首领」（用户实测报的就是这个）。
		_waveIndex = 0;
		_waveRemaining = 0;
		_waveEntries.Clear();
	}

	/// <summary>
	/// 按 Stage 配置重建可生成列表。生成项的刷怪参数（成本/权重/解锁时间/精英概率）与开关**都来自敌人表**：
	/// 生成项与行按**预制体路径**配对（改中文显示名不再影响任何逻辑），白名单按行的 <c>Id</c> 匹配。
	/// 读不到行时（敌人表空 / Balance.tres 未加载）保留场景里的值、且不做白名单过滤（回退路径必须还能玩）。
	/// </summary>
	public void ApplyEntryFilter(StageDefinition definition)
	{
		_entries.Clear();
		foreach (Node child in GetChildren())
		{
			if (child is not EnemySpawnEntry entry || entry.Scene == null)
			{
				continue;
			}

			EnemyConfig? row = EnemyRow(entry);
			if (row != null)
			{
				entry.DisplayName = row.DisplayName;
				entry.Cost = Mathf.Max(1, row.Cost);
				entry.Weight = row.Weight;
				entry.MinElapsedSeconds = row.MinElapsedSeconds;
				entry.EliteChance = row.EliteChance;
			}

			entry.Enabled = definition.AllowsEnemy(row?.Id);
			if (entry.Enabled)
			{
				_entries.Add(entry);
			}
		}
	}

	/// <summary>生成项 → 敌人表的行（按预制体路径配对；读不到 → null，调用方回退到场景里的值）。</summary>
	private static EnemyConfig? EnemyRow(EnemySpawnEntry entry)
		=> entry.Scene == null ? null : ConfigRows.ByScenePath<EnemyConfig>(entry.Scene.ResourcePath);

	/// <summary>立即结算本节点：停止生成并清空场上敌人（阶段流程结束节点时使用）。</summary>
	public void ForceComplete()
	{
		RemainingBudget = 0f;
		foreach (Unit unit in _spawned)
		{
			if (IsInstanceValid(unit) && !unit.IsDead)
			{
				unit.ForceKill();
			}
		}
	}

	/// <summary>Encounter 清场后出场 Boss 与技能精英（阶段 6），阶段流程也可直接调用。</summary>
	/// <summary>生成白板首领：弱化血量、不带 Pressure、不带 Boss 构筑词条。</summary>
	private void SpawnEliteBossNow()
	{
		if (EliteBossScene == null)
		{
			return;
		}

		_eliteBoss = EliteBossScene.Instantiate<Unit>();
		BossRow(EliteBossScene)?.ApplyTo(_eliteBoss);
		AddChild(_eliteBoss);
		_eliteBoss.GlobalPosition = SpawnPlanner.FindSpawnPoint(_player?.GlobalPosition ?? GlobalPosition, MinSpawnDistance, SpawnRadius);

		// 血量倍率优先读首领行（`Config/Tables/Bosses/boss_*.tres` 的 EliteCopyHealthScale），
		// 读不到才用场景导出值 —— 以前 `Balance.tres` 里有一份**没有消费者**的同名副本，改了不起作用。
		float eliteScale = BossRow(EliteBossScene)?.EliteCopyHealthScale ?? EliteBossHealthScale;
		_eliteBoss.Stats.AddModifier(new StatModifier
		{
			Stat = "MaxHealthMultiplier",
			Operation = ModifierOperation.Multiply,
			Value = eliteScale,
			Source = this,
		});
		_eliteBoss.RefreshStats();
		_eliteBoss.FillVitals();
		_spawned.Add(_eliteBoss);
		AliveCount++;
		TotalSpawned++;
		ActRoguelike.Core.GameLog.Write("Spawn", $"白板首领降临（血量倍率 {eliteScale:0.##}（来源：{(BossRow(EliteBossScene) != null ? "表 boss_final" : "场景导出值")}），无压力 / 无词条）");
	}

	/// <summary>预制体 → 首领表的行（按 <c>ScenePath</c> 配对；读不到 → null，调用方回退到场景里的值）。</summary>
	private static BossConfig? BossRow(PackedScene scene)
		=> ConfigRows.ByScenePath<BossConfig>(scene.ResourcePath);

	public void SpawnBossNow()
	{
		if (BossScene == null || BossActive)
		{
			return;
		}

		BossActive = true;
		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		Vector3 center = _player != null && IsInstanceValid(_player) ? _player.GlobalPosition : GlobalPosition;

		Boss = BossScene.Instantiate<Unit>();
		// 单位表（`Config/Tables/Bosses/boss_*.tres`）：生命/护甲/移速/攻击/持有技能/标签。
		// 与敌人一样，必须在 `Unit._EnterTree` 之前套用（先 ApplyTo、再 AddChild）。
		BossRow(BossScene)?.ApplyTo(Boss);
		parent.AddChild(Boss);
		Boss.GlobalPosition = center + new Vector3(-BossSpawnDistance, 0.6f, 0f);
		Balance.ScaleEnemy(Boss, this, true);
		if (!Mathf.IsEqualApprox(BossHealthScale, 1f))
		{
			Boss.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = BossHealthScale, Source = this });
		}

		if (!Mathf.IsEqualApprox(BossDamageScale, 1f))
		{
			Boss.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = BossDamageScale, Source = this });
		}

		Boss.RefreshStats();
		Boss.FillVitals();

		var pressure = Boss.GetNodeOrNull<BossPressure>("Pressure");
		if (pressure != null)
		{
			pressure.Interval = Balance.Settings.BossPressureInterval;
			pressure.DamagePerLevel = Balance.Settings.BossPressureDamagePerLevel;
			pressure.AttackSpeedPerLevel = Balance.Settings.BossPressureAttackSpeedPerLevel;
			pressure.InitialLevel = BossInitialPressure;
		}

		// Boss 构筑：随机词条（策划文档 §12）。压力与技能的词条要在 Pressure 初始化之后套用。
		_bossPerks.Clear();
		foreach (BossPerk perk in BossPerkCatalog.Roll(BossPerkCount))
		{
			perk.Apply(new BossPerkContext { Boss = Boss, Source = this, Pressure = pressure });
			_bossPerks.Add(perk);
		}

		for (int i = 0; i < BossGuardCount && BossGuardScene != null; i++)
		{
			var guard = BossGuardScene.Instantiate<Unit>();
			// 护卫也走单位表（`enemy_skill_elite` 那一行）：与生成项一样，先套用再 AddChild。
			ConfigRows.ByScenePath<EnemyConfig>(BossGuardScene.ResourcePath)?.ApplyTo(guard);
			parent.AddChild(guard);
			float angle = Mathf.Pi * (i + 1) / (BossGuardCount + 1);
			guard.GlobalPosition = Boss.GlobalPosition + new Vector3(Mathf.Cos(angle) * 6f, 0.6f, Mathf.Sin(angle) * 6f);
		}

		EmitSignal(SignalName.BossSpawned);
	}

	/// <summary>玩家死亡 → Encounter 失败（阶段 7 的死亡结算会接管）。</summary>
	public void FailEncounter()
	{
		if (IsCompleted)
		{
			return;
		}

		IsRunning = false;
		IsCompleted = false;
		EmitSignal(SignalName.EncounterFailed);
		Finished?.Invoke(false);
	}

	private Unit? FindPlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup("player_units"))
		{
			if (node is Unit unit && unit.IsValidTarget)
			{
				return unit;
			}
		}

		return null;
	}
}