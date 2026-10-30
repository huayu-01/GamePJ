using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Rewards;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.UI;

/// <summary>左半库存、右半完整详情；内容改变才重建，保留悬停与键盘选择。</summary>
public partial class BuildScreen : OverlayScreen
{
	public const Key DefaultToggleKey = Key.C;
	[Export] public Key ToggleKey { get; set; } = DefaultToggleKey;
	protected override Control Root => _root;
	private Control _root = null!;
	private Label _summary = null!;
	private GridContainer _grid = null!;
	private ScrollContainer _inventory = null!;
	private Label _empty = null!;
	private Label _name = null!;
	private Label _description = null!;
	private Label _information = null!;
	private TextureRect _detailIcon = null!;
	private readonly Dictionary<string, Button> _tiles = new();
	private readonly Dictionary<string, RewardDefinition> _items = new();
	private RunBuild? _renderedBuild;
	private string _signature = "uninitialized";
	private bool _keyHeld;
	private bool _debugSample;
	private string _textRevision = string.Empty;
	public int TileCount => _tiles.Count;
	public string SelectedRewardId { get; private set; } = string.Empty;
	public string SelectedName => _name.Text;
	public string SelectedDescription => _description.Text;
	public string SelectedInformation => _information.Text;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		GlassUi.Prepare(_root);
		var panel = GetNode<PanelContainer>("Root/Panel"); GlassUi.EmptyPanel(panel);
		GetNode<Control>("Root/Panel/VBox/Divider").Visible = false;
		_summary = GetNode<Label>("Root/Panel/VBox/Summary");
		_inventory = GetNode<ScrollContainer>("Root/Panel/VBox/Content/Inventory/Scroll");
		_grid = _inventory.GetNode<GridContainer>("Grid");
		_empty = GetNode<Label>("Root/Panel/VBox/Content/Inventory/Empty");
		var details = GetNode<VBoxContainer>("Root/Panel/VBox/Content/Details/Scroll/Sections");
		_name = details.GetNode<Label>("Name");
		_description = details.GetNode<Label>("Description");
		_information = details.GetNode<Label>("Information");
		_detailIcon = details.GetNode<TextureRect>("Icon");
		details.GetNode<Control>("NameHeading").Visible = false;
		details.GetNode<Control>("InformationHeading").Visible = false;
		details.GetNode<Control>("Divider2").Visible = false;
		RefineLayout(details);
	}

	public override void _Process(double delta)
	{
		bool held = Input.IsPhysicalKeyPressed(ToggleKey);
		if (held && !_keyHeld)
		{
			if (IsOpen && (UiRouter.Instance == null || UiRouter.Instance.IsTopScreen(this))) Close();
			else if (UiRouter.Instance?.AnyOpen != true) Open();
		}
		_keyHeld = held;
		if (!IsOpen) return;
		Refresh();
		_grid.Columns = Mathf.Max(1, Mathf.FloorToInt((_inventory.Size.X - 48f + 12f) / 136f));
	}

	public void Open()
	{
		if (IsOpen) return;
		Refresh();
		SetKeyHints(KeyHint.Of(ToggleKey, "关闭"), (Key.Escape, "关闭"), KeyHint.Text("方向键", "浏览 / 查看详情"),KeyHint.Text("悬停","查看详情"), KeyHint.Text("滚轮","滚动"));
		BeginOpen();
		if (_tiles.TryGetValue(SelectedRewardId, out Button? tile)) tile.GrabFocus();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked) BeginClose();
	}

	private void Refresh()
	{
		if (_textRevision != ContentText.Revision)
		{
			_textRevision = ContentText.Revision;
			foreach (var tile in _tiles) if (_items.TryGetValue(tile.Key, out var row)) tile.Value.TooltipText = _renderedBuild?.DisplayName(row) ?? row.Name;
			RestoreSelection();
		}
		if (_debugSample) return;
		RunBuild? build = RunController.Instance?.Build;
		string signature = build == null ? string.Empty : string.Join("|", build.Acquired.Select(row => row.Id + (row.ShowUpgradeLevel ? ":" + build.StacksOf(row.Id) : "")))
			+ "#" + string.Join("|", build.RuneUnlocks) + "#" + ContentText.Revision;
		if (ReferenceEquals(build, _renderedBuild) && signature == _signature) return;
		_renderedBuild = build;
		_signature = signature;
		ClearTiles();
		if (build == null)
		{
			_summary.Text = "进入关卡后，这里显示本局获得的全部构筑。";
			_empty.Visible = true;
			ClearSelection();
			return;
		}
		foreach (RewardDefinition row in build.Acquired) AddTile(row);
		_summary.Text = $"本局持有 {build.Acquired.Count} 项增幅";
		_empty.Visible = build.Acquired.Count == 0;
		RestoreSelection();
		FilterCategory(_categoryFilter);
	}

	private void ClearTiles()
	{
		foreach (Node child in _grid.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); }
		_tiles.Clear();
		_items.Clear();
	}

	private void AddTile(RewardDefinition row)
	{
		var tile = new Button
		{
			Name = row.Id, CustomMinimumSize = new Vector2(124, 132), Icon = UiIcons.ForReward(row),
			ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, FocusMode = Control.FocusModeEnum.All,
			TooltipText = _renderedBuild?.DisplayName(row) ?? row.Name,
		};
		tile.AddThemeConstantOverride("icon_max_width", 82);
		foreach(string state in new[]{"icon_normal_color","icon_hover_color","icon_focus_color","icon_pressed_color"})tile.AddThemeColorOverride(state,GlassUi.RewardColor(row));
		tile.AddThemeStyleboxOverride("normal", TileStyle(false));
		tile.AddThemeStyleboxOverride("hover", TileStyle(true));
		tile.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		tile.AddThemeStyleboxOverride("pressed", TileStyle(true));
		tile.MouseEntered += () => Select(row.Id);
		tile.FocusEntered += () => Select(row.Id);
		tile.Pressed += () => Select(row.Id);
		_tiles[row.Id] = tile;
		_items[row.Id] = row;
		_grid.AddChild(tile);
		GlassUi.SelectionDot(tile);
		var category = new TextureRect { Name="CategoryIcon", Texture=GlassUi.CategoryIcon(row), MouseFilter=Control.MouseFilterEnum.Ignore,
			ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered, Modulate=GlassUi.RewardColor(row) };
		tile.AddChild(category); category.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		category.OffsetLeft=-24; category.OffsetTop=5; category.OffsetRight=-5; category.OffsetBottom=24;
	}

	private static StyleBoxFlat TileStyle(bool selected) => GlassUi.Tile(selected);

	private void Select(string id)
	{
		if (!_items.TryGetValue(id, out var item)) return;
		SelectedRewardId = id;
		// 悬停选择同时移动键盘焦点，避免焦点仍留在旧格时再选旧格却不触发FocusEntered。
		if (IsOpen && (UiRouter.Instance == null || UiRouter.Instance.IsTopScreen(this)) && _tiles.TryGetValue(id, out Button? selected) && !selected.HasFocus()) selected.GrabFocus();
		foreach (var pair in _tiles) {pair.Value.AddThemeStyleboxOverride("normal", TileStyle(pair.Key == id));pair.Value.GetNode<Control>("SelectionDot").Visible=pair.Key==id;}
		_name.Text = _renderedBuild?.DisplayName(item) ?? item.Name;
		_name.AddThemeColorOverride("font_color",GlassUi.RewardColor(item));
		_description.Text = item.Description;
		_information.Text = string.Empty;
		_detailIcon.Texture = UiIcons.ForReward(item);
		_detailIcon.Modulate=GlassUi.RewardColor(item);
	}

	private void RestoreSelection()
	{
		if (_items.Count == 0) { ClearSelection(); return; }
		Select(_items.ContainsKey(SelectedRewardId) ? SelectedRewardId : _items.Keys.First());
	}

	private void ClearSelection()
	{
		SelectedRewardId = string.Empty;
		_name.Text = "尚未获得增幅";
		_name.AddThemeColorOverride("font_color",GlassUi.Ink);
		_description.Text = "获得增幅后，悬停或用方向键选择左侧格子。";
		_information.Text = string.Empty;
		_detailIcon.Texture = null;
	}

	public void DebugShowSample(int count = 12)
	{
		_debugSample = true;
		ClearTiles();
		foreach (RewardDefinition row in RewardCatalog.All.Take(count)) AddTile(row);
		_summary.Text = $"示例：真实奖励目录中的 {TileCount} 条";
		_empty.Visible = TileCount == 0;
		RestoreSelection();
	}
}
