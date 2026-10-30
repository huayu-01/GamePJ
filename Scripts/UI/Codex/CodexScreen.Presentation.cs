using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.UI;

public partial class CodexScreen
{
	private Label _descriptionHeading=null!;
	private static StyleBoxFlat SidebarStyle(bool selected)
	{
		var style=GlassUi.Tile(selected);if(!selected){style.BgColor=Colors.Transparent;style.BorderWidthLeft=style.BorderWidthRight=style.BorderWidthTop=style.BorderWidthBottom=0;}
		style.ContentMarginLeft=18;style.ContentMarginRight=12;style.ContentMarginTop=0;style.ContentMarginBottom=0;return style;
	}
	private static string CategorySymbol(string category)=>category switch {
		"characters"=>"user","weapons"=>"sword","skills"=>"snowflake","enhancements"=>"arrow-up","evolutions"=>"diamond","mutations"=>"hexagons","specializations"=>"sparkles",
		"enemies"=>"skull","bosses" or "boss_traits"=>"crown","elite_traits"=>"shield-half","runes" or "scene_runes"=>"spiral","effects"=>"hexagons","nodes"=>"message-circle","stages"=>"flag","cards"=>"leaf","summons"=>"cube",_=>"book-2"
	};
	private void AddSidebarHeading(string text)
	{
		_categories.AddChild(new HSeparator { CustomMinimumSize=new Vector2(0,10) });
		var heading=Label(text);GlassUi.Typography(heading,23,700);_categories.AddChild(heading);
	}
	private static MarginContainer Inset(Control host,int padding)
	{
		var inset=new MarginContainer();host.AddChild(inset);foreach(string side in new[]{"left","right","top","bottom"})inset.AddThemeConstantOverride("margin_"+side,padding);return inset;
	}
	private void BuildRefinedLayout()
	{
		_root=new Control { Name="Root" };AddChild(_root);_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);GlassUi.Prepare(_root);
		var margin=new MarginContainer { Name="Book" };_root.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left",36);margin.AddThemeConstantOverride("margin_right",36);margin.AddThemeConstantOverride("margin_top",26);margin.AddThemeConstantOverride("margin_bottom",114);
		var book=new VBoxContainer();book.AddThemeConstantOverride("separation",16);margin.AddChild(book);
		var header=new HBoxContainer();book.AddChild(header);header.AddChild(new Control { CustomMinimumSize=new Vector2(84,0) });_title=Label(T("title","旅途图鉴"));GlassUi.Typography(_title,42,700);_title.AddThemeColorOverride("font_color",GlassUi.Paper);_title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;header.AddChild(_title);
		var close=Button("");close.Icon=UiIcons.Named("Tabler/x");close.ExpandIcon=true;close.AddThemeConstantOverride("icon_max_width",24);close.CustomMinimumSize=new Vector2(48,48);close.Pressed+=Close;header.AddChild(close);
		var body=new HBoxContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill };body.AddThemeConstantOverride("separation",14);book.AddChild(body);
		var navPanel=new PanelContainer { CustomMinimumSize=new Vector2(306,0) };body.AddChild(navPanel);GlassUi.Background(navPanel);
		var nav=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };Inset(navPanel,18).AddChild(nav);
		_categories=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };_categories.AddThemeConstantOverride("separation",2);nav.AddChild(_categories);
		var middlePanel=new PanelContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,SizeFlagsStretchRatio=1f };body.AddChild(middlePanel);GlassUi.Background(middlePanel);
		var middle=new VBoxContainer();middle.AddThemeConstantOverride("separation",20);Inset(middlePanel,30).AddChild(middle);
		var searchPanel=new PanelContainer();searchPanel.AddThemeStyleboxOverride("panel",GlassUi.Tile(false));middle.AddChild(searchPanel);
		var searchRow=new HBoxContainer();searchRow.AddThemeConstantOverride("separation",12);searchPanel.AddChild(searchRow);searchRow.AddChild(new TextureRect { Texture=UiIcons.Named("Tabler/search"),CustomMinimumSize=new Vector2(28,28),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Modulate=Ink });
		_search=new LineEdit { Name="Search",PlaceholderText=T("search","搜索名称、效果或前置…"),ClearButtonEnabled=true,CustomMinimumSize=new Vector2(0,36),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };GlassUi.Typography(_search,22);searchRow.AddChild(_search);
		foreach(string state in new[]{"normal","focus","read_only"})_search.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
		_search.TextChanged+=_=>RefreshGrid();_search.FocusEntered+=UpdateKeyHints;_search.FocusExited+=UpdateKeyHints;
		_count=Label("");GlassUi.Typography(_count,20);middle.AddChild(_count);
		_listScroll=new ScrollContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };middle.AddChild(_listScroll);
		_grid=new GridContainer { Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };_grid.AddThemeConstantOverride("h_separation",12);_grid.AddThemeConstantOverride("v_separation",12);_listScroll.AddChild(_grid);_listScroll.Resized+=FitColumns;
		_empty=Label(T("empty","没有匹配的条目，试试其他关键词。"));middle.AddChild(_empty);
		var page=new PanelContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,SizeFlagsStretchRatio=1f };body.AddChild(page);GlassUi.Background(page);
		_detailScroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };Inset(page,32).AddChild(_detailScroll);
		var detail=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };detail.AddThemeConstantOverride("separation",8);_detailScroll.AddChild(detail);
		_chapter=Label("");GlassUi.Typography(_chapter,20,600);_chapter.AddThemeColorOverride("font_color",new Color("9c5333"));detail.AddChild(_chapter);
		_name=Label("");GlassUi.Typography(_name,42,700);detail.AddChild(_name);
		var hero=new Control { CustomMinimumSize=new Vector2(0,244) };detail.AddChild(hero);
		_icon=new TextureRect { ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore };hero.AddChild(_icon);_icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);_icon.OffsetLeft=_icon.OffsetTop=28;_icon.OffsetRight=_icon.OffsetBottom=-28;
		_preview=new CodexModelPreview();hero.AddChild(_preview);_preview.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		detail.AddChild(new HSeparator());_descriptionHeading=Label("效果");GlassUi.Typography(_descriptionHeading,25,700);detail.AddChild(_descriptionHeading);
		_description=Label("");GlassUi.Typography(_description,25);_description.AddThemeConstantOverride("line_spacing",10);detail.AddChild(_description);
		detail.AddChild(new HSeparator());
		_information=new RichTextLabel { BbcodeEnabled=true,FitContent=true,ScrollActive=false,MouseFilter=Control.MouseFilterEnum.Ignore,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };_information.AddThemeFontSizeOverride("normal_font_size",24);_information.AddThemeFontOverride("normal_font",GlassUi.Weight(500));_information.AddThemeFontOverride("bold_font",GlassUi.Weight(700));detail.AddChild(_information);
	}
}
