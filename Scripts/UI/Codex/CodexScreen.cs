using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.UI;

/// <summary>书页式内容图鉴：分类、搜索、悬停选择和独立详情；只在打开或选择时刷新。</summary>
public partial class CodexScreen : OverlayScreen
{
	public const Key DefaultToggleKey = Key.J;
	protected override Control Root => _root;
	private Control _root = null!;
	private GridContainer _grid = null!;
	private VBoxContainer _categories = null!;
	private LineEdit _search = null!;
	private Label _name = null!, _description = null!, _count = null!, _chapter = null!, _empty = null!;
	private RichTextLabel _information = null!;
	private TextureRect _icon = null!;
	private Label _title = null!;
	private CodexModelPreview _preview = null!;
	private ScrollContainer _detailScroll = null!, _listScroll = null!;
	private readonly Dictionary<string, Button> _tiles = new(), _tabs = new();
	private IReadOnlyList<CodexEntry> _entries = Array.Empty<CodexEntry>();
	private List<CodexEntry> _visible = new();
	public string ActiveCategory { get; private set; } = "all";
	public string SelectedKey { get; private set; } = "";
	public int EntryCount => _entries.Count;
	public int VisibleEntryCount => _visible.Count;
	public string SelectedName => _name.Text;
	public string SelectedDescription => _description.Text;
	public bool ModelVisible => _preview.HasModel && _preview.Visible;
	public string ModelPath => _preview.CurrentModelPath;
	public LineEdit SearchBox => _search;
	public IReadOnlyList<CodexEntry> Entries => _entries;
	private static Color Ink => GlassUi.Ink;
	private static Color Gold => GlassUi.Secondary;
	private static string T(string key, string fallback) => CodexCatalog.Text(key, fallback);
	public override void _Ready()
	{
		Layer = 18;
		BuildRefinedLayout(); InitializeOverlay();
		GlassUi.StyleKeyHints(_root);
		var hintBar=_root.GetNode<Control>(KeyHintBarNodeName);hintBar.OffsetLeft=36;hintBar.OffsetRight=-36;hintBar.OffsetTop=-100;hintBar.OffsetBottom=-20;
		SetProcess(false);
	}
	private static Label Label(string text) => new() { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
	private static void FontSize(Control control, int size) => control.AddThemeFontSizeOverride("font_size", size);
	private static StyleBoxFlat Surface(bool selected = false) => GlassUi.Tile(selected);
	private static Button Button(string text)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) };
		button.AddThemeStyleboxOverride("normal", Surface()); button.AddThemeStyleboxOverride("hover", Surface(true));
		button.AddThemeStyleboxOverride("pressed", Surface(true)); button.AddThemeStyleboxOverride("focus", Surface(true));
		return button;
	}
	private void FitColumns() => _grid.Columns = Math.Max(1, (int)((_listScroll.Size.X + 12) / 228));
	public void Open()
	{
		if (IsOpen || UiRouter.Instance?.TopScreen is { } top && top is not SettingsScreen) return;
		_entries = CodexCatalog.Build();
		_title.Text = T("title", "旅途图鉴"); _search.PlaceholderText = T("search", "搜索名称、效果或前置…");
		RefreshCategories(); RefreshGrid();
		UpdateKeyHints();
		BeginOpen(); Callable.From(FitColumns).CallDeferred();
	}
	private void UpdateKeyHints()
	{
		if (_search.HasFocus()) SetKeyHints((Key.Escape,T("close","返回／关闭")),KeyHint.Text("输入","搜索"),KeyHint.Text("Tab","切换焦点"));
		else SetKeyHints(KeyHint.Of(DefaultToggleKey,T("close","关闭")),(Key.Escape,T("close","返回／关闭")),KeyHint.Text(T("direction_keys","方向键"),T("browse","浏览条目")),KeyHint.Text("悬停／点击","详情"),KeyHint.Text("滚轮","滚动"));
	}
	public override void Close()
	{
		if (!IsOpen || InputBlocked) return;
		_preview.Clear(); BeginClose();
	}
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key || (key.PhysicalKeycode != DefaultToggleKey && key.Keycode != DefaultToggleKey)) return;
		if (_search.HasFocus()) return;
		if (IsOpen) { if (UiRouter.Instance?.IsTopScreen(this) != false) Close(); }
		else if (UiRouter.Instance?.AnyOpen != true) Open();
		GetViewport().SetInputAsHandled();
	}
	public void ChooseCategory(string category)
	{
		if (!CodexCatalog.Categories.Contains(category)) return;
		ActiveCategory = category; RefreshGrid();
		foreach (var pair in _tabs) pair.Value.AddThemeStyleboxOverride("normal", SidebarStyle(pair.Key == category));
	}
	public void SetSearch(string query) { _search.Text = query; RefreshGrid(); }
	private void RefreshCategories()
	{
		foreach (Node child in _categories.GetChildren()) { _categories.RemoveChild(child); child.QueueFree(); } _tabs.Clear();
		foreach (var group in new[]{("","all"),("旅者与武器","characters,weapons,skills"),("构筑","enhancements,evolutions,mutations,specializations"),("生物","enemies,bosses,elite_traits,boss_traits"),("世界","runes,scene_runes,effects,nodes,stages,cards,summons")})
		{
			if(group.Item1.Length>0)AddSidebarHeading(group.Item1);
			foreach(string category in group.Item2.Split(',')) {
			int count = category == "all" ? _entries.Count : _entries.Count(e => e.Category == category); if (count == 0) continue;
			var tab = Button(CodexCatalog.CategoryName(category) + "  " + count); tab.Name = "Category_" + category; tab.Alignment = HorizontalAlignment.Left;tab.CustomMinimumSize=new Vector2(0,36);GlassUi.Typography(tab,22);
			tab.Icon=UiIcons.Named("Tabler/"+CategorySymbol(category));tab.ExpandIcon=true;tab.AddThemeConstantOverride("icon_max_width",26);tab.AddThemeConstantOverride("h_separation",14);
			tab.AddThemeColorOverride("icon_normal_color",Ink);tab.AddThemeColorOverride("icon_hover_color",Ink);tab.AddThemeColorOverride("icon_focus_color",Ink);
			tab.Pressed += () => { if (!InputBlocked) ChooseCategory(category); }; _categories.AddChild(tab); _tabs[category] = tab;
			foreach(string state in new[]{"normal","hover","pressed","focus"})tab.AddThemeStyleboxOverride(state,SidebarStyle(state!="normal"||category==ActiveCategory));
			}
		}
	}
	private void RefreshGrid()
	{
		foreach (Node child in _grid.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); } _tiles.Clear();
		_visible = CodexCatalog.Filter(_entries, ActiveCategory, _search.Text).ToList();
		_count.Text = string.Format(T("count", "{0} · {1} 项 / 共 {2} 项"), CodexCatalog.CategoryName(ActiveCategory), _visible.Count, _entries.Count);
		_empty.Visible = _visible.Count == 0;
		foreach (var entry in _visible)
		{
			var tile = Button(""); tile.Name = "Entry" + _tiles.Count; tile.CustomMinimumSize = new Vector2(216, 226); tile.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			tile.TooltipText = entry.Name; _grid.AddChild(tile); _tiles[entry.Key] = tile;
			GlassUi.SelectionDot(tile);
			var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; tile.AddChild(box); box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); box.OffsetLeft = box.OffsetTop = 24; box.OffsetRight = box.OffsetBottom = -24;box.AddThemeConstantOverride("separation",16);
			box.AddChild(new TextureRect { Texture = entry.Icon, CustomMinimumSize = new Vector2(104, 126), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = entry.Key.EndsWith("/1339") || entry.Key.EndsWith("enemy_scaredy_shroom") ? new Color("6e42a5") : entry.Category=="enemies" && entry.Icon?.ResourcePath.Contains("shield")==true ? new Color("235d8c") : CodexCatalog.CategoryColor(entry.Category) });
			var caption = Label(entry.Name); GlassUi.Typography(caption,23,600); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.SizeFlagsVertical = Control.SizeFlags.ExpandFill; box.AddChild(caption);
			tile.MouseEntered += () => SelectEntry(entry.Key); tile.FocusEntered += () => { SelectEntry(entry.Key); _listScroll.EnsureControlVisible(tile); }; tile.Pressed += () => SelectEntry(entry.Key);
		}
		FitColumns(); _listScroll.ScrollVertical = 0;
		if (_visible.Count > 0) SelectEntry(_visible.Any(e => e.Key == SelectedKey) ? SelectedKey : _visible[0].Key, true);
		else { SelectedKey = ""; _name.Text = T("no_selection", "暂无条目"); _chapter.Text = _description.Text = ""; _information.Text=""; _icon.Texture = null; _preview.Clear(); }
	}
	public void SelectEntry(string key, bool force = false)
	{
		if (!force && SelectedKey == key) return;
		var entry = _visible.FirstOrDefault(e => e.Key == key); if (entry == null) return;
		SelectedKey = key; _chapter.Text = CodexCatalog.CategoryName(entry.Category); _name.Text = entry.Name;
		_name.AddThemeColorOverride("font_color",entry.Category is "enhancements" or "evolutions" or "mutations" or "specializations" ? CodexCatalog.CategoryColor(entry.Category) : Ink);
		_descriptionHeading.Text=entry.Category is "enemies" or "bosses" ? "行为" : "效果";
		_description.Text = string.IsNullOrWhiteSpace(entry.Description) ? T("no_description", "详细资料待补充。") : entry.Description;
		if(entry.Category is "enemies" or "bosses")_description.Text=_description.Text.Replace("\n\n","\n");
		_information.Text = (entry.Category is "enemies" or "bosses" ? "[color=#ae5b31][b]基础资料[/b][/color]\n" : "")+GlassUi.RulesMarkup(entry.Information); _icon.Texture = entry.Icon; _icon.Modulate=CodexCatalog.CategoryColor(entry.Category); _icon.Visible = !_preview.ShowEntry(entry);
		foreach (var pair in _tiles) {pair.Value.AddThemeStyleboxOverride("normal", Surface(pair.Key == key));pair.Value.GetNode<Control>("SelectionDot").Visible=pair.Key==key;}
		_detailScroll.ScrollVertical = 0;
	}
}
