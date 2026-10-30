using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Debug;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.UI;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实库存交互、GM授予与特殊奖励唯一选择；不写任何玩家存档。</summary>
public partial class BuildRewardsProbe : Node
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Weapon _weapon = null!;
	private RunController _run = null!;
	private RewardScreen _reward = null!;
	private BuildScreen _build = null!;
	private GmConsole _gm = null!;
	private const string GridPath = "Root/Panel/VBox/Content/Inventory/Scroll/Grid";
	private const string GmPath = "Root/Panel/Margin/Layout/Tabs/Rewards/";

	private void Check(bool ok, string text)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[BuildRewards] {(ok ? "PASS" : "FAIL")} {text}");
	}
	private async Task Wait(double seconds)
	{
		ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
		while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private static void SetField(object target, string field, object value)
		=> target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
	private T Scene<T>(string path) where T : Node
	{
		T node = RuntimeResources.Load<PackedScene>(path).Instantiate<T>();
		AddChild(node);
		return node;
	}
	private async Task Click(Button button)
	{
		await UiProbeWait.Interactive(this);
		// Dummy显示器没有原生鼠标焦点；无头跑真实按钮处理器，图形跑完整鼠标分发链。
		if (DisplayServer.GetName() == "headless")
		{
			button.EmitSignal(Button.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			return;
		}
		Vector2 point = button.GetGlobalRect().GetCenter();
		Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point });
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private async Task ChooseFirst()
	{
		await UiProbeWait.Interactive(this);
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key1, PhysicalKeycode = Key.Key1, Pressed = true });
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key1, PhysicalKeycode = Key.Key1, Pressed = false });
		await Wait(.4);
	}

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		int previousChoiceCount = ConfigIndex.Settings.RewardChoiceCount;
		try
		{
			await Wait(.05);
			_reward = Scene<RewardScreen>("res://Scenes/UI/RewardScreen.tscn");
			_build = Scene<BuildScreen>("res://Scenes/UI/BuildScreen.tscn");
			_gm = Scene<GmConsole>("res://Scenes/Debug/GmConsole.tscn");
			// GM打开会聚焦命令栏；本夹具的查询全部直接赋值，不测试系统输入法。
			foreach(var input in _gm.FindChildren("*","LineEdit",true,false).OfType<LineEdit>())input.Editable=false;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			_player.Name = "RewardPlayer"; AddChild(_player);
			new ActRoguelike.Player.PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
			_weapon = NodeSearch.FindInSubtree<Weapon>(_player)!;
			_player.ProcessMode = ProcessModeEnum.Disabled;
			_run = new RunController { Name = "RewardRun", AutoStartStage = false };
			AddChild(_run); _run.SetProcess(false);
			SetField(_run, "_player", _player); SetField(_run, "_weapon", _weapon);
			await Wait(.1);
			await Availability();
			await GmAcquire();
			await Inventory();
			await SpecialRewards();
			await SingleCandidateReward();
		}
		catch (Exception exception) { Check(false, exception.ToString()); }
		finally
		{
			ConfigIndex.Settings.RewardChoiceCount = previousChoiceCount;
			UiRouter.Instance?.ResetForSceneChange();
			GD.Print($"[BuildRewards] SUMMARY pass={_pass} fail={_fail}");
			GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
		}
	}

	private async Task Availability()
	{
		List<RewardDefinition> rows = _run.AvailableRewards();
		Check(rows.Any(row => row.Id == "evo_divergence"), "符合能量掌／猎手限定的发散进入候选");
		Check(rows.All(row => row.AllowsCharacter("chr_hunter") && row.AllowsWeapon(_weapon)), "列表逐项遵守角色和武器限定");
		Check(!rows.Any(row => row.Id == "evo_affection") && !_run.TryAcquireReward("evo_affection"), "不满足限定的镰刀喜爱不能从列表获取");
		Check(!rows.Any(row => row.Id == "mut_burn_plus") && !_run.TryAcquireReward("mut_burn_plus"), "未拥有上一级时不能获取灼烧升级");
		float health = _player.MaxHealth;
		Check(_run.TryAcquireReward("upg_life") && _player.MaxHealth > health, "真实授予一级生命立即改变生命上限");
		Check(_run.TryAcquireReward("mut_burn") && _run.AvailableRewards().Any(row => row.Id == "mut_burn_plus")
			&& !_run.TryAcquireReward("mut_burn"), "前置实时解锁，同时唯一增幅不能重复获取");
		Check(_run.TryAcquireReward("mut_pierce") && !_run.AvailableRewards().Any(row => row.Id == "mut_ricochet"), "获取穿透后真实互斥项弹射退出列表");
		await Wait(.05);
	}

	private async Task GmAcquire()
	{
		_gm.Open();
		await Wait(.4);
		Check(_gm.IsOpen && GetTree().Paused && Input.MouseMode == Input.MouseModeEnum.Visible, "GM打开时暂停世界并释放鼠标");
		Check(_gm.RewardButtonCount == _run.AvailableRewards().Count, "GM默认显示完整当前可获取池");
		var category = _gm.GetNode<OptionButton>(GmPath + "Filters/Category");
		category.Select(2); category.EmitSignal(OptionButton.SignalName.ItemSelected, 2L); await Wait(.05);
		Check(_gm.GetNode<VBoxContainer>(GmPath + "Scroll/List").GetChildren().Cast<Button>().All(button =>
			RewardCatalog.Find(button.Name.ToString())?.Kind == RewardKind.Evolution), "GM分类过滤只显示进化");
		category.Select(0); category.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
		var search = _gm.GetNode<LineEdit>(GmPath + "Filters/Search");
		// 本项直接设置查询，只验证过滤与授予；不激活本机输入法的外部PNG资源。
		search.Editable=false;
		search.Text = "upg_power"; await Wait(.05);
		Check(_gm.RewardButtonCount == 1, "GM搜索可按稳定ID定位一项");
		float damage = _player.Stats.Get("DamageMultiplier");
		await Click(_gm.GetNode<Button>(GmPath + "Scroll/List/upg_power")); await Wait(.05);
		Check(_gm.LastCommandSucceeded && _run.Build.StacksOf("upg_power") == 1 && _player.Stats.Get("DamageMultiplier") > damage,
			"真实鼠标点击GM列表：增幅进入构筑且真实攻击属性上升");
		await Click(_gm.GetNode<Button>(GmPath + "Scroll/List/upg_power")); await Wait(.05);
		Check(_run.Build.StacksOf("upg_power") == 2 && _gm.RewardButtonCount == 1, "可重复增幅点击叠加，获取后仍可继续测试");
		var powerButton = _gm.GetNode<Button>(GmPath + "Scroll/List/upg_power");
		Check(!powerButton.Text.Contains("持有") && !powerButton.TooltipText.Contains("持有：") && !powerButton.TooltipText.Contains("上限："), "GM仍允许原可重复增幅获取，但列表和提示不显示层数");
		search.Text = string.Empty; await Wait(.05);
		await Shot("gm_rewards");
		_gm.Close(); await Wait(.2);
		Check(!GetTree().Paused && UiRouter.Instance?.StackDepth == 0 && (DisplayServer.GetName() == "headless" || Input.MouseMode == Input.MouseModeEnum.Captured), "GM关闭恢复暂停、鼠标与路由栈");
	}

	private async Task Inventory()
	{
		_build.Open(); await Wait(.4);
		var grid = _build.GetNode<GridContainer>(GridPath);
		Check(_build.TileCount == _run.Build.Acquired.Count, "构筑库存每种增幅只有一个格子");
		Button power = grid.GetNode<Button>("upg_power");
		ulong instance = power.GetInstanceId();
		Vector2 position = power.GetGlobalRect().GetCenter();
		if (DisplayServer.GetName() == "headless") power.EmitSignal(Control.SignalName.MouseEntered);
		else Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
		await Wait(.2);
		Check(_build.SelectedRewardId == "upg_power" && _build.SelectedName == _run.Build.DisplayName(RewardCatalog.Find("upg_power")!), "真实悬停立即选中并显示对应名称及当前升级等级");
		Check(_build.SelectedDescription == RewardCatalog.Find("upg_power")!.Description, "详情显示完整效果描述");
		Check(_build.SelectedInformation.Length == 0 && RewardPresentation.Details(RewardCatalog.Find("upg_power")!).Contains("前置："), "048构筑只展示效果；规则资料仍完整保留给图鉴");
		Check(grid.GetChildren().Cast<Button>().All(tile => !tile.HasNode("StackBadge")), "可重复和单次增幅均不显示层数角标，后端真实叠层保留");
		Check(!_build.SelectedInformation.Contains("持有：") && !_build.SelectedInformation.Contains("上限：")
			&& _build.GetNode<Label>("Root/Panel/VBox/Summary").Text == $"本局持有 {_run.Build.Acquired.Count} 项增幅"
			&& !_run.Build.Describe().Contains('×'), "详情、汇总和构筑摘要不显示层数");
		Check(power.GetThemeStylebox("normal") is StyleBoxFlat { ShadowSize: > 0 }, "选中格子具有外部发光圈");
		await Wait(.4);
		Check(grid.GetNode<Button>("upg_power").GetInstanceId() == instance && _build.SelectedRewardId == "upg_power", "连续帧不重建格子，悬停及选中保持稳定");
		Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) });
		await Wait(.05);
		grid.GetNode<Button>("upg_life").GrabFocus(); await Wait(.05);
		Check(_build.SelectedRewardId == "upg_life", "键盘焦点进入也更新选中项");
		var left = _build.GetNode<Control>("Root/Panel/VBox/Content/Inventory");
		var right = _build.GetNode<Control>("Root/Panel/VBox/Content/Details");
		Check(Mathf.Abs(left.Size.X - right.Size.X) <= 3f, $"库存与详情各占一半内容宽度（{left.Size.X:0}/{right.Size.X:0}）");
		await Shot("build_inventory");
		_build.Close(); await Wait(.3);
		Check(!GetTree().Paused && UiRouter.Instance?.StackDepth == 0, "构筑面板关闭恢复游戏");
	}

	private async Task SpecialRewards()
	{
		ConfigIndex.Settings.RewardChoiceCount = RewardCatalog.All.Length + 1;
		int completed = 0;
		Check(_run.OpenRewardForNode(RewardNodeKind.Special, () => completed++), "真实特殊奖励节点入口接受唯一一次选择");
		await Wait(.4);
		Check(_reward.Options.Any(row => row.Id == "evo_divergence") && _reward.Options.Any(row => row.Kind == RewardKind.Enhancement), "特殊池尊重AnyRewardKind，含符合限定的进化和强化");
		Check(!_reward.Options.Any(row => row.Id == "evo_affection") && !_reward.Options.Any(row => row.Id == "upg_life_plus2"), "特殊池仍排除错误武器与未满足前置");
		// 展示全池仅用于资格断言；实际连续交互恢复生产三选一数量。
		await Shot("special_reward");
		ConfigIndex.Settings.RewardChoiceCount = 3;
		string chosen = _reward.Options[0].Id;
		int before = _run.Build.StacksOf(chosen);
		await ChooseFirst();
		Check(_run.Build.StacksOf(chosen) == before + 1 && completed == 1, "一次选择真实生效并立即完成节点一次");
		Check(!_reward.IsOpen && !_reward.GetNode<Control>("Root").Visible, "等待动画结束后奖励页关闭，不再打开第二页");
		Check(!GetTree().Paused && UiRouter.Instance?.StackDepth == 0, "唯一选择完成后解除模态与暂停");
		int total = _run.Build.Acquired.Sum(row => _run.Build.StacksOf(row.Id));
		await ChooseFirst();
		Check(_run.Build.Acquired.Sum(row => _run.Build.StacksOf(row.Id)) == total && completed == 1, "重复数字键不会再次授予奖励或触发完成回调");
		Check(!_reward.IsOpen && !GetTree().Paused && UiRouter.Instance?.StackDepth == 0 && (DisplayServer.GetName() == "headless" || Input.MouseMode == Input.MouseModeEnum.Captured),
			"连续奖励完成后恢复世界、输入捕获和路由栈");
	}

	private async Task SingleCandidateReward()
	{
		// 用只有一项的候选夹具验证唯一选择耗尽；授予、界面和回调仍走生产路径。
		var pool = (RewardPool)typeof(RunController).GetField("_pool", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_run)!;
		var candidates = (List<RewardDefinition>)typeof(RewardPool).GetField("_candidates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;
		RewardDefinition remaining = _run.AvailableRewards().First(row => row.MaxStacks == 1 && row.Requires.Length == 0);
		candidates.Clear(); candidates.Add(remaining);
		int completed = 0;
		Check(_run.OpenRewardForNode(RewardNodeKind.Special, () => completed++), "只剩一个合格候选时特殊节点仍可领取");
		await Wait(.4); await ChooseFirst();
		Check(_run.Build.Has(remaining.Id) && completed == 1 && !_reward.IsOpen && !GetTree().Paused, "唯一选择用尽池子后正常结束，不吞回调、不暂停卡死");
		Check(!_run.OpenRewardForNode(RewardNodeKind.Special, () => completed++) && completed == 1, "候选池已空则拒绝开页，不伪造领取回调");
	}

	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = $"res://Logs/evidence/reward_ui_{name}.png";
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path(ProjectSettings.GlobalizePath(path))) == Error.Ok, $"截图 {name}");
	}
}

