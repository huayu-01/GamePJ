using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实模式、导航、死亡掉落、选择暂停、信标、首领、撤离与出击回退。</summary>
public partial class SurvivalModeProbe : Node
{
	private int _pass, _fail;
	private readonly List<object> _checks = new();
	private SurvivalDirector _director = null!;
	private RunController _run = null!;
	private Node3D _mode = null!;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; _checks.Add(new { passed = ok, message = text }); GD.Print($"[Survival] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);
	private async Task Until(Func<bool> condition, double limit, string message, bool acceptPendingRewards = false)
	{
		for (int i = 0; i < limit * 20; i++)
		{
			if (condition()) return;
			// 等待后续战斗时，击杀掉落可能又触发升级暂停，必须先完成真实选择再继续计时。
			if (acceptPendingRewards && Screen.IsOpen) await DrainRewards();
			await Wait(.05);
		}
		throw new InvalidOperationException($"等待超时：{message}；paused={GetTree().Paused} state={_director?.State} elapsed={_director?.ElapsedSeconds:0.###} alive={_director?.AliveCount} pending={_director?.PendingSpawns} rewards={_director?.PendingRewards}");
	}
	private async Task TapKey(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); await Wait(.03);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
	}
	private async Task InteractInput()
	{
		await TapKey(Key.F);
	}
	private RewardScreen Screen => _mode.GetNode<RewardScreen>("RunController/RewardScreen");
	private async Task Choose()
	{
		await Until(() => Screen.IsOpen && !Screen.InputBlocked && UiRouter.Instance?.IsTopScreen(Screen) == true, 3, "奖励界面保护期结束");
		Check(Screen.Options.Count > 0 && Screen.Options.All(x => _run.AvailableRewards().Any(r => r.Id == x.Id)), "升级候选来自当前角色、武器及前置规则");
		string id = Screen.Options[0].Id; int before = _run.Build.StacksOf(id);
		Button card = Screen.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().First(b => b.Visible);
		card.EmitSignal(Button.SignalName.Pressed); card.EmitSignal(Button.SignalName.Pressed);
		Check(_run.Build.StacksOf(id) == before + 1, "同一奖励按钮重复触发只获取一次");
		await Wait(.08);
	}
	private async Task DrainRewards()
	{
		for (int i = 0; i < 30 && (_director.PendingRewards > 0 || Screen.IsOpen); i++) await Choose();
		Check(_director.PendingRewards == 0 && !Screen.IsOpen, "连升与目标奖励依次领取，队列清空后继续战斗");
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var image = GetViewport().GetTexture().GetImage();
		Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/survival/{name}.png")) == Error.Ok, "真实渲染截图 " + name);
	}
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm; RunSetup.HasSelection = true; RuneCarry.Set(string.Empty);
			_mode = GetParent().GetNode<Node3D>("Mode"); _director = _mode.GetNode<SurvivalDirector>("Director"); _run = _mode.GetNode<RunController>("RunController");
			var original = RuntimeResources.Load<SurvivalDefinition>("res://Config/Modes/twilight_survival.tres");
			Check(!original.Validate().Any() && original.DurationSeconds == 720 && original.Phases.Length == 6, "生产配置：12分钟、六阶段、首领和敌人引用有效");
			var bad = (SurvivalDefinition)original.Duplicate(true); bad.Phases[1].StartFraction = 0; bad.BaseLevelExperience = 0;
			Check(bad.Validate().Count() >= 2 && !original.Validate().Any(), "拒绝倒序阶段与零经验门槛，配置副本彼此隔离");
			bad.MilestoneFractions = new[] { .5f, .25f }; bad.BeaconPositions[0] = new Vector3(float.NaN, 0, 0); bad.Phases[0].MaxAlivePressure = .01f;
			Check(bad.Validate().Count() >= 5, "拒绝倒序强化节点、非有限信标坐标和无法容纳敌人的阶段压力");
			var settings = (SurvivalDefinition)original.Duplicate(true); settings.DurationSeconds = 12; settings.SpawnWarningSeconds = .12f; settings.SpawnInterval = .16f; settings.ExperiencePerPressure = 24; settings.BeaconCaptureSeconds = .25f;
			EnemyConfig chaser = settings.Phases[0].Enemies.First(e => e.Id == "enemy_chaser");
			foreach (var phase in settings.Phases) { phase.Enemies = new[] { chaser }; phase.BudgetPerSecond = 20; phase.MaxAlive = 4; phase.MaxAlivePressure = 16; phase.EliteChance = 0; }
			settings.MilestoneEnemy = chaser; settings.MilestoneFractions = new[] { .25f };
			_director.Definition = settings;
			await Until(() => _run.GameplayReady && _director.State == SurvivalDirector.PhaseState.Surviving, 8, "正式配装与导航就绪");
			_director.Player!.IsInvulnerable = true;
			Check(MapManager.Instance is { IsReady: true, PolygonCount: > 0 }, "独立庭院真实烘焙导航网格");
			Check(_run.Mode == _director && StageController.Instance == null, "整局服务绑定独立模式，不误挂旧冒险阶段");
			Check(RunController.CharacterRow(PlayerCharacterKind.Hunter)!.VisualProfile!.ResourcePath.EndsWith("clover_wayfarer.tres"), "角色A正式配置已使用三叶旅童档案");
			Check(_director.Objectives.Count == 3 && _director.Objectives.All(o => MapManager.Instance!.IsWalkable(o.GlobalPosition)), "三处信标实际可达");
			Check(_run.CanCancelAdventure && AdventureCheckpoint.IsActive, "新模式开局已有整局回退点");
			await Shot("play");
			Vector3 entryPosition = _director.Player.GlobalPosition;
			_director.Player.GlobalPosition = new Vector3(45, 0, 39); _director.Player.ResetPhysicsInterpolation(); await Wait(.1); await Shot("edge");
			_director.Player.GlobalPosition = entryPosition; _director.Player.ResetPhysicsInterpolation();
			var overview = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 125, Position = new Vector3(65, 75, 80) };
			_mode.AddChild(overview); overview.LookAt(Vector3.Zero); overview.MakeCurrent(); await Wait(.08); await Shot("overview"); overview.QueueFree();
			NodeSearch.FindInSubtree<Camera3D>(_director.Player!)?.MakeCurrent();
			var field = new ExperienceField { Capacity = 2, Player = _director.Player, PickupRadius = .1f }; AddChild(field);
			field.Drop(new Vector3(40, 0, 40), 3); field.Drop(new Vector3(41, 0, 40), 4); field.Drop(new Vector3(42, 0, 40), 5);
			Check(field.Count == 2 && field.StoredExperience == 12, "经验达到显示上限时合并数值，累计经验不丢失"); field.QueueFree();
			await Until(() => _director.TotalSpawned > 0, 5, "真实普通敌人生成");
			Check(_director.AliveCount <= 4 && _director.AlivePressure <= 16 && _director.PendingSpawns <= 4, "生成同时受人数、压力与待生成上限约束");
			var enemy = GetTree().GetNodesInGroup("enemy_units").OfType<Unit>().First(u => _mode.IsAncestorOf(u) && !u.IsDead);
			Vector3 deathPosition = enemy.GlobalPosition; int kills = _director.TotalKills;
			enemy.ForceKill(_director.Player); enemy.ForceKill(_director.Player);
			Check(_director.TotalKills == kills + 1 && _director.ExperienceField!.StoredExperience > 0, "真实死亡掉落经验，重复死亡不重复计数");
			_director.Player.GlobalPosition = deathPosition; _director.Player.ResetPhysicsInterpolation();
			await Until(() => _director.Level > 1 && Screen.IsOpen, 4, "拾取经验后实际升级");
			float paused = _director.ElapsedSeconds; await Wait(.3);
			Check(GetTree().Paused && Mathf.IsEqualApprox(_director.ElapsedSeconds, paused), "升级选择暂停战场与生存计时"); await Shot("level_up");
			await DrainRewards();
			await Until(() => _director.ElapsedSeconds >= settings.DurationSeconds * .25f, 5, "强化敌人时间点");
			// 持续清普通敌人，包含已经预警但尚未落地的一批；只清一次会被迟到生成占满四人夹具。
			await Until(() =>
			{
				if (_director.LivingEnemies.Any(u => u.GetChildren().OfType<EliteModifier>().Any())) return true;
				foreach (var room in _director.LivingEnemies) room.ForceKill(_director.Player);
				return false;
			}, 3, "阶段强化敌人", acceptPendingRewards: true);
			Unit elite = GetTree().GetNodesInGroup("enemy_units").OfType<Unit>().First(u => _mode.IsAncestorOf(u) && !u.IsDead && u.GetChildren().OfType<EliteModifier>().Any());
			Check(elite.Armor > 0, "阶段强化敌人沿用正式护甲与精英表现"); elite.ForceKill(_director.Player); await Wait(.08);
			var relic = _director.Objectives.First(o => o.Kind == SurvivalObjective.ObjectiveKind.Relic);
			_director.Player.GlobalPosition = relic.GlobalPosition; _director.Player.ResetPhysicsInterpolation(); await Wait(.12);
			// 同位置经验可能先触发升级暂停；完成选择再验证战斗中的真实交互键。
			await DrainRewards();
			await Until(() => PlayerInteractInput.Current == relic, 2, "044遗物走近后能被真实输入选择");
			Check(PlayerInteractInput.Prompt.Contains("选择1项增幅") && !PlayerInteractInput.Prompt.Contains("死战专属") && relic.GetNode<InteractablePresentation>("Presentation").Highlighted, "044遗物显示收益说明、F键与目标描边");
			await InteractInput();
			Check(relic.Used && !_director.ClaimRelic(relic), "044真实F领取强化敌人遗物，重复请求不再发奖");
			await DrainRewards();
			foreach (var beacon in _director.Objectives.Where(o => o.Kind == SurvivalObjective.ObjectiveKind.Beacon).ToArray())
			{
				_director.Player.GlobalPosition = beacon.GlobalPosition; _director.Player.ResetPhysicsInterpolation(); await Wait(.1);
				await Until(() => PlayerInteractInput.Current == beacon, 2, "044信标走近后能被真实输入选择");
				Check(PlayerInteractInput.Prompt.Contains("恢复15%"), "044信标提示说明净化方式、收益和首领削弱");
				await InteractInput();
				Check(beacon.Channeling && beacon.ProgressVisible, "044真实F启动信标，显示净化进度");
				// 途中拾取经验可能先弹升级；处理正式奖励后继续等待信标，不能把正常暂停当净化失败。
				for(int step=0;step<120 && !beacon.Completed;step++)
				{
					if(Screen.IsOpen || _director.PendingRewards>0) await DrainRewards(); else await Wait(.05);
				}
				Check(beacon.Completed && !_director.CompleteBeacon(beacon), "信标按实际停留完成一次，重复完成不发奖");
				await DrainRewards();
			}
			Check(_director.CompletedBeacons == 3, "三处可选目标全部完成，终局首领削弱条件成立");
			for (int i = 0; i < 500 && _director.Boss == null; i++) { if (Screen.IsOpen || _director.PendingRewards > 0) await DrainRewards(); else await Wait(.05); }
			Check(_director.State == SurvivalDirector.PhaseState.FinalBoss && _director.Boss != null, "生存时长结束后实际生成终局首领");
			if (_director.Boss == null) throw new InvalidOperationException("终局首领未生成");
			await Shot("boss"); _director.Boss.ForceKill(_director.Player); await Wait(.15);
			Check(_director.State == SurvivalDirector.PhaseState.AwaitingExit && _director.BossDefeated && !_run.RunEnded, "击败首领后等待主动撤离，不提前结算");
			Check(_director.AliveCount == 0 && _director.PendingSpawns == 0, "首领结束撤销敌人、召唤增援与待生成预告");
			var exit = _director.Objectives.Single(o => o.Kind == SurvivalObjective.ObjectiveKind.Exit);
			_director.Player.GlobalPosition = exit.GlobalPosition; _director.Player.ResetPhysicsInterpolation(); await Wait(.15);
			Reparent(GetTree().Root); // 验证工作节点独立存活，才能继续检查真实切场景。
			await Until(() => PlayerInteractInput.Current == exit, 2, "044撤离门走近后能被真实输入选择");
			await InteractInput();
			Check(_run.RunEnded, "044真实F启动出口共享单次结算");
			int earned = _run.SettlementEarnedShards; Check(!_run.CompleteMode(_director, true) && _run.SettlementEarnedShards == earned && _run.StageStatistics.Count == 1, "重复完成不会重复入账，模式统计只记录一次");
			await Until(() => Screen.IsOpen && !Screen.InputBlocked, 3, "结算确认保护期结束"); await TapKey(Key.Enter);
			await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Levels/Base.tscn" && PlayerLocator.Player != null, 8, "胜利确认返回真实基地"); await Wait(.3);
			Node home = GetTree().CurrentScene; var gate = home.GetNode<InteractionComponent>("SurvivalGate/InteractionComponent");
			Check(gate.Backend is BaseTerminal { Destination.ScenePath: "res://Scenes/Survival.tscn" }, "基地新增独立入口指向生存场景");
			var brief = UiService.Instance!.Screen<ExpeditionScreen>()!;
			brief.OpenDestination(()=>{},((BaseTerminal)gate.Backend!).Destination); await Until(() => !brief.InputBlocked,3,"手动查看模式说明");
			Check(brief.SummaryText.Contains("死战") && brief.SummaryText.Contains("12") && brief.SummaryText.Contains("经验"), "出击摘要显示实际模式、时长和成长机制");
			brief.Close(); await Wait(.12); Check(GetTree().CurrentScene == home && !AdventureCheckpoint.IsActive, "返回调整不出征也不建立回退点");
			Unit basePlayer = PlayerLocator.Player!; basePlayer.GlobalPosition = gate.GlobalPosition + new Vector3(0, 0, 1); basePlayer.ResetPhysicsInterpolation(); await Wait(.15); await InteractInput();
			Check(!brief.IsOpen && AdventureCheckpoint.IsActive,"生存入口F直接加载并捕获回退点，无中间选择");
			await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Survival.tscn" && RunController.Instance?.GameplayReady == true, 10, "从新入口实际进入模式");
			_run = RunController.Instance!; _director = (SurvivalDirector)_run.Mode!; _mode = (Node3D)GetTree().CurrentScene;
			await Until(() => _director.State == SurvivalDirector.PhaseState.Surviving, 6, "二次入场导航"); _director.Player!.IsInvulnerable = true;
			await Wait(.3); await Shot("production");
			int shards = MetaProgress.Shards; _run.GrantReward("upg_power"); MetaProgress.AddShards(13);
			Check(_run.CanCancelAdventure && MetaProgress.Shards == shards + 13, "正常入口的试炼仍支持本局构筑与消耗回退");
			SurvivalDirector cancelled = _director; Check(_run.CancelAdventure(out string error), "取消生存试炼：" + error);
			await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Levels/Base.tscn", 8, "取消后返回基地"); await Wait(.15);
			Check(MetaProgress.Shards == shards && !AdventureCheckpoint.IsActive, "取消还原出击前资源，不进行试炼结算");
			Check(cancelled.State == SurvivalDirector.PhaseState.Stopped && cancelled.AliveCount == 0 && cancelled.PendingSpawns == 0 && cancelled.PendingRewards == 0, "取消与退树停止生成、经验和迟到奖励回调");
			Node restoredHome = GetTree().CurrentScene;
			var secondGate = restoredHome.GetNode<InteractionComponent>("SurvivalGate/InteractionComponent");
			Unit restoredPlayer = PlayerLocator.Player!; restoredPlayer.GlobalPosition = secondGate.GlobalPosition + Vector3.Back; restoredPlayer.ResetPhysicsInterpolation(); await Wait(.15); await InteractInput();
			await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Survival.tscn" && RunController.Instance?.GameplayReady == true, 10, "死亡回归入场");
			var dyingRun = RunController.Instance!; var dyingMode = (SurvivalDirector)dyingRun.Mode!;
			await Until(() => dyingMode.State == SurvivalDirector.PhaseState.Surviving, 5, "死亡回归导航");
			dyingMode.Player!.ForceKill();
			await Until(() => dyingRun.RunEnded && dyingMode.State == SurvivalDirector.PhaseState.Stopped, 4, "死亡停止模式");
			Check(!dyingRun.CanCancelAdventure && dyingRun.StageStatistics.Count == 1 && dyingMode.AliveCount == 0, "死亡只结算一次并关闭生成、奖励与取消入口");
			var death = GetTree().CurrentScene.GetNode<DeathScreen>("DeathScreen");
			await Until(() => death.IsPlaying, 5, "正式死亡界面"); await Shot("death");
			if(!death.AwaitingConfirmation) death.ContinueDeathFlow();
			await Until(()=>death.AwaitingConfirmation && !death.SummaryInputBlocked,3,"结算确认保护期结束"); death.ContinueDeathFlow();
			await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Levels/Base.tscn", 8, "死亡确认返回基地");
			Check(true, "实际死亡演出与结算确认返回基地");
			GC.Collect(); GC.WaitForPendingFinalizers(); Check(true, "模式往返后完整GC与终结器正常完成");
		}
		catch (Exception error) { Check(false, error.ToString()); }
		GetTree().Paused = false;
		string output = ValidationArtifacts.Path("res://Logs/evidence/survival/validation" + (DisplayServer.GetName() == "headless" ? "-headless" : "-render") + ".json");
		System.IO.File.WriteAllText(output, JsonSerializer.Serialize(new { passed = _fail == 0, pass = _pass, fail = _fail, checks = _checks }, new JsonSerializerOptions { WriteIndented = true }));
		GD.Print($"[Survival] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}
