using System;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>同一目录实例切换语言，验证逻辑身份、模板参数与已打开界面。</summary>
public partial class ContentLocalizationProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[ContentLocalization] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async System.Threading.Tasks.Task Wait() => await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; string previous = ContentText.Locale;
		try
		{
			await Wait(); previous = ContentText.Locale;
			Check(ConfigIndex.Settings.ContentLocales.Length == ContentInventorySnapshot.Current.Count("ContentLocales") && TranslationServer.GetLoadedLocales().Length == ConfigIndex.Settings.ContentLocales.Length, "显式目录与引擎翻译表实际加载成功");
			var power = RewardCatalog.Find("upg_power")!; var burn = RewardCatalog.Find("mut_burn")!;
			var ids = RewardCatalog.All.Select(r => r.Id).ToArray(); var eligibility = RewardCatalog.All.Select(r => string.Join("|", r.Requires) + ":" + string.Join("|", r.Excludes)).ToArray();
			ContentText.SetLocale("zh");
			string chinese = power.Name;
			Check(chinese == "力量" && burn.Description.Contains("3"), "中文目录与效果参数一致");
			var reward = RuntimeResources.Load<PackedScene>("res://Scenes/UI/RewardScreen.tscn").Instantiate<RewardScreen>(); AddChild(reward);
			var build = RuntimeResources.Load<PackedScene>("res://Scenes/UI/BuildScreen.tscn").Instantiate<BuildScreen>(); AddChild(build);
			build.DebugShowSample(RewardCatalog.All.Count()); build.Open();
			var tile = build.GetNode<Control>("Root/Panel/VBox/Content/Inventory/Scroll/Grid").GetNode<Button>(power.Id); tile.EmitSignal(Control.SignalName.MouseEntered);
			reward.Open(new[] { power, burn }, "翻译测试"); await Wait();
			Check(build.SelectedName == chinese, "已打开构筑界面先显示中文");
			ContentText.SetLocale("en"); await Wait();
			GD.Print($"[ContentLocalization] locale={ContentText.Locale} loaded={string.Join(",", TranslationServer.GetLoadedLocales())} power={power.Name} / {power.Description} burn={burn.Name} / {burn.Description} direct={TranslationServer.Translate("content.upg_power.name")}");
			Check(power.Name == "Power" && power.Description.Contains("15") && !power.Description.Contains('{'), "原目录实例自动刷新英文整句与强类型数值");
			Check(burn.Name == "Scorching Hits" && burn.Description.Contains("3 seconds") && !burn.Description.Contains('{'), "数组内效果字段直接供译文使用");
			Check(build.SelectedName == "Power" && build.SelectedRewardId == power.Id, "已打开构筑详情刷新且保留选择");
			var cards = reward.GetNode<Control>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().Where(b => b.Name != "CardTemplate").ToArray();
			Check(cards[0].Text.Contains("Power") && cards[1].Text.Contains("Scorching Hits"), "已打开奖励卡原地刷新文本");
			Check(reward.Options[0] == power && reward.Options[1] == burn, "翻译不重抽候选或替换选项对象");
			if (DisplayServer.GetName() != "headless")
			{
				// 同时打开两页只验证原地翻译；拍卡牌时隐藏夹具的构筑页，避免不可能的玩家叠层。
				var buildRoot = build.GetNode<Control>("Root"); buildRoot.Visible = false;
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				using var screenshot = GetViewport().GetTexture().GetImage();
				Check(screenshot.SavePng(ActRoguelike.Testing.ValidationArtifacts.Path("res://Logs/evidence/content-localization-en.png")) == Error.Ok, "保存真实渲染的英文界面检查图");
				buildRoot.Visible = true;
			}
			Check(RewardCatalog.All.Select(r => r.Id).SequenceEqual(ids) && RewardCatalog.All.Select(r => string.Join("|", r.Requires) + ":" + string.Join("|", r.Excludes)).SequenceEqual(eligibility), "语言切换前后身份、前置和互斥完全相同");
			Check(ContentText.Get("missing.translation", "原文回退") == "原文回退", "缺译显示原文，不显示空白或逻辑键");
			var bad = new ConfigIndexData { Upgrades = new[] { new RewardConfig { Id = "probe_text", NameKey = "missing", DescriptionKey = "bad", DescriptionTemplate = "伤害 {Weight}", ApplyHandler = "StatSet" } }, ContentLocales = new[] { new ContentLocale { Locale = "en", Messages = new() { ["bad"] = "Damage {Missing}" } } } };
			var issues = ContentValidator.Validate(bad);
			Check(issues.Any(i => i.Reason.Contains("缺少整句译文")) && issues.Any(i => i.Reason.Contains("命名参数")) && issues.Any(i => i.Reason.Contains("无法读取参数")), "统一校验捕获缺译、参数不一致和无效参数");
			ContentText.SetLocale("zh"); await Wait();
			Check(power.Name == chinese && build.SelectedName == chinese && cards[0].Text.Contains(chinese), "切回中文刷新同一批界面");
		}
		catch (Exception error) { Check(false, error.ToString()); }
		finally { ContentText.SetLocale(previous); UiRouter.Instance?.ResetForSceneChange(); }
		GD.Print($"[ContentLocalization] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}
