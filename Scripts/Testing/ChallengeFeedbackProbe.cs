using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Visual;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实生成、F输入及物理移动驱动挑战反馈；不模拟完成标记、不写真实存档。</summary>
public partial class ChallengeFeedbackProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private StageController _stage = null!;
	private EncounterController _encounter = null!;
	private InteractionComponent _other = null!;
	private ChallengerMark _mark = null!;
	private ChallengeStatusPanel _panel = null!;
	private int _completed, _cancelled, _triggered;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[ChallengeFeedback] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async Task Frames(int count = 8)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private async Task TapF()
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = true });
		await Frames(3);
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = false });
		await Frames(3);
	}
	private void MovePlayer(Vector3 point)
	{
		_player.GlobalPosition = point; _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
	}
	private Unit[] Enemies() => _encounter.GetChildren().OfType<Unit>().Where(u => !u.IsDead).ToArray();
	private void KillEnemies() { foreach (Unit enemy in Enemies()) enemy.ForceKill(_player); }
	private async Task Shot(string state)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/j48_challenge_{state}.png")) == Error.Ok, $"实际渲染截图 {state}");
	}
	public override async void _Ready()
	{
		float ambient = ConfigIndex.Settings.AmbientSpawnInterval;
		try
		{
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			ConfigIndex.Settings.AmbientSpawnInterval = 0;
			await Build(); await FeedbackAndClear(); await WaveProgression(); await CancellationAndRestart();
		}
		catch (Exception error) { Check(false, error.ToString()); }
		finally
		{
			GetTree().Paused = false;
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = false });
			ConfigIndex.Settings.AmbientSpawnInterval = ambient;
		}
		GD.Print($"[ChallengeFeedback] 挑战状态与拒绝反馈验收结束：通过 {_pass}，失败 {_fail}");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Build()
	{
		for (int i = 0; i < 60 && ConfigRows.Count<EnemyConfig>() == 0; i++) await Frames(1);
		Check(ConfigRows.Count<EnemyConfig>() > 0, "先等待配置索引就绪，再应用测试关卡白名单");
		var floor = new StaticBody3D { Name = "Ground", Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		floor.AddToGroup("nav_geometry");
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(48, 1, 48) } });
		floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(48, 1, 48) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.17f, .23f, .25f) } }); AddChild(floor);
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -25, 0), LightEnergy = 1.3f, ShadowEnabled = true });
		AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.08f, .12f, .18f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(.65f, .7f, .8f), AmbientLightEnergy = .6f } });
		var map = new MapManager { Name = "MapManager", BakeDelaySeconds = .1f }; AddChild(map);
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		_player.Position = new Vector3(-8, .05f, 0); AddChild(_player); _player.IsInvulnerable = true;
		_mark = _player.GetNode<ChallengerMark>("ChallengerMark");
		var hud = RuntimeResources.Load<PackedScene>("res://Scenes/UI/Hud.tscn").Instantiate<PlayerHud>(); AddChild(hud);
		_panel = hud.GetNode<ChallengeStatusPanel>("Root/ChallengeStatusPanel");
		var enemy = new Unit { Name = "FeedbackEnemy", Faction = Faction.Enemy, MaxHealth = 100 };
		enemy.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .35f, Height = 1.8f } });
		enemy.AddChild(new MeshInstance3D { Position = Vector3.Up * .9f, Mesh = new CapsuleMesh { Radius = .35f, Height = 1.8f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.8f, .25f, .3f) } });
		foreach (Node child in enemy.GetChildren()) child.Owner = enemy;
		var packed = new PackedScene(); Check(packed.Pack(enemy) == Error.Ok, "无AI测试敌人使用真实Unit预制体"); enemy.Free();
		_stage = new StageController { Name = "Stage" };
		_encounter = new EncounterController { Name = "Combat", AutoStart = false, WaveMode = false, BudgetCap = 4, BudgetRegenPerSecond = 10, SpawnInterval = .05f, MinSpawnDistance = 4, SpawnRadius = 8 };
		_encounter.AddChild(new EnemySpawnEntry { Scene = packed, Cost = 2, Weight = 1 }); _stage.AddChild(_encounter);
		for (int i = 0; i < 10; i++)
		{
			var host = new Node3D { Name = "Reward" + i, Position = new Vector3((i % 5) * 5 - 10, 0, 12 + (i / 5) * 5) };
			host.AddChild(new InteractionComponent { Kind = i < 2 ? InteractionKind.Evolution : i < 7 ? InteractionKind.Enhancement : InteractionKind.Heal }); _stage.AddChild(host);
		}
		var otherHost = new Node3D { Name = "BlockedMechanism", Position = new Vector3(-6, 0, 0) };
		_other = new InteractionComponent { Kind = InteractionKind.Signal, UseMode = InteractionUseMode.Once, FailureKnockback = 9, FailureWaveRadius = 3.2f, FailureWaveSeconds = .7f, Prompt = "测试机关" };
		_other.Triggered += () => _triggered++; otherHost.AddChild(_other); _stage.AddChild(otherHost);
		_stage.ChallengeFinished += completed => { if (completed) _completed++; else _cancelled++; };
		AddChild(_stage);
		// 关卡会自动补齐生产敌种；白名单只留无表行的夹具预制体，确保预算和数量可重复。
		_stage.ApplyStage(new StageDefinition { Id = "feedback_probe", DisplayName = "挑战反馈", CombatBudget = 4, MaxConcurrent = 2, SpawnInterval = .05f, AllowedEnemyIds = new[] { "feedback_fixture" } }, 1);
		for (int i = 0; i < 120 && !map.IsReady; i++) await Frames(1);
		await Frames(); Check(map.IsReady, "真实静态地面完成导航烘焙及同步");
		Check(!_stage.ChallengerActive && !_mark.Active && !_panel.Visible, "探索时无挑战粒子或挑战面板");
		Check(_stage.RemainingNodes == 10, "反馈夹具保留10个普通节点，机关不占配额");
		Check(_encounter.EntryCount == 1, "测试白名单排除自动补齐的生产敌种，只生成固定成本夹具");
	}
	private async Task Start(int waves = 0)
	{
		_stage.DebugStartCombat(waves);
		for (int i = 0; i < 180 && _encounter.TotalSpawned < 2; i++) await Frames(1);
		await Frames(5);
		Check(_stage.ChallengerActive && ReferenceEquals(_stage.ActiveChallengeEncounter, _encounter), "真实Stage开始当前挑战并暴露同一Encounter");
	}
	private async Task FeedbackAndClear()
	{
		await Start();
		Check(_encounter.TotalSpawned == 2 && _encounter.AliveCount == 2 && _encounter.RemainingBudget == 0, "预算花完后真实生成2个活着的敌人");
		Check(_mark.Active && _mark.EntryCount == 1 && _mark.Particles is { Emitting: true, Visible: true }, "挑战者入态后身上持续发射粒子");
		Check(_panel.Active && _panel.Visible && _panel.StatusText.Contains("生成预算 0") && _panel.StatusText.Contains("本场存活 2"), "右上面板读真实预算和本场存活，预算耗尽不退态");
		Check(_panel.MouseFilter == Control.MouseFilterEnum.Ignore && !GetTree().Paused, "挑战面板不抢输入或暂停游戏");
		MovePlayer(new Vector3(-6, .05f, 1.5f)); await Frames(14); await Shot("active");
		var feedback = _other.Backend!.GetNode<InteractablePresentation>(InteractablePresentation.NodeName);
		Check(feedback.FailKnockback == 9 && feedback.FailureWaveRadius == 3.2f && feedback.FailureWaveSeconds == .7f, "类型组件三个失败参数真正传给后端反馈");
		Vector3 before = _player.GlobalPosition; int nodes = _stage.RemainingNodes;
		await TapF();
		Check(feedback.FailCount == 1 && feedback.FailureWaveCount == 1 && feedback.LastFailureSoundPlayed, "真实F拒绝时播放一次音效及光波");
		Check(feedback.LastFailureWave is { DrawDome: true, Radius: 3.2f } wave && wave.Tint.R > .9f && wave.Tint.G < .2f, "失败生成红色半球光波罩，使用组件设定范围");
		Check(!_other.Backend.Used && _triggered == 0 && _stage.RemainingNodes == nodes, "失败不消耗目标、不发成功信号、不改配额");
		await Shot("rejected"); await Frames(15);
		Check(_player.GlobalPosition.Z > before.Z + .3f, "拒绝冲量经真实Movement将角色向物体外弹开");
		Check(_stage.ChallengerActive && _mark.CompletionCount == 0 && _completed == 0, "其他物体拒绝不终止当前挑战或播放完成");
		Check(_other.Backend.CurrentPrompt.Contains("挑战中"), "交互提示明确说明挑战中不可使用");
		var wall = new StaticBody3D { Position = new Vector3(-6, 1, 2.5f), CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 2, .5f) } }); AddChild(wall);
		MovePlayer(new Vector3(-6, .05f, 1.5f)); await Frames(15); await TapF(); await Frames(25);
		Check(feedback.FailCount == 2 && _player.GlobalPosition.Z < 2.1f && _player.GlobalPosition.Z > 1.5f, "靠墙拒绝仍有反馈，弹开经碰撞停止且不穿墙");
		wall.QueueFree();
		Enemies()[0].ForceKill(_player); await Frames(10);
		Check(_encounter.AliveCount == 1 && _stage.ChallengerActive && _completed == 0, "还剩1只敌人时仍锁定其他交互");
		GetTree().Paused = true; KillEnemies();
		await ToSignal(GetTree().CreateTimer(.15, true), SceneTreeTimer.SignalName.Timeout);
		Check(_stage.ChallengerActive && _completed == 0, "暂停期间不会因清场缓存误报完成");
		GetTree().Paused = false;
		for (int i = 0; i < 60 && _stage.ChallengerActive; i++) await Frames(1);
		await Frames(2);
		Check(!_stage.ChallengerActive && _stage.ActiveChallengeEncounter == null && _completed == 1, "真实清完当前Encounter后只发一次完成并解除挑战");
		Check(!_mark.Active && _mark.Particles is { Emitting: false, Visible: false } && _mark.CompletionCount == 1, "完成时消除身上粒子并触发一次完成表现");
		Check(_panel.CompletionCount == 1 && _panel.Visible && _panel.StatusText.Contains("挑战完成"), "完成后的右上提示明确显示挑战完成");
		await Shot("cleared");
		MovePlayer(new Vector3(-6, .05f, 1.5f)); await Frames(); await TapF();
		Check(_other.Backend.Used && _triggered == 1, "解锁后同一机关通过真实F正常成功");
	}
	private async Task WaveProgression()
	{
		var waves = new EncounterController { Name = "WaveProgression", AutoStart = false, WaveMode = true, WaveCount = 2, WaveMinEnemies = 2, WaveMaxEnemies = 2, NextWavePressureThreshold = .1f, TotalBudget = 8, BudgetCap = 8, MinSpawnDistance = 4, SpawnRadius = 8 };
		waves.AddChild(new EnemySpawnEntry { Scene = _encounter.GetChildren().OfType<EnemySpawnEntry>().First().Scene, Cost = 2, Weight = 1 }); AddChild(waves);
		int finished = 0; waves.Finished += success => { if (success) finished++; }; waves.Start();
		for (int i = 0; i < 180 && waves.TotalSpawned < 2; i++) await Frames(1);
		Check(waves.WaveIndex == 1 && waves.AliveCount == 2 && waves.RemainingBudget == 4, "限定两波的第一波真实出场，仍留后续预算");
		foreach (Unit enemy in waves.GetChildren().OfType<Unit>()) enemy.ForceKill(_player);
		Check(!waves.TryCompleteIfClear() && !waves.IsCompleted && finished == 0, "波间空场不能提前结算后续尚未生成的波次");
		for (int i = 0; i < 180 && waves.TotalSpawned < 4; i++) await Frames(1);
		Check(waves.WaveIndex == 2 && waves.TotalSpawned == 4 && waves.AliveCount == 2 && finished == 0, "第一波全灭后继续生成第二波，不在空场停住");
		foreach (Unit enemy in waves.GetChildren().OfType<Unit>().Where(u => !u.IsDead)) enemy.ForceKill(_player);
		await Frames(); Check(waves.IsCleared && finished == 1, "两波全部清完后只结算一次成功");
		waves.QueueFree(); await Frames(2);
	}
	private async Task CancellationAndRestart()
	{
		_other.Backend!.ResetNode();
		await Start();
		await Frames(30);
		Check(_mark.Active && _mark.Particles?.Emitting == true && _mark.EntryCount == 2 && _mark.GetNode<MeshInstance3D>("Mark").Visible, "连续开战取消旧淡出回调，挑战标记及粒子继续可见");
		_encounter.FailEncounter(); await Frames();
		Check(!_stage.ChallengerActive && !_mark.Active && !_panel.Active && _mark.CompletionCount == 1 && _completed == 1 && _cancelled == 1, "Encounter失败只取消状态，不冒充完成");
		_encounter.FailEncounter(); await Frames(2); Check(_cancelled == 1, "重复失败不重复发结束事件");
		KillEnemies(); await Start(); _encounter.StopSpawning(); await Frames();
		Check(!_stage.ChallengerActive && _mark.CompletionCount == 1 && _completed == 1 && _cancelled == 2, "强制停止生成不被误认作真实清场");
		KillEnemies(); await Start(); _encounter.ResetEncounter(); await Frames();
		Check(!_stage.ChallengerActive && _completed == 1 && _cancelled == 3, "Encounter复位立即撤销挑战，不播放绿波");
		KillEnemies(); await Start();
		_stage.ApplyStage(new StageDefinition { Id = "feedback_reset", CombatBudget = 4, MaxConcurrent = 2, SpawnInterval = .05f, AllowedEnemyIds = new[] { "feedback_fixture" } }, 2); await Frames();
		Check(!_stage.ChallengerActive && !_mark.Active && !_panel.Visible && _completed == 1 && _cancelled == 4, "正式换关消除当前挑战且没有伪完成或面板残留");
		await Start(); _player.IsInvulnerable = false; _player.ForceKill(); await Frames();
		Check(!_stage.ChallengerActive && !_mark.Active && _mark.Particles?.Emitting == false && !_panel.Visible && _completed == 1, "玩家死亡清除粒子和挑战UI，不庆祝完成");
		Check(GetTree().GetNodesInGroup("enemy_units").All(u => u is Unit), "光波不注册为敌人或参与清场计数");
	}
}
