using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>047/048实际控件、焦点确认、规则分色、实例隔离与正常深度专项。</summary>
public partial class VisualRevisionProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "VisualRevision";
	private static IEnumerable<Node> Walk(Node root) { yield return root; foreach(Node child in root.GetChildren()) foreach(Node node in Walk(child)) yield return node; }
	private async Task Tap(Key key) {
		Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true }); await Wait(.03);
		Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=false }); await Wait(.06);
	}
	protected override async Task RunChecks()
	{
		var reward=Mode.GetNode<RewardScreen>("RunController/RewardScreen");
		var rig=Player.GetNode<Node3D>("CameraRig");rig.SetPhysicsProcess(false);rig.SetProcessInput(false);
		var camera=GetViewport().GetCamera3D();camera.GlobalPosition=Player.GlobalPosition+new Vector3(0,4.6f,9.8f);camera.LookAt(Player.GlobalPosition+Vector3.Up*1.2f);camera.ResetPhysicsInterpolation();
		var hud=Walk(Mode).OfType<CanvasLayer>().First(n=>n.GetType().Name=="SurvivalHud");
		bool hudWasVisible=hud.Visible;
		RewardDefinition[] rows={RewardCatalog.Find("mut_burn")!,RewardCatalog.Find("upg_power")!,RewardCatalog.Find("evo_divergence")!};
		reward.Open(rows,"等级 2 · 选择一项奖励");while (reward.InputBlocked) await Wait(.02);
		Check(!hud.Visible,"048选择页隐藏战斗HUD，防止透明卡面叠字");
		var cards=reward.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<RewardCard>().ToArray();
		Check(cards.Length==3 && cards[0].DisplayTitle==rows[0].Name,"048真实三选一卡名与正式目录一致");
		Check(cards.All(c=>!c.Text.Contains("（异化）") && !c.Text.Contains("（强化）") && c.FindChild("CategoryIcon",true,false) is TextureRect),"048类别由右上图标表达，卡文不重复类别");
		Check(cards[0].TitleColor.B>cards[0].TitleColor.G && cards[0].TitleColor.R>cards[0].TitleColor.G,"048异化卡名为紫色，停用的旧路线不恢复");
		Check(cards.All(c=>c.DescriptionColor==GlassUi.Paper),"048描述正文不随类别色改变");
		Check(cards.All(c=>!c.TooltipText.Contains("前置") && !c.TooltipText.Contains("互斥")),"048候选不产生遮住其它卡的条件悬浮框");
		var selected=new List<int>(); reward.RewardChosen += index=>selected.Add(index);
		cards[1].GrabFocus();await Wait(.05);await Tap(Key.Enter);
		GD.Print($"[VisualRevision] focus-index={reward.SelectedIndex} submitted={string.Join(',',selected)} blocked={reward.InputBlocked}");
		Check(reward.SelectedIndex==1 && selected.SequenceEqual(new[]{1}),"048真实Enter确认焦点中的第二项，不回退第一项");
		Check(reward.HasKeyHintBar && reward.KeyHintBarTopmost && reward.KeyHintBarIgnoresMouse,"048按键栏存在、处于界面上层且不拦截鼠标");
		await Shot("048-reward-glass"); reward.Close();await Wait(.35);
		var run=Mode.GetNode<RunController>("RunController");
		run.TryAcquireReward("mut_burn");
		for(int count=0;count<24;count++) {
			var available=run.AvailableRewards().FirstOrDefault(row=>!run.Build.Has(row.Id)&&row.Id!="mut_burn_plus");
			if(available==null || !run.TryAcquireReward(available.Id))break;
		}
		var build=Mode.GetNode<BuildScreen>("BuildScreen");build.Open();while (build.InputBlocked) await Wait(.02);
		build.GetNode<GridContainer>("Root/Panel/VBox/Content/Inventory/Scroll/Grid").GetChildren().OfType<Button>().FirstOrDefault(b=>b.Name.ToString().StartsWith("mut_burn"))?.GrabFocus();await Wait(.1);
		Check(build.SelectedInformation.Length==0,"048构筑不再堆前置/互斥字段");
		Check(build.GetNode("Root/KeyHintBar").HasNode("GlassBacking/FrostedSurface"),"048更新按键提示保留磨砂阅读底");
		await Shot("048-build-glass");
		var mutationFilter=(Button)build.FindChild("Filter_mutations",true,false);mutationFilter.GrabFocus();await Tap(Key.Enter);
		var visibleTiles=build.GetNode<GridContainer>("Root/Panel/VBox/Content/Inventory/Scroll/Grid").GetChildren().OfType<Button>().Where(tile=>tile.Visible).ToArray();
		Check(visibleTiles.Length>0 && visibleTiles.All(tile=>GlassUi.CategoryKey(RewardCatalog.Find(tile.Name.ToString())!)=="mutations"),"048分类栏真实键盘切换异化，仅过滤显示而不改构筑");
		var allTiles=build.GetNode<GridContainer>("Root/Panel/VBox/Content/Inventory/Scroll/Grid").GetChildren().OfType<Button>().ToArray();
		string? emptyCategory=new[]{"enhancements","evolutions","mutations","specializations"}.FirstOrDefault(category=>allTiles.All(tile=>GlassUi.CategoryKey(RewardCatalog.Find(tile.Name.ToString())!)!=category));
		Check(emptyCategory!=null,"048真实持有夹具包含一个空分类用于检查空态");
		if(emptyCategory!=null) { ((Button)build.FindChild("Filter_"+emptyCategory,true,false)).GrabFocus();await Tap(Key.Enter);Check(build.SelectedRewardId.Length==0 && build.SelectedName=="当前分类暂无增幅" && build.TileCount==allTiles.Length,"048空分类清掉旧详情并保留真实库存"); }
		build.Close();await Wait(.35);
		var codex=UiService.Instance!.Screen<CodexScreen>()!;codex.SearchBox.Editable=false;codex.Open();while (codex.InputBlocked) await Wait(.02);
		var entry=codex.Entries.First(e=>e.Key.EndsWith('/'+rows[0].Id));codex.ChooseCategory(entry.Category);codex.SetSearch(rows[0].Name);await Wait(.1);
		var info=Walk(codex).OfType<RichTextLabel>().FirstOrDefault();
		GD.Print($"[VisualRevision] codex={codex.SelectedName} info={info?.Text}");
		Check(info!=null && info.Text.Contains("[color=#a16822]") && info.Text.Contains("[color=#8551a5]"),"048图鉴的前置与互斥标题分别着色，完整规则仍可查");
		await Shot("048-codex-glass");
		codex.SetSearch("");codex.ChooseCategory("enemies");
		var mushroom=codex.Entries.First(e=>e.Name=="胆小菇");codex.SelectEntry(mushroom.Key);
		var mushroomTile=Walk(codex).OfType<Button>().First(b=>b.TooltipText==mushroom.Name);mushroomTile.GrabFocus();await Wait(.1);
		var creatureList=mushroomTile.GetParent().GetParent<ScrollContainer>();creatureList.EnsureControlVisible(mushroomTile);await Wait(.15);
		GD.Print($"[VisualRevision] creature-scroll={creatureList.ScrollVertical} tile={mushroomTile.GetGlobalRect()} list={creatureList.GetGlobalRect()}");
		await Shot("048-codex-creature");
		codex.SearchBox.GrabFocus();await Wait(.1);await Tap(Key.J);
		Check(codex.IsOpen && !Walk(codex.GetNode("Root/KeyHintBar")).OfType<Label>().Any(l=>l.Text=="J"),"048搜索焦点不让J关闭图鉴，按键栏同步隐藏该动作");
		codex.SearchBox.ReleaseFocus();codex.Close();await Wait(.35);
		Check(hud.Visible==hudWasVisible,"048关闭资料页恢复原战斗HUD可见状态");
		var scene=RuntimeResources.Load<PackedScene>("res://Scenes/Combat/SkillIndicator.tscn");
		var one=scene.Instantiate<SkillIndicator>();var two=scene.Instantiate<SkillIndicator>();Mode.AddChild(one);Mode.AddChild(two);one.IsHostile=true;
		var position=Player.GlobalPosition;one.ShowAreaPoint(position+new Vector3(-2,0,-3),3);one.SetWarningProgress(.75f);two.ShowLineArea(position+new Vector3(2,0,0),Vector3.Forward,8,2);
		Check(one.WarningProgress==.75f && one.LastRadius==3 && two.LastLength==8 && two.LastWidth==2,"047只读进度及原真实范围保持");
		Check(one.GetNode<MeshInstance3D>("DrawRoot/BrushArea").MaterialOverride!=two.GetNode<MeshInstance3D>("DrawRoot/BrushArea").MaterialOverride,"047多实例笔触材质独立");
		Check(one.GetNode<MeshInstance3D>("DrawRoot/BrushContour").MaterialOverride is StandardMaterial3D { NoDepthTest:false },"047轮廓保持正常深度测试");
		await Shot("047-brush-current");one.HideAll();
		Check(!one.IsShowing && !one.GetNode<MeshInstance3D>("DrawRoot/BrushArea").Visible && two.IsShowing,"047隐藏不残留笔触，也不清除其它实例");
		one.QueueFree();two.QueueFree();
	}
}
