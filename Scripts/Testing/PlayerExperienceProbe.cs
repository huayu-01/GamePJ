using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实基地界面→加载→正式森林→奖励／挑战／首领／换关；测试击杀仅用于流程推进，不代表人工试玩。</summary>
public partial class PlayerExperienceProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[PlayerExperience] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async Task Wait(double seconds = .35) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task KeyPress(Key key)
	{
		await UiProbeWait.Interactive(this);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
		await Wait(.05);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await Wait(.3);
		string batch = OS.GetCmdlineUserArgs().Contains("--challenge-feedback") ? "j48"
			: OS.GetCmdlineUserArgs().Contains("--interaction-models") ? "j47"
			: OS.GetCmdlineUserArgs().Contains("--interaction-components") ? "j46"
			: OS.GetCmdlineUserArgs().Contains("--flow-robustness") ? "j45" : "j44";
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/{batch}_{name}.png")) == Error.Ok, $"图形截图 {name}");
	}
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
#if DEBUG
		try
		{
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
			AbilityStore.DebugBeginSandbox();
			RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm; RunSetup.HasSelection = true;
			await Wait(.05); Reparent(GetTree().Root);
			var fixture = new Node3D { Name = "PlayerExperienceFixture" };
			GetTree().Root.AddChild(fixture); GetTree().CurrentScene = fixture;
			GetTree().CurrentScene.AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate());
			await Wait(1.2);
			BaseGuide guide = GetTree().CurrentScene.FindChild("BaseGuide", true, false) as BaseGuide ?? throw new Exception("缺少基地指引");
			Check(guide.GuideText.Contains("可直接出击"), "指引可跳过，不锁定玩家移动或出击");
			Unit player = PlayerLocator.Player!;
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, PhysicalKeycode = Key.W, Pressed = true });
			await Wait(.65);
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, PhysicalKeycode = Key.W, Pressed = false });
			await Wait(); Check(guide.MovementObserved, "真实移动输入推进练习观察");
			PracticeTarget practice = GetTree().CurrentScene.FindChild("PracticeTarget", true, false) as PracticeTarget ?? throw new Exception("缺少木桩");
			practice.Target!.ApplyDirectDamage(60, player); await Wait(.2);
			Check(guide.HitObserved && practice.HitCount > 0, "真实伤害结算驱动命中指引，基地不发关卡奖励");
			await Shot("base_guide");
			ExpeditionScreen screen = UiService.Instance!.Screen<ExpeditionScreen>()!;
			bool departed = false;
			Check(screen.Open(() => departed = true) && GetTree().Paused, "出击摘要走统一弹层并暂停");
			screen.Depart(); Check(!departed, "开启保护期不会误出击");
			Check(screen.SummaryText.Contains("猎手") && screen.SummaryText.Contains("能量掌") && screen.SummaryText.Contains("先选择奖励"), "摘要共读真实配装并说明先奖励后挑战");
			await Wait(); await Shot("expedition_summary");
			await KeyPress(Key.Escape); await Wait();
			Check(!screen.IsOpen && !departed && !GetTree().Paused, "返回调整恢复基地，不启动加载");
			BaseTerminal gate = Interactable.All.OfType<BaseTerminal>().Single(t => t.Kind == BaseTerminal.TerminalKind.Gate && (t.Destination?.ScenePath ?? t.TargetScene) == "res://Scenes/Expedition.tscn");
			Check(gate.TargetScene == "res://Scenes/Expedition.tscn", "正式出击门指向森林，Main留作回归入口");
			Check(screen.Open(() => departed = true), "工具摘要仍可独立打开，用于顶层路由回归"); await Wait();
			SettingsScreen settings = UiService.Instance.Screen<SettingsScreen>()!;
			UiRouter router = UiRouter.Instance!;
			await KeyPress(settings.ToggleKey); await Wait();
			Check(settings.IsOpen && screen.IsOpen && router.IsTopScreen(settings) && router.StackDepth == 2,
				"真实设置快捷键覆盖出击摘要，路由仅认顶层设置");
			await Shot("summary_settings");
			await KeyPress(Key.Enter); await Wait();
			Check(settings.IsOpen && screen.IsOpen && GetTree().CurrentScene == fixture && GetTree().Paused,
				"设置覆盖时真实Enter不会确认底层出击或启动加载");
			screen.Depart(); screen.Close();
			Check(settings.IsOpen && screen.IsOpen && GetTree().CurrentScene == fixture,
				"底层出击与返回按钮共用顶层闸门，程序回调也不能穿过覆盖层");
			settings.CloseOnCancel = false;
			await KeyPress(Key.Escape); await Wait();
			Check(settings.IsOpen && screen.IsOpen && router.IsTopScreen(settings) && GetTree().Paused,
				"顶层不允许取消时，Esc被顶层消费，不会向下关闭出击摘要");
			settings.CloseOnCancel = true;
			await KeyPress(Key.Escape); await Wait();
			Check(!settings.IsOpen && screen.IsOpen && router.IsTopScreen(screen) && GetTree().Paused,
				"真实Esc只关闭顶层设置，摘要仍开且游戏仍暂停");
			screen.Close(); await Wait(.2);
			player.GlobalPosition = gate.GlobalPosition + Vector3.Back; player.ResetPhysicsInterpolation(); await Wait();
			await KeyPress(Key.F);
			Check(!screen.IsOpen && AdventureCheckpoint.IsActive,"真实F入口直接出击、不打开摘要且已建立取消回退点");
			for (int i = 0; i < 100 && GetTree().CurrentScene is not ForestExpedition { EntryReady: true }; i++) await Wait(.1);
			ForestExpedition forest = GetTree().CurrentScene as ForestExpedition ?? throw new Exception("没有进入正式森林");
			Check(forest.EntryReady, "正式森林导航和交互点准备完成");
			await Wait(.4);
			StageController stage = StageController.Instance!; RunController run = RunController.Instance!;
			player = PlayerLocator.Player!;
			Check(stage.RemainingNodes == 10 && stage.FreeChallengeQuota == ConfigIndex.Settings.FreeChallengeBaseQuota, "森林使用固定10节点和唯一全局额度来源");
			RewardNode[] nodes = stage.RewardNodes.Where(n => n.Enabled).ToArray();
			foreach (RewardNode node in nodes)
				Check(MapManager.Instance!.TryPath(player.GlobalPosition, node.GlobalPosition, out _, player), $"节点可达 {node.Name}");
			Check(nodes.All(n => n.GetNodeOrNull<InteractablePresentation>(InteractablePresentation.NodeName) != null), "正式节点保留描边和交互反馈组件");
			RewardNode enhance = nodes.First(n => n.Kind == RewardNodeKind.Enhancement);
			Check(NodeChallengeInfo.Describe(enhance, stage).Contains("免费"), "互动前说明挑战波数和本次免费");
			player.GlobalPosition = enhance.GlobalPosition + Vector3.Back; player.ResetPhysicsInterpolation(); await Wait();
			await Shot("forest_node");
			int acquired = run.Build.Acquired.Count;
			await KeyPress(Key.F); await Wait();
			Check(UiService.Instance.Screen<RewardScreen>()?.IsOpen == true && stage.ChallengesUsed == 0, "实际节点先打开奖励，选择前不启动挑战");
			await Shot("forest_reward");
			await KeyPress(Key.Key1); await Wait(.25);
			Check(run.Build.Acquired.Count == acquired + 1 && stage.ChallengesUsed == 1 && stage.ChallengerActive, "真实数字键领取增幅后开始挑战");
			Check(NodeChallengeInfo.Describe(nodes.First(n => n != enhance), stage).Contains("超额"), "额度消耗后代价提示随真实状态更新");
			await Shot("forest_combat");
			for (int i = 0; i < 150 && stage.ChallengerActive; i++)
			{
				foreach (Unit enemy in GetTree().GetNodesInGroup("enemy_units").OfType<Unit>().ToArray()) if (!enemy.IsDead) enemy.ForceKill(player);
				await Wait(.05);
			}
			Check(!stage.ChallengerActive, "测试击杀清场后回到探索，无自动推进关卡");
			Check(stage.OpenBossDoor(), "森林接受首领挑战");
			EncounterController bossFight = stage.GetChildren().OfType<EncounterController>().Single(e => e.IsBossNode);
			for (int i = 0; i < 50 && bossFight.Boss == null; i++) await Wait(.1);
			Check(bossFight.Boss != null && MapManager.Instance!.TryPath(player.GlobalPosition, bossFight.Boss.GlobalPosition, out _, bossFight.Boss), "首领使用与玩家连通的合法出生点");
			bossFight.Boss!.ForceKill(player); await Wait(1.3);
			Check(stage.AwaitingExit && stage.AmbientAliveCount == 0, "首领结束后清理威胁并进入稳定离场");
			float frozen = stage.ElapsedSeconds; await Wait(.2);
			Check(Mathf.IsEqualApprox(frozen, stage.ElapsedSeconds), "离场等待不计入有效游玩时间");
			RewardNode portal = Interactable.InSubtree(stage).OfType<RewardNode>().Single(n => n.Kind == RewardNodeKind.Portal);
			player.GlobalPosition = portal.GlobalPosition + Vector3.Back; await Wait(); await KeyPress(Key.F); await Wait();
			Check(stage.StageNumber == 2 && run.StageStatistics.Count == 1, "实际离场交互推进下一关并保留上一关快照");
			var low = new EnemySpawnEntry { Weight = 1 }; var high = new EnemySpawnEntry { Weight = 9 }; var zero = new EnemySpawnEntry { Weight = 0 };
			GD.Seed(44); int highCount = 0, zeroCount = 0;
			for (int i = 0; i < 1000; i++) { var pick = EncounterController.PickWeighted(new[] { low, high, zero }); if (pick == high) highCount++; if (pick == zero) zeroCount++; }
			Check(highCount > 830 && highCount < 970 && zeroCount == 0, $"波次加权抽样1:9，1000次高权重={highCount}，零权重={zeroCount}");
			low.Free(); high.Free(); zero.Free();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { AbilityStore.DebugEndSandbox(); }
#endif
		GetTree().Paused = false;
		GD.Print($"[PlayerExperience] === 玩家流程验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}
