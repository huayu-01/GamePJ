using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Debug;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>B批独立探针。自身在场景根外存活，可验证真实run/base场景切换。</summary>
public partial class BatchBProbe : Node
{
	private int _passed;
	private int _failed;
	private Node _main = null!;
	private Unit _player = null!;
	private GmConsole _gm = null!;
	private RunController _run = null!;
	private AbilityHud _hud = null!;
	private LineEdit InputLine => _gm.GetNode<LineEdit>("Root/Panel/Margin/Layout/Command/Input");
	public override async void _Ready()
	{
		try { await Run(); }
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[BatchB] === B 批验收结束：通过 {_passed}，失败 {_failed} ===");
		ActRoguelike.Core.GameExit.Request(GetTree(), _failed == 0 ? 0 : 1);
	}
	private void Check(bool condition, string message)
	{
		if (condition) _passed++; else _failed++;
		GD.Print($"[BatchB] {(condition ? "PASS" : "FAIL")} {message}");
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task KeyPress(Key code)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private void Action(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		bool paused = GetTree().Paused;
		GetTree().Paused = false;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"视口截图 {name}.png");
		GetTree().Paused = paused;
	}
	private async Task Command(string text, bool success = true)
	{
		int count = _gm.CommandCount;
		InputLine.Text = text;
		InputLine.GrabFocus();
		await KeyPress(Key.Enter);
		await Wait(0.03);
		Check(_gm.CommandCount == count + 1 && _gm.LastCommandSucceeded == success, $"GUI命令 {text} → {_gm.LastResult}");
	}
	private void Bind()
	{
		_main = GetTree().CurrentScene;
		_player = _main.GetNode<Unit>("Player");
		_run = _main.GetNode<RunController>("RunController");
		_gm = _main.GetNode<GmConsole>("GmConsole");
		_hud = _main.GetNode<AbilityHud>("Hud/Root/Abilities");
	}
	private async Task Run()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_main = RuntimeResources.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate();
		GetTree().Root.AddChild(_main);
		GetTree().CurrentScene = _main;
		await Wait(1.3);
		Bind();
		_run.Call("BeginRunWithCharacter", 3, 3);
		await Wait(0.3);
		Check(_hud.SlotCount == 5 && _hud.MouseFilter == Control.MouseFilterEnum.Ignore, _hud.DebugState());
		Check(_hud.IconPathOf("skl_a1_w1_2").EndsWith("ic_skill_nova.svg"), "HUD图标读取技能行Texture2D引用");
		Check(AbilityHud.KeyCaption("skill_1") == "Q" && AbilityHud.KeyCaption("weapon_skill_1") == "左键", "按键提示来自InputMap");
		await Shot("b_hud_ready");
		await KeyPress(Key.Q);
		await Wait(0.65);
		var dodge = _player.GetNode<BackdashAbility>("AbilitySkill1");
		float first = _hud.CooldownFraction(dodge.AbilityId);
		Check(!dodge.IsActive && first > 0.8f && first <= 1, $"真实Q后冷却扇形比例={first:0.###} CD={dodge.CooldownRemaining:0.##}");
		await Shot("b_hud_cooldown");
		await Wait(0.4);
		Check(_hud.CooldownFraction(dodge.AbilityId) < first - 0.02f, "冷却扇形随真实时间递减");
		await Wait(4.2);
		Check(_hud.CooldownFraction(dodge.AbilityId) is > 0.4f and < 0.65f, "真实冷却推进至半程，扇形面积与剩余时间一致");
		await Shot("b_hud_mid_cooldown");
		Action("weapon_skill_2", true);
		await Wait(0.3);
		Ability palm = _player.GetNode<Ability>("AbilityWeaponSkill2");
		Check(palm.IsCharging && palm.ChargeProgress > 0, "真实右键蓄力与技能栏进度一致");
		await Shot("b_hud_charging");
		Action("weapon_skill_2", false);
		await Wait(0.5);
		Check(_gm.Available && !InputMap.HasAction("gm_console"), "GM节点已启用，无常驻InputMap绑定");
		await KeyPress(Key.G);
		await Wait(0.4);
		Check(_gm.IsOpen && GetTree().Paused && InputLine.HasFocus(), "真实G打开GM、暂停并聚焦输入框");
		int shards = MetaProgress.Shards;
		await Command("resource 17");
		Check(MetaProgress.Shards == shards + 17, $"真实资源命令：碎片{shards}→{MetaProgress.Shards}（脚本结束精确恢复原存档）");
		var disk = new ConfigFile();
		disk.Load("user://meta.cfg");
		Check(disk.GetValue("meta", "shards", -1).AsInt32() == shards + 17, "资源命令真实落盘");
		await Command("list skills");
		Check(_gm.LastResult.Contains("skl_a1_skill_1") && _gm.LastResult.Contains("后撤"), "技能目录来自当前启用技能");
		await Command("list upgrades");
		Check(_gm.LastResult.Contains(RewardCatalog.All[0].Id), "增幅目录来自当前表");
		RewardDefinition reward = Array.Find(RewardCatalog.All, row => row.Kind == RewardKind.Enhancement)!;
		int stacks = _run.Build.StacksOf(reward.Id);
		await Command($"give {reward.Id}");
		Check(_run.Build.StacksOf(reward.Id) == stacks + 1, "指定增幅进入真实RunBuild");
		await Command("cdr 0.5");
		Check(Mathf.IsEqualApprox(dodge.EffectiveCooldown, dodge.Cooldown * 0.5f), "GM的CD倍率进入原有计算管线");
		await Command("charges skl_a1_skill_1 3");
		await KeyPress(Key.Escape);
		await Wait(0.3);
		Check(!_gm.IsOpen && !GetTree().Paused, "一次Esc关闭GM并恢复游戏，不打开设置页");
		Check(dodge.MaxCharges == 3 && dodge.ChargesRemaining == 3, "层数命令补满3层");
		await KeyPress(Key.Q);
		await Wait(0.65);
		Check(dodge.ChargesRemaining == 2 && _hud.CooldownFraction(dodge.AbilityId) > 0, "真实Q消耗一层，技能栏显示2/3及回层弧");
		await Shot("b_hud_charges");
		await KeyPress(Key.G);
		await Wait(0.35);
		await Command("refill skl_a1_skill_1");
		Check(dodge.ChargesRemaining == 3 && dodge.CooldownRemaining == 0, "补满命令清CD并恢复层数");
		await Command("invincible on");
		float health = _player.Health;
		DamageResult hit = _player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 100 });
		Check(hit.Invulnerable && _player.Health == health, "GM无敌确实阻止伤害");
		await Command("invincible off");
		Check(!_player.IsInvulnerable, "关闭无敌还原原状态");
		_player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 30 });
		await Command("heal");
		Check(_player.Health == _player.MaxHealth && _player.Armor == _player.StartingArmor, "恢复状态命令回满生命与护甲");
		int enemies = GetTree().GetNodesInGroup("enemy_units").Count;
		await Command("spawn enemy_chaser 2");
		Check(GetTree().GetNodesInGroup("enemy_units").Count == enemies + 2, "召唤命令真实增加两个敌人");
		await Command("list enemies chaser");
		Check(_gm.LastResult.Contains("enemy_chaser"), "敌人目录可按ID或名称过滤");
		int unchanged = MetaProgress.Shards;
		foreach (string bad in new[] { "resource -1", "resource 3 extra", "give __missing__", "cdr NaN", "charges __missing__ 3", "spawn __missing__", "spawn enemy_chaser 0", "__unknown__" }) await Command(bad, false);
		Check(MetaProgress.Shards == unchanged && dodge.MaxCharges == 3, "错误命令不改变资源与技能层数");
		InputLine.Text = "refi";
		await KeyPress(Key.Tab);
		Check(InputLine.Text == "refill ", "Tab通过真实GUI输入补全命令");
		await KeyPress(Key.Up);
		Check(InputLine.Text == "__unknown__", "↑通过真实GUI输入读取历史");
		await Command("help");
		await Shot("b_gm_console");
		await Command("invincible on");
		int depth = UiRouter.Instance!.StackDepth;
		_gm.QueueFree();
		await Wait(0.15);
		Check(UiRouter.Instance.StackDepth == depth - 1 && !GetTree().Paused && !_player.IsInvulnerable, "开着GM直接拔除：出栈、恢复暂停和GM持有的无敌");
		await KeyPress(Key.G);
		await Wait(0.1);
		Check(!UiRouter.Instance.AnyOpen && !_main.HasNode("GmConsole"), "拔除节点后G键毫无反应");
		_gm = RuntimeResources.Load<PackedScene>("res://Scenes/Debug/GmConsole.tscn").Instantiate<GmConsole>();
		_main.AddChild(_gm);
		await KeyPress(Key.G);
		await Wait(0.35);
		Check(_gm.IsOpen, "重新挂载预制体即恢复G快捷键");
		await KeyPress(Key.Escape);
		await Wait(0.2);
		foreach ((int character, int weapon, string weaponId, int count) in new[] { (3, 3, "skl_a1_w1_1", 5), (3, 4, "skl_a2_w1_1", 5), (3, 5, "skl_a3_w1_1", 5), (4, 6, "skl_b1_w1_1", 6), (4, 7, "skl_b2_w1_1", 6), (4, 8, "skl_b3_w1_1", 6) })
		{
			_run.Call("BeginRunWithCharacter", character, weapon);
			await Wait(0.2);
			Check(_hud.SlotCount == count && _hud.DisplayedIds.Contains(weaponId), $"换装{character}/{weapon}：{_hud.DebugState()}");
		}
		await Shot("b_hud_weapon_master");
		Vector2 original = _hud.Size;
		_hud.Size = new Vector2(400, 300);
		bool inside = true;
		foreach (string id in _hud.DisplayedIds.Split(','))
		{
			Rect2 bounds = _hud.BoundsOf(id);
			inside &= bounds.Position.X >= 0 && bounds.Position.Y >= 0 && bounds.End.X <= 400 && bounds.End.Y <= 300;
		}
		Check(inside, "400×300可用区域内自动换行，技能格无越界");
		_hud.Size = original;
		await KeyPress(Key.G);
		await Wait(0.35);
		await Command("list interactions");
		Check(_gm.LastResult.Contains("0 ·"), "交互列表提供真实可用节点及序号");
		List<Interactable> items = Interactable.All.FindAll(item => IsInstanceValid(item) && item.Enabled && !item.Used && !item.Vanished);
		items.Sort((a, b) => string.Compare(a.GetPath(), b.GetPath(), StringComparison.Ordinal));
		int targetIndex = items.FindIndex(item => item is RewardNode);
		Check(targetIndex >= 0, "存在可测试的真实奖励节点");
		await Command($"interact {targetIndex}");
		GD.Print($"[BatchB] 交互取证：{items[targetIndex].Name} 就绪={items[targetIndex].CanInteract} 范围={items[targetIndex].PlayerInRange} 距离={_player.GlobalPosition.DistanceTo(items[targetIndex].GlobalPosition):0.###} 关卡={StageController.Instance?.DebugState}");
		Check(!_gm.IsOpen && (items[targetIndex].Used || UiRouter.Instance.AnyOpen || StageController.Instance!.ChallengerActive), "调用真实奖励节点链路产生节点/界面/挑战结果");
		_gm.Open();
		await Wait(0.2);
		await Command("base");
		await Wait(0.8);
		Check(GetTree().CurrentScene.SceneFilePath == "res://Scenes/Levels/Base.tscn", "base命令真实切换基地场景");
		Check(!GetTree().Paused && UiRouter.Instance!.StackDepth == 0, "从奖励模态返回基地后没有旧界面引用与残留暂停");
		_main = GetTree().CurrentScene;
		_gm = _main.GetNode<GmConsole>("GmConsole");
		await KeyPress(Key.G);
		await Wait(0.35);
		Check(_gm.IsOpen, "同一GM预制体可直接挂进基地使用");
		await Command("run chr_hunter wpn_bow");
		await Wait(1.0);
		Bind();
		Check(GetTree().CurrentScene.SceneFilePath == "res://Scenes/Main.tscn" && _hud.DisplayedIds.Contains("skl_a3_w1_2"), "run命令真实出征为猎手+弓");
		await KeyPress(Key.G);
		await Wait(0.35);
		await Command("list skills");
		await Shot("b_gm_after_travel");
		_gm.Close();
		await Wait(0.2);
		var settings = _main.GetNode<SettingsScreen>("SettingsScreen");
		settings.Open();
		await KeyPress(Key.G);
		await Wait(0.35);
		Check(settings.IsOpen && _gm.IsOpen && UiRouter.Instance.StackDepth == 2, "设置页暂停期间仍能G打开GM，形成两层界面栈");
		_gm.QueueFree();
		await Wait(0.1);
		Check(settings.IsOpen && GetTree().Paused && UiRouter.Instance.StackDepth == 1, "拔除GM保留原设置页及其暂停状态");
		settings.Close();
		await Wait(0.2);
		var disabled = RuntimeResources.Load<PackedScene>("res://Scenes/Debug/GmConsole.tscn").Instantiate<GmConsole>();
		disabled.Enabled = false;
		_main.AddChild(disabled);
		await Wait(0.1);
		await KeyPress(Key.G);
		Check(!IsInstanceValid(disabled) && !UiRouter.Instance.AnyOpen && !GetTree().Paused, "Enabled=false预制体自行移除，G不响应且不遗留暂停");
	}
}
