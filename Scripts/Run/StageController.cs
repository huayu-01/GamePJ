using System;
using System.Collections.Generic;
using ActRoguelike.Boss;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Run;

/// <summary>
/// Stage：一个可自由探索的关卡（策划文档 §6.2）。
/// 地面上散布可交互节点（每局随机启用若干）：走近互动 → 拿奖励 → 触发一场 Encounter；
/// Boss Door 随时可开，玩家可以放弃剩余节点直接挑战 Boss；Boss 阵亡即 Stage 完成。
/// </summary>
public partial class StageController : Node3D
{
	public static StageController? Instance { get; private set; }

	/// <summary>每局从候选位置里随机启用多少个奖励节点。</summary>
	[Export] public int ActiveNodeCount { get; set; } = 3;

	/// <summary>Boss 死后降临的可交互节点（在 Main.tscn 里指向 RewardNode.tscn）。</summary>
	[Export] public PackedScene? PortalScene { get; set; }

	/// <summary>隔离次数：超出的次数会变成 Boss 战的初始压力等级（策划文档 §6.3）。</summary>
	/// <summary>
	/// 免费挑战额度 —— **玩家侧显示为「隔离次数」**（关卡核心文档 L405-416：
	/// 初始 1 次、每关 +1、上限 2；非战斗节点不扣额度）。
	/// </summary>
	[Export] public int FreeChallengeQuota { get; set; } = 2;

	/// <summary>降临物：从多高处落下来（米）、下落时长、落地回弹的高度与时长。</summary>
	private const float PortalDropHeight = 12f;
	private const float PortalDropSeconds = 0.7f;
	private const float PortalBounceHeight = 0.35f;
	private const float PortalBounceSeconds = 0.18f;

	public event Action<int, EncounterController>? NodeStarted;
	public event Action<int, EncounterController>? NodeCleared;
	public event Action? Victory;

	public bool IsVictory { get; private set; }

	/// <summary>
	/// 「挑战者」状态：挑战进行中（用户要求由关卡管理器持有）。
	/// 交互物在交互时读它决定"弹菜单还是拒绝"，玩家的地面标记也读它决定显示与否。
	///
	/// 🔴 <b>这里曾经把「打完 Boss 之后的收尾阶段」也算成挑战中</b>（原式：
	/// <c>_encounterRunning || (_bossFight &amp;&amp; !IsVictory)</c>），而 <c>IsVictory</c> 必须**互动降临节点**
	/// 才会变 true —— 于是 Boss 一死就死锁：地面标记一直亮着"挑战中"，玩家按 F 想进降临节点却被
	/// <see cref="Interactable.Interact"/> 的挑战者闸门拒掉，永远进不了下一关（用户报的就是这个）。
	///
	/// 现在把 <see cref="_bossCleared"/> 也算进"这一战结束了"：Boss 阵亡的那一刻
	/// （<see cref="_PhysicsProcess"/> 的 <c>_bossCleared = true</c>）挑战者状态立刻解除，
	/// 降临节点落地的提示与交互才走得通。语义上 Boss 战**从来不是**节点挑战，
	/// 它和 <see cref="_encounterRunning"/>（节点挑战）本来就是两条线，只有"还有 Boss 活着"期间算挑战中。
	///
	/// <c>!IsVictory</c> 仍然在最前面（本关已经通关时任何挑战态都不算数），只是它**不能**再兼任
	/// "Boss 打完了吗"的判断。
	/// </summary>
	public bool ChallengerActive => !IsVictory && (_encounterRunning || (_bossFight && !_bossCleared && _bossEncounter?.Boss is { } boss && !boss.IsDead));
	public bool BossOpened { get; private set; }
	public int ChallengesUsed { get; private set; }
	public float ElapsedSeconds { get; private set; }

	/// <summary>还剩几个没用过的奖励节点。</summary>
	public int RemainingNodes
	{
		get
		{
			int count = 0;
			foreach (RewardNode node in _nodes)
			{
				if (node.Enabled && !node.Used)
				{
					count++;
				}
			}

			return count;
		}
	}

	public int FreeQuotaLeft => Mathf.Max(0, FreeChallengeQuota - ChallengesUsed);

	/// <summary>
	/// **§8.2 的免费挑战闸门**：这一次结算对应的挑战是不是「免费挑战」（在本关免费额度之内）。
	///
	/// 额度在 <see cref="StartEncounter"/> 里随 <see cref="ChallengesUsed"/> 一起扣（§8.2 第 1–2 步：
	/// 「检查当前免费挑战额度」→「若有额度，则扣除 1 次」→「启动 Encounter」），所以第 N 次挑战是否免费
	/// 就是 <c>N ≤ 本关额度</c>。不刷怪的节点不进 <see cref="StartEncounter"/>，也就不会消耗额度（§8.1）。
	///
	/// ⚠️ 挑战数没到额度时一律判为免费 —— 包括"直接调用结算、根本没有开挑战"的情形（例如自测）。
	/// 这正是 §8.2 第 5 条的反面：「若没有免费额度，则挑战完成后增加对应类型的 Pressure」。
	/// </summary>
	public bool CurrentChallengeFree => ChallengesUsed <= FreeChallengeQuota;

	/// <summary>本关跨过 Pressure 阈值的次数（每一次都会立即抽一条数值型强化，§7.3）。</summary>
	public int PressureRolls => _pressureRolls;

	/// <summary>诊断用：本关累计已开的波数（白板首领门槛读它；自测比对用）。</summary>
	public int WavesOpenedInStage => _wavesOpenedInStage;

	/// <summary>
	/// 诊断用：**本关定期刷出的"游离怪"当前还活着几只**（用户需求 #6）。列表是"刷出来时登记"的，
	/// 读的时候顺手剪掉已死/已释放的，所以它同时是"场上真实数量"。
	/// </summary>
	public int AmbientAliveCount
	{
		get
		{
			PruneAmbient();
			return _ambientUnits.Count;
		}
	}

	/// <summary>诊断用：定期刷怪当前的**强度档位**（0 = 还没跨过第一个 120 秒）。</summary>
	public int AmbientStrengthStep => _ambientSteps;

	/// <summary>
	/// 诊断用（自测/日志）：定期刷怪的一行摘要 —— 间隔 / 每档秒数 / 每档强度 / 上限 / 已过秒数 / 当前档位 / 场上只数。
	/// </summary>
	public string AmbientSummary
	{
		get
		{
			ConfigIndexData config = ConfigIndex.Settings;
			return $"间隔 {config.AmbientSpawnInterval:0.#}s / 每 {config.AmbientSpawnEscalateSeconds:0.#}s 增强一档（每档 ×{1f + config.AmbientSpawnStrengthStep:0.##}）/ 上限 {config.AmbientSpawnMaxAlive} 只 / 本关已过 {_ambientElapsed:0.#}s / 当前档位 {_ambientSteps} / 场上 {AmbientAliveCount} 只";
		}
	}

	/// <summary>
	/// 诊断用：**「挑战者状态」当前是开是关**。
	///
	/// 自测用它验证用户报的那个 bug（直接开门打 Boss → 打完卡在"挑战中"、进不了降临节点）：
	/// Boss 阵亡之后这个值必须是 <c>false</c>，否则 <c>Interactable.Interact</c> 的挑战者闸门
	/// 会把降临节点的交互拒掉（那时 <c>IsVictory</c> 还是 false，而它正是唯一能翻它的动作 —— 死锁）。
	/// </summary>
	public bool ChallengerStateNow => ChallengerActive;

	/// <summary>还没凑够一个阈值的余数（每次结算完清零、不结转，§7.3 第 5 条）。</summary>
	public float PressureBank => _pressureBank;

	/// <summary>本关的 Pressure 阈值（§7.3；关卡行留 0 → ConfigIndex 的全局默认值）。</summary>
	public float PressureThresholdNow => ResolveThreshold(Definition, ConfigIndex.Settings);

	/// <summary>
	/// 本关由 Pressure 转换（+ 节点行额外次数）抽到的**数值型**强化，按抽取顺序（含重复）。
	/// Boss 出生（<c>BossSpawned</c>）时这些就会被套到它身上，所以它同时是"HUD / 预览面板要显示什么"的真源。
	/// </summary>
	public IReadOnlyList<BossPerk> PressurePerks => _pendingPressurePerks;

	/// <summary>上面那批数值强化是否已经套到 Boss 身上（Boss 出生之后为 true）。</summary>
	public bool PressurePerksApplied => _pressureApplied;

	// 兼容旧 HUD/流程代码的最小壳。
	public int CurrentIndex => BossOpened ? 1 : 0;
	public int NodeCount => _nodes.Count;
	public EncounterController? CurrentNode => _bossFight ? _bossEncounter : _encounter;

	private readonly List<RewardNode> _nodes = new();
	private EncounterController? _encounter;
	private EncounterController? _bossEncounter;
	private bool _encounterRunning;
	private bool _bossFight;
	private bool _bossCleared;
	private RewardNodeKind? _currentNodeKind;

	/// <summary>Pressure 的"银行"：还没凑够一个阈值的余数。凑够一个阈值就清零（§7.3 第 5 条：不结转）。</summary>
	private float _pressureBank;

	/// <summary>本关已跨过阈值的次数（= 由 Pressure 转换触发的数值强化次数）。与 §8 的免费额度溢出是两码事。</summary>
	private int _pressureRolls;

	/// <summary>
	/// 本关**已经开过多少波**（跨多场 Encounter 累加，每关清零）。
	///
	/// 只有白板首领的波次门槛读它（用户需求：「白板首领…基本只在第 8 波怪刷出」）：
	/// 节点表给一场 Encounter 的波数只有 1～4（低 1 / 中 3 / 高 4），
	/// 所以门槛必须按**本关累计**算 —— 按单场算的话门槛取 8 永远到不了，
	/// 白板首领会从"每场都出"直接变成"永远不出"。
	///
	/// 每次开战前写进 <see cref="EncounterController.WavesOpenedBefore"/>（那边自己不重置，
	/// 避免"本关累计"这个量在两处各记一份）。
	/// </summary>
	private int _wavesOpenedInStage;

	// ─────────────────────────────────────────────────────────────────────────
	// 关卡内的定期刷怪（用户需求 #6，2026-10-12）
	//
	// 用户原话：「当角色进入关卡时，场景里会**定期在任意有效位置刷出怪物**，**每过 120 秒**
	// （可调整的参数）就**增强接下来刷出的怪物**（提升强度或更强类型的怪）」。
	//
	// 它与节点的 Encounter **并存**（不是替代）：Encounter 仍按节点价值的波数开战，
	// 这套只是"关卡本身一直在往外冒怪"的压力。
	// 落点**复用** EncounterController 的刷怪路径（→ `SpawnPlanner` → `MapManager.SpawnPoint`），
	// 没有第二套"找有效位置"的实现。
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>距离下一次刷游离怪还有多少秒。</summary>
	private float _ambientTimer;

	/// <summary>本关已经过了多少秒（只在这套机制开着时累加；切关清零）。</summary>
	private float _ambientElapsed;

	/// <summary>当前强度档位 = 本关已过秒数 ÷ 每档秒数（切关清零）。</summary>
	private int _ambientSteps;

	/// <summary>本关刷出的游离怪（只用来数"场上还有几只"，已死/已释放的读的时候剪掉）。</summary>
	private readonly List<Unit> _ambientUnits = new();

	/// <summary>
	/// 本关由 Pressure 转换（+ 节点行额外次数）抽到、但**还没套用**的数值强化 ——
	/// 挑战发生时 Boss 还不存在，所以在它出生时才套（见 <see cref="ApplyPendingPressurePerks"/>）。
	/// </summary>
	private readonly List<BossPerk> _pendingPressurePerks = new();

	/// <summary>上面那批数值强化是否已经套到 Boss 身上（BossSpawned 触发后为 true）。</summary>
	private bool _pressureApplied;

	private bool _rewardPending;

	/// <summary>当前关卡的配置与序号（1 起）。</summary>
	public StageDefinition? Definition { get; private set; }
	public int StageNumber { get; private set; } = 1;

	/// <summary>应用一关的配置并重置关卡状态（开局与切关都走这里）。</summary>
	public void ApplyStage(StageDefinition definition, int stageNumber)
	{
		Definition = definition;
		StageNumber = stageNumber;
		ActiveNodeCount = definition.RewardNodeCount;
		FreeChallengeQuota = definition.FreeChallengeQuota;

		// 证明"这一关用的是表里的哪一行、哪套 Pressure 规则"（配置表回退纪律：回退/来源要留痕）。
		string tags = definition.PressureUpgradeTags.Length == 0
			? "不限制"
			: string.Join(",", definition.PressureUpgradeTags);
		ActRoguelike.Core.GameLog.Write("Node", $"Stage 表行：{definition.Id}（{definition.DisplayName}）Boss Pressure 规则：增长倍率 {definition.PressureGrowthMultiplier:0.##} / 阈值 {(definition.PressureThreshold > 0 ? definition.PressureThreshold : ActRoguelike.Config.ConfigIndex.Settings.PressurePerPoint)} / 数值强化候选 [{tags}]");
		ActRoguelike.Core.GameLog.Write("Node", $"Pressure 节点驱动：{DescribeNodePressureRules()}");

		if (_encounter != null)
		{
			_encounter.TotalBudget = definition.CombatBudget;
			_encounter.MaxConcurrent = definition.MaxConcurrent;
			_encounter.SpawnInterval = definition.SpawnInterval;
			_encounter.EliteChanceScale = definition.EliteChanceScale;
			_encounter.EnemyHealthScale = definition.EnemyHealthMultiplier;
			_encounter.EnemyDamageScale = definition.EnemyDamageMultiplier;
			_encounter.ApplyEntryFilter(definition);
		}

		if (_bossEncounter != null)
		{
			_bossEncounter.BossGuardCount = definition.BossGuardCount;
			_bossEncounter.BossHealthScale = definition.BossHealthMultiplier;
			_bossEncounter.BossDamageScale = definition.BossDamageMultiplier;
			_bossEncounter.BossPerkCount = definition.BossPerkCount;
		}

		ResetStage();
	}

	/// <summary>重置关卡：节点重新随机布置、挑战计数清零、门重新关闭、场上清空。</summary>
	private void ResetStage()
	{
		ChallengesUsed = 0;
		BossOpened = false;
		IsVictory = false;
		_bossFight = false;
		_bossCleared = false;
		_currentNodeKind = null;
		_pressureBank = 0f;
		_pressureRolls = 0;
		_wavesOpenedInStage = 0;
		_pendingPressurePerks.Clear();
		_pressureApplied = false;

		// 定期刷怪（需求 #6）按**关卡**重置：换关就从头计时、档位回 0（游离怪本身由 ClearField 清掉）。
		_ambientTimer = ConfigIndex.Settings.AmbientSpawnInterval;
		_ambientElapsed = 0f;
		_ambientSteps = 0;
		_ambientUnits.Clear();

		// 免费挑战额度：初始 1 次、每关 +1、上限 2（关卡核心文档 L405-416）。
		// 注意只统计"会刷怪的节点"，StartEncounter 才会让 ChallengesUsed 递增。
		FreeChallengeQuota = Mathf.Min(
			ActRoguelike.Config.ConfigIndex.Settings.FreeChallengeMaxQuota,
			ActRoguelike.Config.ConfigIndex.Settings.FreeChallengeBaseQuota + (StageNumber - 1));

		// 清掉上一关 Boss 死后降临的节点。
		foreach (Node child in GetChildren())
		{
			if (child is RewardNode { Kind: RewardNodeKind.Portal } portal)
			{
				portal.QueueFree();
			}
		}
		_encounterRunning = false;
		_rewardPending = false;
		ElapsedSeconds = 0f;

		foreach (RewardNode node in _nodes)
		{
			node.ResetNode();
			NodePlacement.PlaceSafely(node);
		}

		// Boss 门也要复位，否则切关后按 F 没反应（用掉一次就废了）。
		// 注意：用过的门会"消失"并退出 Interactable.All，所以要按 Stage 的子节点找（门的父节点就是 Stage）。
		foreach (Node child in GetChildren())
		{
			if (child is BossDoor door)
			{
				door.ResetNode();
			}
		}

		PlaceNodes();

		// 关卡核心文档 L87-93：每个 Stage 固定 10 个节点 —— 高价值 2 / 中价值 5 / 低价值 3。
		EnableByLayer(high: 2, mid: 5, low: 3);
		ClearField();
		_encounter?.ResetEncounter();
		_bossEncounter?.ResetEncounter();
	}

	/// <summary>清掉上一关残留的单位（敌人、Boss、护卫都要清）。</summary>
	private void ClearField()
	{
		foreach (string group in new[] { "enemy_units", "boss_units" })
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node is Unit { IsDead: false } unit)
				{
					unit.ForceKill();
				}
			}
		}
	}

	public override void _Ready()
	{
		Instance = this;
		AddToGroup("stage");
		CollectChildren();

		// 关卡核心文档 §7.5：Pressure 转换出的**数值强化**在 Boss 出生时才套得上（挑战发生时它还不存在）。
		// 用 EncounterController 的 BossSpawned 信号挂钩，而不是让 EncounterController 认识 Pressure ——
		// "一关怎么攒 Pressure"不该渗进"一场遭遇"那一层。成对退订见 _ExitTree。
		if (_bossEncounter != null)
		{
			_bossEncounter.BossSpawned += ApplyPendingPressurePerks;
		}

		PlaceNodes();

		// 关卡核心文档 L87-93：每个 Stage 固定 10 个节点 —— 高价值 2 / 中价值 5 / 低价值 3。
		EnableByLayer(high: 2, mid: 5, low: 3);
	}

	public override void _ExitTree()
	{
		// 订阅必须成对（项目纪律）：信号发在已释放的 StageController 上会直接报错。
		if (_bossEncounter != null)
		{
			_bossEncounter.BossSpawned -= ApplyPendingPressurePerks;
		}

		if (Instance == this)
		{
			Instance = null;
		}
	}

	/// <summary>兼容旧调用：节点布置已在 _Ready 完成，这里只是确保战斗模板处于待命状态。</summary>
	public void StartRun()
	{
		PlaceNodes();

		// 关卡核心文档 L87-93：每个 Stage 固定 10 个节点 —— 高价值 2 / 中价值 5 / 低价值 3。
		EnableByLayer(high: 2, mid: 5, low: 3);
	}

	/// <summary>兼容旧调用（现在不再有线性推进）。</summary>
	public void Advance()
	{
	}

	private void CollectChildren()
	{
		foreach (Node child in GetChildren())
		{
			switch (child)
			{
				case RewardNode node:
					_nodes.Add(node);
					break;
				case EncounterController { IsBossNode: true } boss:
					_bossEncounter = boss;
					break;
				case EncounterController encounter:
					_encounter = encounter;
					break;
			}
		}
	}

	/// <summary>每局随机布置：只启用 ActiveNodeCount 个节点，其余禁用并隐藏（生成后固定）。</summary>
	private void PlaceNodes()
	{
		var pool = new List<RewardNode>(_nodes);
		for (int i = pool.Count - 1; i > 0; i--)
		{
			int j = (int)(GD.Randi() % (uint)(i + 1));
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}

		int active = Mathf.Min(ActiveNodeCount, pool.Count);
		for (int i = 0; i < pool.Count; i++)
		{
			pool[i].Enabled = i < active;
			pool[i].Visible = i < active;
		}
	}

	/// <summary>玩家互动了奖励节点：先三选一，选完再按模板开一场 Encounter。</summary>
	public bool RequestRewardNode(RewardNode node)
	{
		if (IsVictory || _rewardPending || _encounterRunning || !node.Enabled || node.Used)
		{
			return false;
		}

		// 降临节点（Boss 死后的传送门）：纯关卡逻辑，不弹奖励、不刷怪。
		if (node.Kind == RewardNodeKind.Portal)
		{
			ActRoguelike.Core.GameLog.Write("Node", "互动降临节点 → 进入下一关");
			IsVictory = true;
			Victory?.Invoke();
			return true;
		}

		if (RunController.Instance is not { } run)
		{
			return false;
		}

		_rewardPending = true;
		ActRoguelike.Core.GameLog.Write("Node", $"互动奖励节点：{node.Prompt}（Kind={node.Kind}，刷怪={node.SpawnsEncounter}）");
		bool accepted = run.OpenRewardForNode(node.Kind, () =>
		{
			_rewardPending = false;

			// 节点可配置是否刷怪：不刷怪的节点拿了奖励就直接回到探索。
			if (node.SpawnsEncounter)
			{
				StartEncounter(node);
			}
		});

		if (!accepted)
		{
			_rewardPending = false;
		}

		return accepted;
	}

	private void StartEncounter(RewardNode? node = null)
	{
		if (_encounter == null || IsVictory)
		{
			return;
		}

		ChallengesUsed++;
		_encounterRunning = true;

		// 记下"这次挑战是哪种奖励节点"：清场后由它决定 Pressure 的基础点数 / 额外比例 / 候选标签（§7.2）。
		// ⚠️ 曾经漏了这一行：字段存在于 _PhysicsProcess 的结算分支里，却从来没人赋值，
		// 于是**正常游玩时 Pressure 一次都不会累积**（只有自测直接调 AddPressureForNode 才有数字）。
		_currentNodeKind = node?.Kind;
		ActRoguelike.Core.GameLog.Write("Spawn", $"第 {ChallengesUsed} 场 Encounter 开始（节点触发的战斗）");
		// 每一场新 Encounter 都要复位上一场的状态（IsCompleted / 波次 / 预算），
		// 否则第二次交互时 EncounterController 会以为自己已经打完、直接不刷怪。

		_encounter.ResetEncounter();

		// 波数按节点价值（关卡核心文档 L225-227；波数映射见 docs/stages.md 的「波数按价值标签」一节）。
		// 必须在 ResetEncounter 之后设置：Reset 会把 WaveCount 清掉。
		_encounter.WaveCount = node == null ? 0 : NodePlacement.WavesFor(node.Kind);

		// 白板首领的波次门槛（用户需求：只在第 8 波这类高波次出现）：把"本关之前已经开过的波数"
		// 交给这条 Encounter，它按 `WavesOpenedBefore + 本场已开波数` 判定。
		// 节点表给一场 Encounter 的波数只有 1～4，所以门槛必须按**本关累计**算，否则取 8 就永远到不了。
		_encounter.WavesOpenedBefore = _wavesOpenedInStage;
		_encounter.MinWaveForEliteBoss = Definition?.EliteBossMinWave ?? 0;
		_encounter.Start();
		NodeStarted?.Invoke(ChallengesUsed, _encounter);
	}

	/// <summary>
	/// 挑战完成后按节点/关卡配置累积 Pressure 并**立即抽取**数值强化（关卡核心文档 §7.2–§7.5）。
	///
	/// <b>§7.2「不同挑战类型具有各自的 Pressure 增加值」</b> → 基础点数按这个顺序取（只有一个真源）：
	/// ① <see cref="NodeConfig.BossPressurePoints"/>（节点表那一行 = 挑战类型，最具体）；
	/// ② Stage 行按价值层的 <see cref="StageDefinition.PressurePerNodeHighValue"/> /
	///    <see cref="StageDefinition.PressurePerNodeNormal"/>（§7.4 的两个"基础 Pressure"字段）；
	/// ③ <see cref="ConfigIndexData.PressurePerHighValueNode"/>（全局默认值 150）。
	///
	/// <b>组合顺序（§7.2「组合顺序应通过明确的计算配置定义」—— 全项目只有这一处定义）</b>：
	/// <c>本次点数 = 基础点数 × 本关增长倍率 × 挑战类型额外比例</c>。
	/// 倍率来自 Stage 行（§7.4：固定倍率表、不随机生成），额外比例来自
	/// <see cref="StageDefinition.PressureEvolutionBonus"/> / <see cref="StageDefinition.PressureMutationBonus"/>。
	///
	/// <b>§7.3 转换阈值</b>：入账后**逐次**检查阈值 —— 每满一次就**立即**抽一次随机强化（§7.3 第 3 条），
	/// 所以一次结算跨过多个阈值就是多次抽取（§7.3 第 6 条明令不能"只调用一次转换函数"）；
	/// 转换完不足阈值的余数清零、不结转（§7.3 第 5 条）。
	///
	/// <b>§7.5</b>：抽取走 <see cref="BossPerkCatalog.RollPressure"/>（只出**数值型** + 按 Weight 真加权 +
	/// 按标签过滤候选）。抽到的词条先**排队**，Boss 出生时才由 <see cref="ApplyPendingPressurePerks"/>
	/// 套到它身上 —— 挑战发生时 Boss 还不存在。行为/技能型走 <see cref="BossPerkCatalog.Roll"/>，
	/// 两套来源各有各的抽取点，不得合并成一个池（§7.1）。
	///
	/// <b>§8.2 免费挑战额度（第八章，本轮新接）</b>：**额度之内的挑战完成后不加 Pressure**。
	/// 结算顺序按文档：额度在 <see cref="StartEncounter"/> 里检查并扣除（§8.2 第 1–2 步），
	/// Pressure 在挑战**正常完成**时才增加（第 4–5 步，所以挑战没打完就不会加）；
	/// 这里的闸门读 <see cref="CurrentChallengeFree"/>：免费挑战直接返回、一点 Pressure 都不累积
	/// （含节点行配的 <c>BossModifierRolls</c> 额外抽取 —— 那也属于 Pressure 结算）。
	/// 见 docs/config.md「免费挑战额度（§8.2）」一节。
	/// </summary>
	/// <param name="kind">这次完成的是哪种奖励节点（决定基础点数、额外比例与候选标签）。</param>
	public void AddPressureForNode(RewardNodeKind kind)
	{
		// §8.2 闸门：免费挑战（在本关免费额度之内的那几次）完成后**不**增加 Pressure。
		// 额度已在 StartEncounter 里扣过，所以这里只看"这次挑战的序号是否还在额度内"。
		if (CurrentChallengeFree)
		{
			ActRoguelike.Core.GameLog.Write(
				"Node",
				$"Pressure 累积：跳过（§8.2 免费挑战）—— 挑战数 {ChallengesUsed} ≤ 本关免费额度 {FreeChallengeQuota}（剩余 {FreeQuotaLeft}）→ 本次不增加 Pressure、也不抽取数值强化");
			return;
		}

		ConfigIndexData config = ConfigIndex.Settings;
		StageDefinition? stage = Definition;
		NodeConfig? nodeRow = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);

		// ① 基础点数（§7.2：不同挑战类型各自的增加值）。
		(float basePoints, string source) = ResolveBasePressure(kind, nodeRow, stage, config);

		// ② 组合顺序：基础 × 本关倍率 × 类型比例。
		float growth = stage is { PressureGrowthMultiplier: > 0f } row ? row.PressureGrowthMultiplier : 1f;
		float scale = kind switch
		{
			RewardNodeKind.Evolution => Positive(stage?.PressureEvolutionBonus, config.PressureEvolutionBonus),
			RewardNodeKind.Mutation => Positive(stage?.PressureMutationBonus, config.PressureMutationBonus),
			_ => 1f,
		};
		float perNode = basePoints * growth * scale;
		float perPoint = ResolveThreshold(stage, config);

		// ③ §7.3：入账后**逐次**跨阈值 —— 每满一次就地抽一次，不能只转一次就丢掉多余的强化机会。
		int crossed = 0;
		var picked = new List<string>();
		if (perNode > 0f)
		{
			_pressureBank += perNode;
			while (_pressureBank >= perPoint)
			{
				_pressureBank -= perPoint;
				crossed++;
				AppendPick(BossPerkCatalog.RollPressure(PickTags(nodeRow)), picked);
			}

			// §7.3 第 5 条：转换后不足阈值的余数**清除、不结转**。
			if (crossed > 0)
			{
				_pressureBank = 0f;
			}

			_pressureRolls += crossed;
		}

		// ④ 节点表再加几次（§3 T11 的"抽几次"：与阈值无关，配置了就抽）。
		int extra = Mathf.Max(0, nodeRow?.BossModifierRolls ?? 0);
		for (int i = 0; i < extra; i++)
		{
			AppendPick(BossPerkCatalog.RollPressure(PickTags(nodeRow)), picked);
		}

		if (crossed == 0 && extra == 0 && perNode <= 0f)
		{
			return; // 这个节点本来就不加压力（与分表前的静默一致，不写日志）。
		}

		string extraText = extra > 0 ? $" + 节点行额外 {extra} 次" : string.Empty;
		string pickText = picked.Count == 0 ? "（池空，未兑现）" : string.Join("、", picked);
		ActRoguelike.Core.GameLog.Write(
			"Node",
			$"Pressure 累积：+{perNode:0.#}（基础 {basePoints:0.#}（来源：{source}）× 本关倍率 {growth:0.##} × 类型比例 {scale:0.##}）阈值 {perPoint:0} → 本次跨阈值 {crossed} 次{extraText} → 本关累计 {_pressureRolls} 次 / 余数 {_pressureBank:0.#} / 立即抽取 [{pickText}]");
	}

	/// <summary>把一次抽取结果登记进队列（池空时只警告，不静默变强）。</summary>
	private void AppendPick(BossPerk? perk, List<string> names)
	{
		if (perk == null)
		{
			GD.PushWarning("[StageController] Pressure 转换池是空的（候选 0 条），这次强化没有兑现。");
			return;
		}

		_pendingPressurePerks.Add(perk);
		names.Add(perk.Name);
	}

	/// <summary>
	/// 本次抽取允许的**数值型**候选标签（§7.4「强化池权重或适用规则」）：
	/// 节点行的 <see cref="NodeConfig.BossModifierTags"/> 优先（节点更具体）→ 关卡行的
	/// <see cref="StageDefinition.PressureUpgradeTags"/> → 两层都留空 = 不限制（本表全部行都是候选）。
	/// </summary>
	private string[] PickTags(NodeConfig? nodeRow)
	{
		if (nodeRow is { BossModifierTags.Length: > 0 })
		{
			return nodeRow.BossModifierTags;
		}

		return Definition is { PressureUpgradeTags.Length: > 0 } stage
			? stage.PressureUpgradeTags
			: System.Array.Empty<string>();
	}

	/// <summary>
	/// 基础 Pressure 的三层取值（§7.2 + §7.4）。节点行填了就用节点行（"挑战类型"最具体），
	/// 否则按价值层用 Stage 行的两个基础值，最后才用 ConfigIndex 的全局默认值。
	/// 返回的第二个值是**来源说明**，只用于日志留痕（回退纪律：来源必须能一眼看出来）。
	/// </summary>
	private static (float Points, string Source) ResolveBasePressure(
		RewardNodeKind kind,
		NodeConfig? nodeRow,
		StageDefinition? stage,
		ConfigIndexData config)
	{
		if (nodeRow is { BossPressurePoints: > 0f } row)
		{
			return (row.BossPressurePoints, $"节点表 {row.Id}");
		}

		if (LayerOf(kind) >= 2)
		{
			return stage is { PressurePerNodeHighValue: > 0f } high
				? (high.PressurePerNodeHighValue, $"关卡表 {high.Id}")
				: (config.PressurePerHighValueNode, "ConfigIndex 全局默认值");
		}

		return stage is { PressurePerNodeNormal: > 0f } normal
			? (normal.PressurePerNodeNormal, $"关卡表 {normal.Id}")
			: (0f, "未配置（= 这个价值层不加 Pressure）");
	}

	/// <summary>本关的 Pressure 阈值（§7.3）：行里填了用行里的，留 0 → 用 ConfigIndex 的全局默认值。</summary>
	private static float ResolveThreshold(StageDefinition? stage, ConfigIndexData config)
		=> Mathf.Max(1f, Positive(stage?.PressureThreshold, config.PressurePerPoint));

	/// <summary>
	/// Boss 出生 → 把本关攒下的 Pressure **数值强化**套到它身上。
	///
	/// 关卡核心文档 §7.1：两套来源可以在最终 Boss 初始化时**共同生效**，但不能合并成一个无法区分来源的池。
	/// 所以这里只套 Pressure 那一侧（数值型），Boss Build 的行为词条由 <c>EncounterController</c>
	/// 走 <see cref="BossPerkCatalog.Roll"/> 单独抽、单独套；日志里两条线分别留痕。
	///
	/// 用 <c>BossSpawned</c> 信号挂钩，而不是让 EncounterController 认识 Pressure ——
	/// 关卡的累积规则不渗进"一场遭遇"那一层。
	/// </summary>
	private void ApplyPendingPressurePerks()
	{
		if (_bossEncounter?.Boss is not { } boss)
		{
			return;
		}

		// 到这里（BossSpawned）这批数值强化就算"落地"了：HUD 与预览面板据此区分"已套用 / 待首领降临"。
		_pressureApplied = true;

		if (_pendingPressurePerks.Count == 0)
		{
			ActRoguelike.Core.GameLog.Write("Node", $"Boss 出生：本关 Pressure 数值强化 0 条（阈值转换 {_pressureRolls} 次）→ {Vitals(boss)} —— 与 Boss Build 行为词条分开结算（关卡核心文档 §7.1）");
			return;
		}

		BossPressure? pressure = boss.GetNodeOrNull<BossPressure>("Pressure");
		var names = new List<string>();
		foreach (BossPerk perk in _pendingPressurePerks)
		{
			perk.Apply(new BossPerkContext { Boss = boss, Source = this, Pressure = pressure });
			names.Add(perk.Name);
		}

		// 顺带把套用后的血量/护甲/乘区打出来 —— 这是"数值真的落到 Boss 身上"的直接证据
		// （只写"抽到了什么"证明不了生效）。
		ActRoguelike.Core.GameLog.Write(
			"Node",
			$"Boss 出生：套用本关 Pressure 数值强化 {names.Count} 条 [{string.Join("、", names)}]（阈值转换 {_pressureRolls} 次，来源：表 Pressure）→ {Vitals(boss)} —— 与 Boss Build 行为词条分开结算（关卡核心文档 §7.1）");
	}

	/// <summary>Boss 当前的有效数值（Pressure 套用前后的证据用）。</summary>
	private static string Vitals(Unit boss)
		=> $"Boss 生命 {boss.MaxHealth:0.#} 护甲 {boss.Armor:0.#} 伤害×{boss.Stats.Get("DamageMultiplier"):0.###} 攻速×{boss.Stats.Get("AttackSpeedMultiplier"):0.###} 移速×{boss.Stats.Get("MoveSpeedMultiplier"):0.###}";

	/// <summary>
	/// 节点表里配了 Boss Pressure 的行（§7.2「不同挑战类型具有各自的 Pressure 增加值」）。
	/// 这是**确定性**的一行日志，可以当回归指标逐字比对；同时也是回退留痕：
	/// 一行都没配时明确写出"全部走「关卡行 → ConfigIndex 全局默认值」回退"。
	/// </summary>
	private static string DescribeNodePressureRules()
	{
		List<string> ids = ConfigRows.Ids<NodeConfig>();
		ids.Sort(StringComparer.Ordinal);

		var configured = new List<string>();
		int total = 0;
		foreach (string id in ids)
		{
			if (ConfigRows.ById<NodeConfig>(id) is not { } row)
			{
				continue;
			}

			total++;
			if (row.BossPressurePoints <= 0f && row.BossModifierRolls <= 0 && row.BossModifierTags.Length == 0)
			{
				continue;
			}

			string rowTags = row.BossModifierTags.Length == 0 ? "不限制" : string.Join(",", row.BossModifierTags);
			configured.Add($"{row.Id} 基础 {row.BossPressurePoints:0.#} / 额外抽 {row.BossModifierRolls} 次 / 标签 [{rowTags}]");
		}

		return configured.Count == 0
			? $"节点表 {total} 行都没配 → 全部走「关卡行 → ConfigIndex 全局默认值」回退"
			: $"节点表 {total} 行里 {configured.Count} 行配了 → {string.Join(" · ", configured)}";
	}

	/// <summary>行里填了就用行里的，否则用全局默认值（0 / 负数 = 未设置）。</summary>
	private static float Positive(float? rowValue, float globalValue)
		=> rowValue is > 0f ? rowValue.Value : globalValue;

	/// <summary>仅供自测：按类型触发一次节点交互（走正常 RequestRewardNode 路径）。</summary>
	public bool DebugTriggerNode(RewardNodeKind kind)
	{
		foreach (Node child in GetChildren())
		{
			if (child is RewardNode node && node.Kind == kind && !node.Used)
			{
				node.Enabled = true;
				node.Visible = true;
				return RequestRewardNode(node);
			}
		}

		return false;
	}

	/// <summary>
	/// 仅供自测：关键状态快照。`all` = Interactable.All 里还剩几个可交互物（用掉的会退出这张表）。
	/// `pressure` = 本关跨过 Pressure 阈值的次数（每次转换都会立即抽一条数值强化）；
	/// `pmods` = 已经抽到的数值强化条数（含节点行额外次数；Boss 出生后即为已套用）；
	/// `free` = §8.2 的闸门当前是开还是关（true = 这次结算算免费挑战 → 不加 Pressure）。
	/// </summary>
	public string DebugState => $"pending={_rewardPending} running={_encounterRunning} waves={_encounter?.WaveCount} waveIndex={_encounter?.WaveIndex} remaining={_encounter?.WaveRemaining} alive={_encounter?.AliveCount} spawned={_encounter?.TotalSpawned} encRunning={_encounter?.IsRunning} encDone={_encounter?.IsCompleted} challenges={ChallengesUsed} freeQuota={FreeQuotaLeft} free={CurrentChallengeFree} node={_currentNodeKind?.ToString() ?? "-"} boss={BossOpened} pressure={_pressureRolls} pmods={_pendingPressurePerks.Count} bank={_pressureBank:0} victory={IsVictory} all={Interactable.All.Count}";

	/// <summary>
	/// **仅供自测**：把本关的免费挑战额度记账到用完（<c>ChallengesUsed = 额度 + 1</c>），
	/// 于是下一次结算按「超出免费额度」处理（§8.2 第 5 条）。不启动战斗、不生成敌人、不改任何数值。
	///
	/// 存在的理由：自测直接调 <see cref="AddPressureForNode"/>，那时挑战数还是 0 → 判定为**免费挑战**
	/// → 闸门会（正确地）把 Pressure 全部拦掉。要验证 §7.2–§7.5 的数值链路，必须先把额度真的用掉，
	/// 而不是把闸门关掉。返回剩余额度（期望 0）。
	/// </summary>
	public int DebugExhaustFreeQuota()
	{
		ChallengesUsed = Mathf.Max(ChallengesUsed, FreeChallengeQuota + 1);
		return FreeQuotaLeft;
	}

	/// <summary>仅供自测：直接开一场普通战斗（无 MCP 时用命令行验证波次）。</summary>
	public void DebugStartCombat(int waves = 0)
	{
		StartEncounter(null);
		if (waves > 0 && _encounter != null)
		{
			_encounter.WaveCount = waves;
		}
	}

	/// <summary>Boss Door：随时可开（策划文档 §6.2）。</summary>
	public bool OpenBossDoor()
	{
		if (IsVictory || BossOpened || _bossEncounter == null)
		{
			return false;
		}

		BossOpened = true;
		ActRoguelike.Core.GameLog.Write("Node", $"打开 Boss 门：已挑战 {ChallengesUsed} 次 / 隔离次数 {FreeChallengeQuota} → 初始压力 {Mathf.Max(0, ChallengesUsed - FreeChallengeQuota)}");
		_bossFight = true;

		// 玩家可以在普通战斗进行中直接开门（策划 §6.2）：立刻冻结普通 Encounter 的生成，
		// 否则清场后波次系统还会继续刷怪。
		_encounterRunning = false;
		_encounter?.StopSpawning();

		// 初始压力只由"超出免费挑战额度"这一项决定（关卡核心文档 §8.1/§8.2）。
		// ⚠️ 配置表阶段 6 之前这里还有一个 `+ _bossPressureBonus`（节点累积的 Pressure 被折算成压力等级）——
		// 那是 §7.5 转换池没落地时的**替身**。现在转换的结果是"从数值池里抽一条强化并套到 Boss 身上"
		// （§7.3 第 3 条），所以那一段被移除，不能两头都算。
		_bossEncounter.BossInitialPressure = Mathf.Max(0, ChallengesUsed - FreeChallengeQuota);
		_bossEncounter.SpawnBossNow();
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsVictory)
		{
			return;
		}

		ElapsedSeconds += (float)delta;

		// Encounter 清场 → 回到自由探索（不自动推进）。
		if (_encounterRunning && _encounter is { IsCleared: true })
		{
			_encounterRunning = false;

			// 白板首领门槛的账：本场开了几波就累加几波（含被门槛拦掉、没出白板首领的那几场）。
			_wavesOpenedInStage += _encounter.WaveIndex;
			if (_currentNodeKind.HasValue)
			{
				AddPressureForNode(_currentNodeKind.Value);
				_currentNodeKind = null;
			}

			NodeCleared?.Invoke(ChallengesUsed, _encounter);
		}

		// 区域杀改由 MapManager 创建的碰撞区域处理（进入即杀），不再每帧遍历单位组。

		// 完成判定改由 Stage 驱动（Encounter 自身生成循环里的返回路径会跳过它）。
		if (_encounterRunning && _encounter != null && _encounter.TryCompleteIfClear())
		{
			ActRoguelike.Core.GameLog.Write("Spawn", "Stage 驱动完成判定：本场已清空并结算");
		}

		if (_bossFight && !_bossCleared && _bossEncounter is { Boss: { } boss } && (!IsInstanceValid(boss) || boss.IsDead))
		{
			// Boss 阵亡：清空所有还在战斗的怪，并在 Boss 位置降临一个可交互节点，
			// 由玩家互动后才进入下一关（用户需求）。
			_bossCleared = true;
			Vector3 where = IsInstanceValid(boss) ? boss.GlobalPosition : GlobalPosition;
			ClearField();
			SpawnPortal(where);
		}

		// 关卡内的定期刷怪（需求 #6）：与节点的 Encounter 并行，互不干扰。
		UpdateAmbientSpawns((float)delta);
	}

	/// <summary>
	/// 关卡内的定期刷怪 + 每 <c>AmbientSpawnEscalateSeconds</c> 秒增强（用户需求 #6）。
	///
	/// 四条纪律：
	/// ① **只在"进了关卡"之后跑**（<see cref="Definition"/> 为空 = 还没开局，连计时都不累加）；
	/// ② **0 = 关掉**（<c>AmbientSpawnInterval &lt;= 0</c> → 整套机制不参与，行为与接它之前逐位一致）；
	/// ③ 落点**复用** <see cref="EncounterController.SpawnAmbient"/>（→ MapManager 的可行走点），
	///    所以是"任意有效位置"而不是随机丢在建筑里；
	/// ④ 游离怪**不进** Encounter 的预算/波次/清场判定（那边只认自己刷出来的那些），
	///    所以节点的挑战难度与完成条件一行都没变。
	/// </summary>
	private void UpdateAmbientSpawns(float dt)
	{
		ConfigIndexData config = ConfigIndex.Settings;
		if (config.AmbientSpawnInterval <= 0f || Definition == null || _encounter == null)
		{
			return;
		}

		_ambientElapsed += dt;

		// 强度档位：每过一个 AmbientSpawnEscalateSeconds 就 +1 档，**只影响之后刷出的怪**（已刷出的不动）。
		int steps = config.AmbientSpawnEscalateSeconds > 0f
			? (int)(_ambientElapsed / config.AmbientSpawnEscalateSeconds)
			: 0;
		if (steps > _ambientSteps)
		{
			_ambientSteps = steps;
			Core.GameLog.Write(
				"Spawn",
				$"定期刷怪·增强：本关已过 {_ambientElapsed:0.#}s → 强度档位 {_ambientSteps}（之后刷出的怪 生命/伤害 ×{1f + (config.AmbientSpawnStrengthStep * _ambientSteps):0.##}）");
		}

		_ambientTimer -= dt;
		if (_ambientTimer > 0f)
		{
			return;
		}

		_ambientTimer = config.AmbientSpawnInterval;

		PruneAmbient();
		if (config.AmbientSpawnMaxAlive > 0 && _ambientUnits.Count >= config.AmbientSpawnMaxAlive)
		{
			Core.GameLog.Write("Spawn", $"定期刷怪：跳过这一次（场上游离怪 {_ambientUnits.Count} 只已达上限 {config.AmbientSpawnMaxAlive}）");
			return;
		}

		Unit? spawned = SpawnAmbientNow();
		if (spawned == null)
		{
			Core.GameLog.Write("Spawn", "定期刷怪：这一次没刷出来（找不到玩家 / 本关没有可生成项），下个间隔再试");
		}
	}

	/// <summary>
	/// 立刻按**当前档位**刷一只游离怪，并登记进"场上游离怪"名单（返回 null = 这次没刷出来）。
	/// 自测也走这一个入口（<see cref="DebugSpawnAmbientNow"/>），保证"量到的"与"正式跑的"是同一条路。
	/// </summary>
	private Unit? SpawnAmbientNow()
	{
		float strength = 1f + (ConfigIndex.Settings.AmbientSpawnStrengthStep * _ambientSteps);
		Unit? spawned = _encounter?.SpawnAmbient(strength);
		if (spawned == null)
		{
			return null;
		}

		_ambientUnits.Add(spawned);
		Core.GameLog.Write(
			"Spawn",
			$"定期刷怪：{spawned.Name} 落在 {spawned.GlobalPosition}（强度档位 {_ambientSteps} → 生命/伤害 ×{strength:0.##}，场上游离怪 {_ambientUnits.Count} 只）");
		return spawned;
	}

	/// <summary>剪掉已死/已释放的游离怪（AliveCount 那种"缓存在读的时候重算"的同一套做法）。</summary>
	private void PruneAmbient()
	{
		for (int i = _ambientUnits.Count - 1; i >= 0; i--)
		{
			if (!IsInstanceValid(_ambientUnits[i]) || _ambientUnits[i].IsDead)
			{
				_ambientUnits.RemoveAt(i);
			}
		}
	}

	/// <summary>自测用：立刻按当前档位刷一只游离怪（与正式路径同一个入口）。</summary>
	public Unit? DebugSpawnAmbientNow() => SpawnAmbientNow();

	/// <summary>
	/// 自测用：把"本关已过秒数"往前拨 <paramref name="seconds"/> 秒并立刻结算档位 ——
	/// 用来在几秒内验证"每 120 秒增强一档"这条规则（不用真的等 120 秒）。
	/// 真实流程里秒数只会由 <see cref="UpdateAmbientSpawns"/> 按物理帧累加。
	/// </summary>
	public int DebugAdvanceAmbientClock(float seconds)
	{
		ConfigIndexData config = ConfigIndex.Settings;
		_ambientElapsed += seconds;
		if (config.AmbientSpawnEscalateSeconds > 0f)
		{
			_ambientSteps = (int)(_ambientElapsed / config.AmbientSpawnEscalateSeconds);
		}

		Core.GameLog.Write("Spawn", $"定期刷怪·自测：把本关时钟拨快 {seconds:0.#}s → 已过 {_ambientElapsed:0.#}s / 档位 {_ambientSteps}");
		return _ambientSteps;
	}

	/// <summary>
	/// Boss 死后降临的节点：纯关卡逻辑（进入下一关），不刷怪也不给奖励。
	///
	/// 降临点是 **Boss 之门消失后的位置**（用户要求）：门先收起，节点从高处落到门原来的位置上，
	/// 落地前不可交互（避免玩家在半空中按 F）；找不到门时退回"玩家周围"的可行走点（原逻辑，保留作兜底）。
	/// </summary>
	private void SpawnPortal(Vector3 where)
	{
		if (PortalScene == null)
		{
			// 没有配置降临节点时退化为直接完成本关。
			IsVictory = true;
			Victory?.Invoke();
			return;
		}

		BossDoor? door = FindBossDoor();
		Vector3 landing = door != null ? door.GlobalPosition : NearbyDropPoint(where);
		door?.Disappear();

		var portal = PortalScene.Instantiate<RewardNode>();
		portal.Kind = RewardNodeKind.Portal;
		portal.Prompt = "前往下一关";
		portal.SpawnsEncounter = false;
		portal.Enabled = false;   // 下落过程中不可交互（落地时 LandPortal 再打开）
		AddChild(portal);

		// 用寻路网格吸附到**可行走**的位置：纯随机点可能落在建筑里，玩家进不去就交互不了（用户指出）。
		portal.GlobalPosition = landing;
		NodePlacement.PlaceSafely(portal);
		landing = portal.GlobalPosition;

		// 从高处落至目标位置 + 轻微回弹，落地后才变成可交互。
		portal.GlobalPosition = landing + (Vector3.Up * PortalDropHeight);
		Tween drop = portal.CreateTween();
		drop.TweenProperty(portal, "global_position:y", landing.Y, PortalDropSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		drop.TweenProperty(portal, "global_position:y", landing.Y + PortalBounceHeight, PortalBounceSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		drop.TweenProperty(portal, "global_position:y", landing.Y, PortalBounceSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		drop.TweenCallback(Callable.From(() => LandPortal(portal)));

		string source = door != null ? $"Boss 之门 {door.GlobalPosition} 已收起" : "找不到 Boss 之门，退回玩家附近";
		ActRoguelike.Core.GameLog.Write(
			"Node",
			$"Boss 阵亡 → 清空场上敌人；{source}；降临节点「前往下一关」从 {PortalDropHeight:0.#} 米高落到 {landing}（下落中不可交互）");
	}

	/// <summary>降临物落地：这时才允许交互。</summary>
	private void LandPortal(RewardNode portal)
	{
		if (!IsInstanceValid(portal))
		{
			return;   // 下落途中切关了
		}

		portal.Enabled = true;
		portal.Appear();
		ActRoguelike.Core.GameLog.Write("Node", $"降临节点落地：{portal.GlobalPosition}（落地后可交互）");
	}

	/// <summary>
	/// 关卡里的 Boss 之门（降临点要用它消失后的位置）。
	/// 只在 Stage 的直接子节点里找：门的父节点就是 Stage（Main.tscn），
	/// 而且**消失过的门会退出 Interactable.All**，不能靠那张表反查。
	/// </summary>
	private BossDoor? FindBossDoor()
	{
		foreach (Node child in GetChildren())
		{
			if (child is BossDoor door)
			{
				return door;
			}
		}

		return null;
	}

	/// <summary>
	/// 兜底降临点（找不到 Boss 门时用）：玩家周围的可站立位置。
	/// 首领可能被打飞 / 冲出地图外，跟着它去捡节点会让玩家掉出地图摔死（用户实测）。
	/// </summary>
	private Vector3 NearbyDropPoint(Vector3 fallback)
	{
		Vector3 anchor = ActRoguelike.Core.PlayerLocator.Player is { } player ? player.GlobalPosition : fallback;
		float dropAngle = (float)GD.RandRange(0d, Mathf.Tau);
		float dropRadius = (float)GD.RandRange(2.2d, 3.2d);
		var around = new Vector3(
			anchor.X + (Mathf.Cos(dropAngle) * dropRadius),
			anchor.Y + 0.4f,
			anchor.Z + (Mathf.Sin(dropAngle) * dropRadius));

		// 用寻路网格吸附到**可行走**的位置：纯随机点可能落在建筑里，玩家进不去就交互不了（用户指出）。
		return ActRoguelike.World.MapManager.Instance?.DropPoint(anchor) ?? around;
	}

	/// <summary>保底：把指定类型里第一个还没启用的节点打开（强化池 / 材料节点用）。</summary>
	private void EnsureNodeEnabled(RewardNodeKind kind)
	{
		foreach (RewardNode node in _nodes)
		{
			if (node.Kind == kind && !node.Used)
			{
				node.Enabled = true;
				node.Visible = true;
				return;
			}
		}
	}
	/// <summary>按价值层启用节点（关卡核心文档 L87-93：高价值 2 / 中价值 5 / 低价值 3）。</summary>
	private void EnableByLayer(int high, int mid, int low)
	{
		foreach (RewardNode node in _nodes)
		{
			node.Enabled = false;
			node.Visible = false;
		}

		// 关卡核心文档 L106：节点之间要保持最小间距，跨层一起判定。
		var chosen = new List<RewardNode>();
		EnableLayer(layer: 2, count: high, chosen: chosen);
		EnableLayer(layer: 1, count: mid, chosen: chosen);
		EnableLayer(layer: 0, count: low, chosen: chosen);
	}

	/// <summary>某一价值层里随机挑 count 个启用。</summary>
	private void EnableLayer(int layer, int count, List<RewardNode> chosen)
	{
		var pool = new List<RewardNode>();
		foreach (RewardNode node in _nodes)
		{
			if (NodePlacement.LayerOf(node.Kind) == layer)
			{
				pool.Add(node);
			}
		}

		for (int i = pool.Count - 1; i > 0; i--)
		{
			int j = (int)(GD.Randi() % (uint)(i + 1));
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}

		// 先按最小间距挑，跨层累加已选节点；挑不够时再放宽补足并记录日志。
		float spacing = ActRoguelike.Config.ConfigIndex.Settings.MinNodeSpacing;
		int picked = 0;
		foreach (RewardNode candidate in pool)
		{
			if (picked >= count)
			{
				break;
			}

			bool tooClose = false;
			foreach (RewardNode already in chosen)
			{
				if (candidate.GlobalPosition.DistanceTo(already.GlobalPosition) < spacing)
				{
					tooClose = true;
					break;
				}
			}

			if (tooClose)
			{
				continue;
			}

			chosen.Add(candidate);
			candidate.Enabled = true;
			candidate.Visible = true;
			picked++;
		}

		if (picked < count)
		{
			foreach (RewardNode candidate in pool)
			{
				if (picked >= count)
				{
					break;
				}

				if (chosen.Contains(candidate))
				{
					continue;
				}

				chosen.Add(candidate);
				candidate.Enabled = true;
				candidate.Visible = true;
				picked++;
			}

			ActRoguelike.Core.GameLog.Write("Node", $"第 {layer} 层因最小间距 {spacing:0.#} 不足，已放宽补足到 {picked}/{count}");
		}
	}

	/// <summary>某价值层的节点会开几波（关卡核心文档 L225-227：高价值 3 / 中价值 2 / 低价值 1）。
	/// 与 <see cref="NodePlacement.WavesFor"/> 是同一份实现（都读节点表），这里保留只是为了不破坏既有调用点。</summary>
	public static int WavesFor(RewardNodeKind kind) => NodePlacement.WavesFor(kind);

	/// <summary>节点价值层：2 高（进化/异化）、1 中（强化/随机奖励/商店）、0 低（局外资源/回复/特殊）。</summary>
	/// <summary>节点价值层。配置表阶段 2 起统一走节点表（原来这里与 `NodePlacement` 各写了一份重复实现）。</summary>
	private static int LayerOf(RewardNodeKind kind) => NodePlacement.LayerOf(kind);
}