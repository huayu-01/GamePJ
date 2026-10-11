using System;
using System.Collections.Generic;
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
	/// </summary>
	public bool ChallengerActive => _encounterRunning || (_bossFight && !IsVictory);
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
	private float _pressureBank;
	private int _bossPressureBonus;
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
		_bossPressureBonus = 0;

		// 免费挑战额度：初始 1 次、每关 +1、上限 2（关卡核心文档 L405-416）。
		// 注意只统计"会刷怪的节点"，StartEncounter 才会让 ChallengesUsed 递增。
		FreeChallengeQuota = Mathf.Min(
			ActRoguelike.Config.Balance.Settings.FreeChallengeMaxQuota,
			ActRoguelike.Config.Balance.Settings.FreeChallengeBaseQuota + (StageNumber - 1));

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
		PlaceNodes();

		// 关卡核心文档 L87-93：每个 Stage 固定 10 个节点 —— 高价值 2 / 中价值 5 / 低价值 3。
		EnableByLayer(high: 2, mid: 5, low: 3);
	}

	public override void _ExitTree()
	{
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
		ActRoguelike.Core.GameLog.Write("Spawn", $"第 {ChallengesUsed} 场 Encounter 开始（节点触发的战斗）");
		// 每一场新 Encounter 都要复位上一场的状态（IsCompleted / 波次 / 预算），
		// 否则第二次交互时 EncounterController 会以为自己已经打完、直接不刷怪。

		_encounter.ResetEncounter();

		// 波数按节点价值（关卡核心文档 L225-227）：高价值 3 波 / 中价值 2 波 / 低价值 1 波。
		// 必须在 ResetEncounter 之后设置：Reset 会把 WaveCount 清掉。
		_encounter.WaveCount = node == null ? 0 : NodePlacement.WavesFor(node.Kind);
		_encounter.Start();
		NodeStarted?.Invoke(ChallengesUsed, _encounter);
	}

	/// <summary>
	/// 挑战完成后按节点价值累积 Pressure（关卡核心文档 L352/L370）：
	/// 高价值（进化/异化）每次 +150；每满 100 转成 1 点首领压力，**余数清零**。
	/// </summary>
	public void AddPressureForNode(RewardNodeKind kind)
	{
		if (LayerOf(kind) < 2)
		{
			return;
		}

		// 关卡核心文档 L356：进化/异化奖励对 Pressure 增加值有额外比例。
		float scale = kind switch
		{
			RewardNodeKind.Evolution => ActRoguelike.Config.Balance.Settings.PressureEvolutionBonus,
			RewardNodeKind.Mutation => ActRoguelike.Config.Balance.Settings.PressureMutationBonus,
			_ => 1f,
		};
		float perNode = ActRoguelike.Config.Balance.Settings.PressurePerHighValueNode * scale;
		float perPoint = Mathf.Max(1f, ActRoguelike.Config.Balance.Settings.PressurePerPoint);
		_pressureBank += perNode;
		int gained = (int)(_pressureBank / perPoint);
		if (gained > 0)
		{
			_bossPressureBonus += gained;
			_pressureBank = 0f;
		}

		ActRoguelike.Core.GameLog.Write("Node", $"Pressure 累积：+{perNode:0} → 余数 {_pressureBank:0} / 本关首领额外压力 {_bossPressureBonus}");
	}

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

	/// <summary>仅供自测：关键状态快照。`all` = Interactable.All 里还剩几个可交互物（用掉的会退出这张表）。</summary>
	public string DebugState => $"pending={_rewardPending} running={_encounterRunning} waves={_encounter?.WaveCount} waveIndex={_encounter?.WaveIndex} remaining={_encounter?.WaveRemaining} alive={_encounter?.AliveCount} spawned={_encounter?.TotalSpawned} encRunning={_encounter?.IsRunning} encDone={_encounter?.IsCompleted} challenges={ChallengesUsed} freeQuota={FreeQuotaLeft} boss={BossOpened} pressure={_bossPressureBonus} bank={_pressureBank:0} victory={IsVictory} all={Interactable.All.Count}";

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
		_bossEncounter.BossInitialPressure = Mathf.Max(0, ChallengesUsed - FreeChallengeQuota) + _bossPressureBonus;
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
		float spacing = ActRoguelike.Config.Balance.Settings.MinNodeSpacing;
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

	/// <summary>某价值层的节点会开几波（关卡核心文档 L225-227：高价值 3 / 中价值 2 / 低价值 1）。</summary>
	public static int WavesFor(RewardNodeKind kind) => LayerOf(kind) switch { 2 => 3, 1 => 2, _ => 1 };

	/// <summary>节点价值层：2 高（进化/异化）、1 中（强化/随机奖励/商店）、0 低（局外资源/回复/特殊）。</summary>
	private static int LayerOf(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Evolution or RewardNodeKind.Mutation => 2,
		RewardNodeKind.Enhancement or RewardNodeKind.Shop => 1,
		_ => 0,
	};
}