using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ActRoguelike.UI;

public partial class BuildScreen
{
	private string _categoryFilter="all";
	private readonly Dictionary<string,Button> _categoryTabs=new();
	private void RefineLayout(VBoxContainer details)
	{
		var panel=GetNode<PanelContainer>("Root/Panel");
		panel.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
		panel.OffsetLeft=80;panel.OffsetRight=-80;panel.OffsetTop=72;panel.OffsetBottom=-164;
		var title=GetNode<Label>("Root/Panel/VBox/Title");title.Text="本局构筑";GlassUi.Typography(title,46,700);GlassUi.FrameTitle(title);
		_summary.Visible=false;
		GetNode<VBoxContainer>("Root/Panel/VBox").AddThemeConstantOverride("separation",30);
		GetNode<HBoxContainer>("Root/Panel/VBox/Content").AddThemeConstantOverride("separation",18);
		var inventory=GetNode<VBoxContainer>("Root/Panel/VBox/Content/Inventory");
		var page=GetNode<VBoxContainer>("Root/Panel/VBox/Content/Details");
		GlassUi.Background(inventory);GlassUi.Background(page);
		inventory.GetNode<Control>("Heading").Visible=false;
		var tabsMargin=new MarginContainer();inventory.AddChild(tabsMargin);inventory.MoveChild(tabsMargin,1);
		tabsMargin.AddThemeConstantOverride("margin_left",28);tabsMargin.AddThemeConstantOverride("margin_right",28);tabsMargin.AddThemeConstantOverride("margin_top",28);tabsMargin.AddThemeConstantOverride("margin_bottom",18);
		var tabs=new HBoxContainer();tabs.AddThemeConstantOverride("separation",14);tabsMargin.AddChild(tabs);
		foreach(var entry in new[]{("all","全部",""),("enhancements","强化","enhancement"),("evolutions","进化","evolution"),("mutations","异化","mutation"),("specializations","特化","specialization")})
		{
			var tab=new Button { Name="Filter_"+entry.Item1,Text=entry.Item2,CustomMinimumSize=new Vector2(entry.Item1=="all"?104:140,52),Icon=entry.Item3.Length==0?null:UiIcons.Named("Minimal/"+entry.Item3),ExpandIcon=true };
			tab.AddThemeConstantOverride("icon_max_width",34);GlassUi.Typography(tab,24,600);
			tab.AddThemeColorOverride("icon_normal_color",GlassUi.Ink);tab.AddThemeColorOverride("icon_hover_color",GlassUi.Ink);tab.AddThemeColorOverride("icon_focus_color",GlassUi.Ink);
			string category=entry.Item1;tab.Pressed+=()=>FilterCategory(category);tabs.AddChild(tab);_categoryTabs[category]=tab;
		}
		_inventory.AddThemeStyleboxOverride("panel",new StyleBoxEmpty { ContentMarginLeft=24,ContentMarginRight=24,ContentMarginBottom=28 });
		page.GetNode<ScrollContainer>("Scroll").AddThemeStyleboxOverride("panel",new StyleBoxEmpty { ContentMarginLeft=44,ContentMarginRight=36,ContentMarginTop=42,ContentMarginBottom=32 });
		details.AddThemeConstantOverride("separation",28);
		var heading=new HBoxContainer();heading.AddThemeConstantOverride("separation",30);details.AddChild(heading);details.MoveChild(heading,0);
		_detailIcon.Reparent(heading);_detailIcon.CustomMinimumSize=new Vector2(136,144);_detailIcon.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
		_name.Reparent(heading);_name.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;_name.VerticalAlignment=VerticalAlignment.Center;GlassUi.Typography(_name,40,700);
		var effectHeading=details.GetNode<Label>("EffectHeading");effectHeading.Text="效果";GlassUi.Typography(effectHeading,25,700);effectHeading.AddThemeColorOverride("font_color",new Color("175574"));
		GlassUi.Typography(_description,26);_description.AddThemeConstantOverride("line_spacing",10);
		_information.Visible=false;
	}
	private void FilterCategory(string category)
	{
		_categoryFilter=category;
		foreach(var pair in _tiles)pair.Value.Visible=category=="all"||GlassUi.CategoryKey(_items[pair.Key])==category;
		foreach(var pair in _categoryTabs) {
			var style=GlassUi.Tile(false);style.BgColor=pair.Key==category?new Color(.92f,.96f,1f,.65f):Colors.Transparent;
			style.BorderColor=pair.Key==category?Colors.White:Colors.Transparent;
			pair.Value.AddThemeStyleboxOverride("normal",style);
		}
		var first=_tiles.FirstOrDefault(pair=>pair.Value.Visible);
		_empty.Text=_items.Count>0?"当前分类暂无增幅。":"本局还没有获得增幅。";
		_empty.Visible=first.Value==null;
		if(first.Value==null) {
			ClearSelection();
			if(_items.Count>0){_name.Text="当前分类暂无增幅";_description.Text="切换其他分类查看本局已获得的增幅。";}
		} else if(!_tiles.TryGetValue(SelectedRewardId,out var selected)||!selected.Visible)Select(first.Key);
		_inventory.ScrollVertical=0;
	}
}
