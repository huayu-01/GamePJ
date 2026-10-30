using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实 Stage→Run 事件、跨关统计、首领离场边界及死亡确认；收益使用进程内存，不写用户存档。</summary>
public partial class RunFlowContinuityProbe : Node3D
{
	private const string ReturnFixture = "res://Scenes/Debug/_RunFlowReturnFixture.tscn";
	private int _pass;
	private int _fail;
	private Node3D _world = null!;
	private StageController _stage = null!;
	private EncounterController _encounter = null!;
	private EncounterController _bossEncounter = null!;
	private RunController _run = null!;
	private Unit _player = null!;
	private StageDefinition[] _rows = Array.Empty<StageDefinition>();

	private void Check(bool condition, string message)
	{
		if (condition) _pass++; else _fail++;
		GD.Print($"[RunFlowContinuity] {(condition ? "PASS" : "FAIL")} {message}");
	}

	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Wait(float seconds)
	{
		float elapsed = 0f;
		while (elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}

	private static void SetField(object target, string name, object value)
		=> target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
	private static void CallPrivate(object target, string name, params object[] values)
		=> target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, values);

	private void BuildWorld()
	{
		_world = new Node3D { Name = "FlowWorld" };
		_world.AddToGroup("nav_geometry");
		GetTree().CurrentScene.AddChild(_world);
		var ground = new StaticBody3D { Position = new Vector3(0, -.5f, 0), CollisionLayer = CombatLayers.World };
		ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(400, 1, 400) } });
		_world.AddChild(ground);
		_world.AddChild(new ActRoguelike.World.MapManager { Name = "MapManager", BakeDelaySeconds = .05f });
		_player = new Unit { Faction = Faction.Player, MaxHealth = 100f, Name = "Player" };
		_world.AddChild(_player);
		_player.ProcessMode = ProcessModeEnum.Disabled;
		var bossTemplate = new Unit { Faction = Faction.Enemy, Name = "FlowBoss", Tags = new[] { "Boss" } };
		var bossScene = new PackedScene();
		Check(bossScene.Pack(bossTemplate) == Error.Ok, "建立最小首领预制体，后续走真实 SpawnBossNow");
		bossTemplate.Free();
		_stage = new StageController { Name = "Stage", PortalScene = RuntimeResources.Load<PackedScene>("res://Scenes/Interactables/RewardNode.tscn") };
		_encounter = new EncounterController { Name = "Encounter", AutoStart = false, WaveMode = false };
		_bossEncounter = new EncounterController { Name = "BossEncounter", AutoStart = false, BossScene = bossScene };
		_stage.AddChild(_encounter);
		_stage.AddChild(_bossEncounter);
		for (int i = 0; i < 10; i++)
		{
			_stage.AddChild(new RewardNode
			{
				Name = $"Reward{i}", Kind = i < 2 ? RewardNodeKind.Evolution : i < 7 ? RewardNodeKind.Enhancement : RewardNodeKind.Resource,
				Position = new Vector3(i * 10f, 0f, 10f),
			});
		}
		_world.AddChild(_stage);
		_stage.SetPhysicsProcess(false);
		_encounter.SetPhysicsProcess(false);
		_bossEncounter.SetPhysicsProcess(false);
		_rows = Enumerable.Range(1, 3).Select(i => new StageDefinition
		{
			Id = $"flow_probe_{i}", DisplayName = $"验证关卡 {i}", CombatBudget = 0f, BossPerkCount = 0, BossGuardCount = 0,
		}).ToArray();
		BuildRun();
		_stage.ApplyStage(_rows[0], 1);
	}

	private void BuildRun()
	{
		_run = new RunController { Name = "RunController" };
		_world.AddChild(_run);
		_run.SetProcess(false);
		// 仅隔离开局配置与界面；关卡完成仍通过生产 Victory 事件驱动真实 RunController。
		SetField(_run, "_stages", _rows);
		SetField(_run, "_player", _player);
	}

	private void StartChallenges(int count)
	{
		for (int i = 0; i < count; i++)
		{
			_stage.DebugStartCombat();
			_encounter.StopSpawning();
			_stage._PhysicsProcess(0d);
		}
		Check(_stage.ChallengesUsed == count, $"第 {_stage.StageNumber} 关通过真实 StartEncounter 记录 {count} 次挑战");
	}

	private async Task<RewardNode> DefeatBoss()
	{
		Check(_stage.OpenBossDoor(), "首领门接受开战请求");
		RewardNode ordinary = _stage.GetChildren().OfType<RewardNode>().First(n => n.Kind != RewardNodeKind.Portal);
		Check(!ordinary.Enabled, "开首领门即关闭剩余普通节点");
		ordinary.Enabled = true;
		Check(!_stage.RequestRewardNode(ordinary), "即使旧引用重新启用，首领战期间也不能领取普通奖励");
		Check(_bossEncounter.Boss is { IsDead: false }, "真实首领实例已生成且存活");
		_bossEncounter.Boss!.ForceKill();
		_stage._PhysicsProcess(0d);
		Check(_stage.AwaitingExit && !_stage.ChallengerActive && !_stage.IsVictory, "首领死亡解除挑战态，等待离场且尚未提前切关");
		Check(_stage.CompletedStatistics is { BossDefeated: true }, "首领死亡即冻结本关统计");
		StageStatistics frozen = _stage.CaptureStatistics();
		string ambient = _stage.AmbientSummary;
		_stage._PhysicsProcess(300d);
		Check(_stage.CaptureStatistics() == frozen, "首领后的离场等待不计入有效用时，统计不漂移");
		Check(_stage.AmbientSummary == ambient && _stage.DebugSpawnAmbientNow() == null, "首领结束后游离刷怪计时与生成入口均停止");
		Check(!_stage.RequestRewardNode(ordinary), "首领结束后不能重开普通节点");
		int challenges = _stage.ChallengesUsed;
		_stage.DebugStartCombat();
		Check(_stage.ChallengesUsed == challenges && !_encounter.IsRunning, "底层开战入口也不能在离场阶段重启挑战");
		RewardNode portal = _stage.GetChildren().OfType<RewardNode>().First(n => n.Kind == RewardNodeKind.Portal && !n.IsQueuedForDeletion());
		Check(!portal.Enabled && !_stage.RequestRewardNode(portal), "降临物下落期间仍不可交互");
		await Wait(1.2f);
		Check(portal.Enabled && _stage.RequestRewardNode(ordinary) == false, "降临物落地后可用，普通节点仍被拒绝");
		return portal;
	}

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
#if !DEBUG
		GD.PushWarning("[RunFlowContinuity] 此专项只在 DEBUG 内存存档隔离下执行。");
		GameExit.Request(GetTree(), 2);
		return;
#else
		try
		{
			MetaProgress.UseInMemoryStoreForSelfTest();
			await Frame();
			// 探针在场景切换后继续取证，实际受测节点仍属于当前场景，会被引擎正常回收。
			Reparent(GetTree().Root);
			var fixture = new Node3D { Name = "RunFlowFixture" };
			GetTree().Root.AddChild(fixture); GetTree().CurrentScene = fixture;
			BuildWorld();
			await Wait(.6f);
			Check(_stage.RemainingNodes == 10, "关卡固定启用 10 个节点，不再经过被覆盖的随机数量开关");
			_stage.SetPhysicsProcess(true);
			GetTree().Paused = true;
			await Wait(.1f);
			Check(_stage.ElapsedSeconds == 0f, "场景树暂停时真实物理帧不会累加有效用时");
			_stage.SetPhysicsProcess(false);
			GetTree().Paused = false;
			var earlyPortal = new RewardNode { Kind = RewardNodeKind.Portal };
			Check(!_stage.RequestRewardNode(earlyPortal), "首领未结束时拒绝伪造的传送节点请求");
			earlyPortal.Free();
			for (int index = 0; index < 3; index++)
			{
				StartChallenges(index + 1);
				_stage._PhysicsProcess(10d * (index + 1));
				Check(Mathf.IsEqualApprox(_stage.ElapsedSeconds, 10f * (index + 1)), "本关有效时间来自真实 Stage 物理计时");
				_player.ApplyDirectDamage(10f);
				RewardNode portal = await DefeatBoss();
				if (index == 2) Check(portal.Prompt.Contains("返回基地") && !portal.Prompt.Contains("下一关"), "最终关离场提示为完成出征并返回基地");
				int before = MetaProgress.Shards;
				Check(_stage.RequestRewardNode(portal), "真实传送请求通过 Victory 事件交给 RunController");
				Check(_run.StageStatistics.Count == index + 1, "每关快照恰好累计一次");
				if (index < 2)
				{
					Check(!_run.RunEnded && _stage.StageNumber == index + 2 && _stage.ChallengesUsed == 0, "换关只清本关计数，整局继续");
					Check(Mathf.IsEqualApprox(_player.Health, _player.MaxHealth), "切关实际回满生命，与玩家提示一致");
					Check(_run.TotalChallengesStarted == (index + 1) * (index + 2) / 2, "之前关卡的挑战次数保留到整局统计");
				}
				else Check(MetaProgress.Shards - before == RunSettlement.ShardsFor(2, 6, true), "最终结算包含全部 6 次挑战收益");
			}
			Check(_run.RunEnded && Mathf.IsEqualApprox(_run.TotalElapsedSeconds, 60f) && _run.TotalChallengesStarted == 6, "三关结束保留 60 秒有效用时和 6 次节点挑战");
			int settled = MetaProgress.Shards;
			CallPrivate(_run, "EndRun", true);
			CallPrivate(_run, "OnVictory");
			Check(MetaProgress.Shards == settled && _run.StageStatistics.Count == 3, "重复胜利或结束通知不再发碎片、不重复快照");
			Check(RunSettlement.Describe(true, "", "", 0, 0, 60f, 6, 1).Contains("1 关全部完成"), "结算读取实际关卡数，不硬编码三关");

			_world.RemoveChild(_run);
			_run.QueueFree();
			_stage.ApplyStage(_rows[0], 1);
			BuildRun();
			StartChallenges(2);
			_stage._PhysicsProcess(12d);
			var death = RuntimeResources.Load<PackedScene>("res://Scenes/UI/DeathScreen.tscn").Instantiate<DeathScreen>();
			death.Name = "DeathScreen";
			GetTree().CurrentScene.AddChild(death);
			death.SetProcess(false);
			Check(death.TargetScene == "res://Scenes/Levels/Base.tscn", "生产死亡默认返回真实基地");
			death.TargetScene = ReturnFixture;
			_player.ForceKill();
			_run._Process(0d);
			Check(_run.RunEnded && _run.TotalChallengesStarted == 2 && Mathf.IsEqualApprox(_run.TotalElapsedSeconds, 12f), "真实玩家死亡进入结算并冻结当前关统计");
			Check(death.PhaseName == "death" && !GetTree().Paused, "死亡动画期间世界保持运行");
			death._Process(death.DeathAnimationSeconds + .1d);
			Check(death.PhaseName == "panel" && GetTree().Paused, "动画结束后进入暂停的过渡面板");
			death._Process(death.HoldSeconds + .1d);
			Check(death.AwaitingConfirmation && GetTree().Paused, "过渡完成后等待确认，不自动离场");
			death._Process(120d);
			Check(death.AwaitingConfirmation && IsInstanceValid(_world), "结算可持续阅读，超过旧倒计时仍不关闭");
			// 再验证跳过演出只进入结算，最终确认才走同一条返回路径。
			var skip = RuntimeResources.Load<PackedScene>("res://Scenes/UI/DeathScreen.tscn").Instantiate<DeathScreen>();
			_world.AddChild(skip);
			skip.SetProcess(false);
			skip.BeginDeath("跳过演出验证", "测试结算");
			Check(skip.ContinueDeathFlow() && skip.AwaitingConfirmation, "跳过死亡演出只进入结算，不直接返回");
			skip.GetParent().RemoveChild(skip);
			skip.QueueFree();
			RunSetup.HasSelection = true;
			while (death.SummaryInputBlocked) await Frame();
			Check(death.ContinueDeathFlow(), "玩家确认后接受返回请求");
			await Frame(); await Frame();
			Check(GetTree().CurrentScene?.SceneFilePath == ReturnFixture && !GetTree().Paused && !RunSetup.HasSelection,
				"确认后真实切换场景，解除暂停并清除本局选择标记");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[RunFlowContinuity] === 整局与离场验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
#endif
	}
}
