using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.World;
using Godot;
using Disk = System.IO.File;

namespace ActRoguelike.Testing;

/// <summary>只允许在独立用户目录运行；真实出击、暂停确认、四种存档回退及模态中的取消。</summary>
public partial class AdventureCancelProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[AdventureCancel] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async Task Wait(double seconds = .4)
	{
		ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
		while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private async Task Key(Key key)
	{
		await UiProbeWait.Interactive(this);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); await Wait(.04);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Wait();
	}
	private async Task Click(Button button)
	{
		await UiProbeWait.Interactive(this);
		if (DisplayServer.GetName() == "headless") button.EmitSignal(Button.SignalName.Pressed);
		else
		{
			Vector2 point = button.GetGlobalRect().GetCenter();
			Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Wait(.03);
			Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
			Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false });
		}
		await Wait();
	}
	private Dictionary<string, byte[]?> Files() => new[] { "meta.cfg", "ability_cards.cfg", "rune.cfg", "loadout.cfg" }
		.ToDictionary(name => name, name => Disk.Exists(Path(name)) ? Disk.ReadAllBytes(Path(name)) : null);
	private static string Path(string name) => ProjectSettings.GlobalizePath("user://" + name);
	private static bool Equal(byte[]? a, byte[]? b) => a == null ? b == null : b != null && a.SequenceEqual(b);
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		// 本探针有意验证真实落盘，不允许误用玩家目录。
		if (!OS.GetUserDataDir().Replace('\\', '/').Contains("/CodexValidation/"))
		{
			Check(false, "必须使用独立CodexValidation用户目录"); GameExit.Request(GetTree(), 1); return;
		}
		var original = Files(); byte[]? settings = Disk.Exists(Path("settings.cfg")) ? Disk.ReadAllBytes(Path("settings.cfg")) : null;
		try
		{
			await Wait(.1); Reparent(GetTree().Root);
			var fixture = new Node3D { Name = "AdventureFixture" }; GetTree().Root.AddChild(fixture); GetTree().CurrentScene = fixture;
			fixture.AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate()); await Wait(1);
			await CancelCase(false); await CancelCase(true); await CancelCase(false, true);
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally
		{
			AdventureCheckpoint.Complete();
			// 独立测试目录发生失败也恢复输入夹具，保留错误日志。
			foreach (var pair in original) Restore(Path(pair.Key), pair.Value);
			Restore(Path("settings.cfg"), settings);
			GD.Print($"[AdventureCancel] SUMMARY pass={_pass} fail={_fail}");
			GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
		}
	}
	private static void Restore(string path, byte[]? bytes) { if (bytes == null) Disk.Delete(path); else Disk.WriteAllBytes(path, bytes); }
	private async Task CancelCase(bool overReward, bool survival = false)
	{
		Unit player = PlayerLocator.Player!;
		string destination = survival ? "res://Scenes/Survival.tscn" : "res://Scenes/Expedition.tscn";
		var gate = Interactable.All.OfType<BaseTerminal>().Single(t => t.Kind == BaseTerminal.TerminalKind.Gate && (t.Destination?.ScenePath ?? t.TargetScene) == destination);
		player.GlobalPosition = gate.GlobalPosition + Vector3.Back; player.Velocity = Vector3.Zero; player.ResetPhysicsInterpolation(); await Wait(.2);
		var beforeFiles = Files(); int shards = MetaProgress.Shards, quota = AbilityStore.QuotaLevel;
		string rune = RuneCarry.Current; var character = RunSetup.Character; var weapon = RunSetup.Weapon;
		Vector3 position = player.GlobalPosition; float armor = player.Armor, health = player.Health;
		await Key(Godot.Key.F); Check(UiService.Instance?.Screen<ExpeditionScreen>()?.IsOpen != true && AdventureCheckpoint.IsActive, "真实F直接出击且保存完整回退点，无中间摘要");
		for (int i = 0; i < 100; i++)
		{
			if (survival ? RunController.Instance?.Mode is SurvivalDirector { State: SurvivalDirector.PhaseState.Surviving } : GetTree().CurrentScene is ForestExpedition { EntryReady: true }) break;
			await Wait(.1);
		}
		await Wait(.4);
		var run = RunController.Instance!; player = PlayerLocator.Player!; player.IsInvulnerable = true;
		Check(run.CanCancelAdventure && AdventureCheckpoint.IsActive, survival ? "正式死战出击创建唯一回退点" : "正式森林出击创建唯一回退点");
		if (!survival) Check(run.AvailableRewards().All(r=>r.RequiredModeIds.Length==0) && !run.GrantReward("surv_scavenger_compass"), "普通冒险的候选与直接发奖接口都拒绝生存专属");
		var hud = GetTree().CurrentScene.FindChild("Hud", true, false) as PlayerHud;
		Check(hud != null && hud.DisplayedRuneId == run.CarriedRuneId, "HUD从本局实际携带项显示符文，空携带也明确显示");
		Check(run.TryAcquireReward("1355") && run.TryAcquireReward("1355") && run.CollectedBonusShards == 40 && MetaProgress.Shards == shards,
			"069普通冒险与死战的两份资源强化只进本局账本，取消前不提前写局外");
		if (survival)
		{
			var director = (SurvivalDirector)run.Mode!; director.SetPhysicsProcess(false);
			var enemy = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate<Unit>();
			enemy.Tags = enemy.Tags.Append("Elite").ToArray(); GetTree().CurrentScene.AddChild(enemy);
			enemy.GlobalPosition = player.GlobalPosition + Vector3.Right * 5; enemy.Movement?.SetPhysicsProcess(false);
			director._PhysicsProcess(.51); enemy.ApplyDirectDamage(1000000, player); await Wait(.1);
			var drop = director.GetChildren().OfType<RunShardPickup>().Single(); player.GlobalPosition = drop.GlobalPosition; player.ResetPhysicsInterpolation(); await Wait(.4);
			Check(director.CollectedCombatShards == 10 && MetaProgress.Shards == shards, "067死战实际拾取精英10碎片，取消前局外资源仍未变化");
		}
		MetaProgress.AddShards(77); Check(MetaProgress.TrySpend(5), "本局真实资源收益与商店消耗");
#if DEBUG
		AbilityStore.DebugSetQuotaLevel(quota + 2); AbilityStore.Save();
#endif
		RuneCarry.Set(rune == "rune_lei_seal" ? "rune_wind_seal" : "rune_lei_seal");
		LoadoutSelection.SelectWeapon(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		using (var changedSettings = new ConfigFile()) { changedSettings.Load("user://settings.cfg"); changedSettings.SetValue("adventure_probe", "preserve", true); changedSettings.Save("user://settings.cfg"); }
		byte[] settingsDuring = Disk.ReadAllBytes(Path("settings.cfg"));
		Check(Files().Any(pair => !Equal(pair.Value, beforeFiles[pair.Key])), "隔离存档已真实变化，回退不能靠未发生写入蒙混通过");
		int completions = 0;
		if (overReward) { Check(run.OpenRewardForNode(RewardNodeKind.Special, () => completions++), "取消前打开真实特殊奖励模态"); await Wait(); }
		await Key(Godot.Key.Escape);
		var menu = UiService.Instance!.Screen<SettingsScreen>()!;
		Check(menu.IsOpen && UiRouter.Instance!.IsTopScreen(menu), overReward ? "真实Esc可在奖励模态上打开取消菜单" : "真实Esc打开关卡暂停菜单");
		var actions = menu.GetNode<Control>("Root/Panel/VBox/AdventureActions");
		Check(actions.Visible, "关卡中显示取消本次冒险入口");
		await Click(actions.GetNode<Button>("Abandon"));
		Check(actions.GetNode<Control>("Confirmation").Visible && !run.RunEnded, "点击取消先展示回退说明，不立即结算或切场景");
		await Click(actions.GetNode<Button>("Confirmation/Back"));
		Check(!actions.GetNode<Control>("Confirmation").Visible && run.CanCancelAdventure, "继续冒险不丢失回退点或更改本局");
		await Click(actions.GetNode<Button>("Abandon"));
		if (DisplayServer.GetName() != "headless")
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/adventure_cancel_{overReward}.png")) == Error.Ok, "取消冒险确认画面截图");
		}
		await Click(actions.GetNode<Button>("Confirmation/Confirm"));
		for (int i = 0; i < 100 && GetTree().CurrentScene?.SceneFilePath != "res://Scenes/Levels/Base.tscn"; i++) await Wait(.05);
		await Wait(.8); player = PlayerLocator.Player!;
		Check(GetTree().CurrentScene.SceneFilePath == "res://Scenes/Levels/Base.tscn" && !GetTree().Paused && UiRouter.Instance!.StackDepth == 0, "确认取消后返回基地并恢复输入和模态栈");
		Check(MetaProgress.Shards == shards && AbilityStore.QuotaLevel == quota && RuneCarry.Current == rune
			&& RunSetup.Character == character && RunSetup.Weapon == weapon, "资源、能力卡、符文和配装内存状态全部恢复");
		foreach (var pair in Files()) Check(Equal(pair.Value, beforeFiles[pair.Key]), $"{pair.Key}逐字回退到出击前");
		Check(Disk.ReadAllBytes(Path("settings.cfg")).SequenceEqual(settingsDuring), "本局更改的设置保留，不参与进度回退");
		Check(player.GlobalPosition.DistanceTo(position) < .5f && Mathf.IsEqualApprox(player.Health, health) && Mathf.IsEqualApprox(player.Armor, armor), "恢复基地出击点、生命和护甲");
		Check(!AdventureCheckpoint.IsActive && RunController.Instance == null && completions == 0, "回退点已消费，本局奖励回调与结算不执行");
		await Key(Godot.Key.Escape);
		menu = UiService.Instance!.Screen<SettingsScreen>()!;
		Check(!menu.GetNode<Control>("Root/Panel/VBox/AdventureActions").Visible, "基地设置不显示取消冒险");
		await Key(Godot.Key.Escape);
	}
}
