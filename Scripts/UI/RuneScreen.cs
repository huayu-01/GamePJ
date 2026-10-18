using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>基地符文配置：表驱动目录、预览与确认携带；局内不允许换装。</summary>
public partial class RuneScreen : OverlayScreen
{
	private Control _root = null!;
	private VBoxContainer _list = null!;
	private Label _title = null!;
	private Label _description = null!;
	private Label _unlocks = null!;
	private Label _current = null!;
	private TextureRect _icon = null!;
	private Button _equip = null!;
	private readonly List<RuneConfig?> _rows = new();
	private readonly List<Button> _buttons = new();
	private int _preview;
	protected override Control Root => _root;
	public int OptionCount => _rows.Count;
	public string PreviewId => _rows.ElementAtOrDefault(_preview)?.Id ?? string.Empty;
	public override void _Ready()
	{
		Layer = 11;
		_root = new Control { Name = "Root", Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
		AddChild(_root);
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var dim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.92f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(dim);
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var panel = new PanelContainer();
		_root.AddChild(panel);
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.OffsetLeft = 32; panel.OffsetRight = -32; panel.OffsetTop = 24; panel.OffsetBottom = -76;
		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 16);
		panel.AddChild(body);
		body.AddChild(new Label { Text = "符文 · 出击信物", ThemeTypeVariation = "UiTitle" });
		_current = new Label(); body.AddChild(_current);
		var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		columns.AddThemeConstantOverride("separation", 24); body.AddChild(columns);
		var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		columns.AddChild(scroll);
		_list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 8); scroll.AddChild(_list);
		var detailsScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		columns.AddChild(detailsScroll);
		var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		details.AddThemeConstantOverride("separation", 20); detailsScroll.AddChild(details);
		_icon = new TextureRect { CustomMinimumSize = new Vector2(100, 100), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
		details.AddChild(_icon);
		_title = new Label { ThemeTypeVariation = "UiTitle" }; details.AddChild(_title);
		_description = Wrapped(); details.AddChild(_description);
		_unlocks = Wrapped(); details.AddChild(_unlocks);
		_equip = new Button { Name = "Equip", CustomMinimumSize = new Vector2(0, 60) };
		_equip.Pressed += EquipPreview; body.AddChild(_equip);
		var close = new Button { Text = "返回基地" }; close.Pressed += Close; body.AddChild(close);
		InitializeOverlay();
		SetKeyHints(KeyHint.Text("↑ / ↓", "浏览符文"), KeyHint.Text("Enter / 手柄确认", "携带"), (Key.Escape, "返回"));
	}
	private static Label Wrapped() => new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
	public void Open()
	{
		if (IsOpen) return;
		foreach (Node child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
		_rows.Clear(); _buttons.Clear(); _rows.Add(null);
		_rows.AddRange(ConfigIndex.Settings.Runes.Where(r => r != null).OrderBy(r => r.Slot));
		for (int i = 0; i < _rows.Count; i++)
		{
			int index = i; RuneConfig? row = _rows[i];
			var button = new Button { Text = row?.DisplayName ?? "不携带符文", Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 62), Icon = Icon(row), ExpandIcon = true };
			button.AddThemeConstantOverride("icon_max_width", 42);
			button.FocusEntered += () => Preview(index);
			button.Pressed += () => { if (!InputBlocked) { Preview(index); EquipPreview(); } };
			_list.AddChild(button); _buttons.Add(button);
		}
		_preview = Mathf.Max(0, _rows.FindIndex(r => r?.Id == RuneCarry.Current));
		BeginOpen(); Preview(_preview); _buttons[_preview].GrabFocus();
	}
	private static Texture2D? Icon(RuneConfig? row) => string.IsNullOrEmpty(row?.IconPath) ? null : RuntimeResources.Load<Texture2D>(row.IconPath);
	private void Preview(int index)
	{
		_preview = index;
		RuneConfig? row = _rows[index];
		_title.Text = row?.DisplayName ?? "不携带符文";
		_icon.Texture = Icon(row);
		_description.Text = row == null ? "以当前角色和武器的基础配置出击。" : RewardEffectRegistry.Compile(row)?.Description ?? row.DescriptionTemplate;
		var tokens = new HashSet<string>(row?.UnlockList() ?? System.Array.Empty<string>());
		if (row != null) tokens.Add(row.Id);
		string[] names = ConfigIndex.Settings.Upgrades.Where(r => r != null && r.Requires.Any(tokens.Contains)).Select(r => r.DisplayName).ToArray();
		_unlocks.Text = names.Length == 0 ? "没有额外的特化前置解锁。" : "可发现的特化增幅\n" + string.Join(" · ", names);
		_current.Text = $"当前携带：{RuneCarry.Row?.DisplayName ?? "不携带"}　·　每次出击只能携带一枚";
		bool carried = PreviewId == RuneCarry.Current;
		_equip.Text = carried ? "✓ 已携带" : row == null ? "不携带符文" : "携带";
		_equip.Disabled = carried;
		for (int i = 0; i < _buttons.Count; i++) _buttons[i].Modulate = (_rows[i]?.Id ?? "") == RuneCarry.Current ? new Color(0.46f, 0.93f, 0.78f) : Colors.White;
	}
	public void EquipPreview()
	{
		if (!IsOpen || InputBlocked || PreviewId == RuneCarry.Current) return;
		RuneCarry.Set(PreviewId); Preview(_preview);
	}
	public override void Close() { if (IsOpen && !InputBlocked) BeginClose(); }
}
