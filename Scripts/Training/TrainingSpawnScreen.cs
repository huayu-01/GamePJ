using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Training;

/// <summary>训练场专用召唤面板：木桩/敌人选择、数量、精英、清空。</summary>
public partial class TrainingSpawnScreen : CanvasLayer
{
	private TrainingGroundController? _controller;
	private Control _root = null!;
	private OptionButton _count = null!;
	private CheckBox _elite = null!;
	private Label _status = null!;
	private VBoxContainer _enemyList = null!;
	private bool _built;
	private bool _enemiesPopulated;

	public override void _Ready()
	{
		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
		Build();
		Visible = false;
	}

	private void Build()
	{
		if (_built) return;
		_built = true;
		var theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres");
		_root = new Control { Name = "Root", Theme = theme };
		AddChild(_root);
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var dim = new ColorRect { Color = new Color(0, 0, 0, .72f), MouseFilter = Control.MouseFilterEnum.Stop };
		_root.AddChild(dim);
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		var panel = new PanelContainer();
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(920, 720);
		panel.Position = new Vector2(-460, -360);
		_root.AddChild(panel);
		var margin = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
		panel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 12);
		margin.AddChild(box);

		var title = new Label { Text = "训练场 · 召唤终端" };
		title.AddThemeFontSizeOverride("font_size", 30);
		box.AddChild(title);
		var hint = new Label { Text = "选择木桩或敌人 → 点击召唤。清空只删除训练召唤物；重置状态请用场内重置终端。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		box.AddChild(hint);

		var filters = new HBoxContainer();
		filters.AddThemeConstantOverride("separation", 12);
		box.AddChild(filters);
		filters.AddChild(new Label { Text = "数量" });
		_count = new OptionButton();
		foreach (string item in new[] { "1", "3", "5" }) _count.AddItem(item);
		_count.Selected = 0;
		filters.AddChild(_count);
		_elite = new CheckBox { Text = "精英（仅敌人）" };
		filters.AddChild(_elite);
		var clear = new Button { Text = "清空训练召唤物" };
		clear.Pressed += () => _controller?.ClearSpawns();
		filters.AddChild(clear);

		var dummyTitle = new Label { Text = "木桩（7 类）" };
		dummyTitle.AddThemeFontSizeOverride("font_size", 22);
		box.AddChild(dummyTitle);
		var dummyGrid = new GridContainer { Columns = 4 };
		dummyGrid.AddThemeConstantOverride("h_separation", 8);
		dummyGrid.AddThemeConstantOverride("v_separation", 8);
		box.AddChild(dummyGrid);
		foreach ((string id, string label) in new[]
		{
			("normal", "普通木桩"), ("armor", "持续刷护甲"), ("block", "正面格挡"),
			("invincible", "无敌"), ("shooter", "朝前射击"), ("area", "前方区域"),
			("group", "木桩群 ×5"),
		})
		{
			var button = new Button { Text = label, CustomMinimumSize = new Vector2(200, 40) };
			string captured = id;
			button.Pressed += () => _controller?.SpawnDummy(captured, CountValue(), _elite.ButtonPressed);
			dummyGrid.AddChild(button);
		}

		var enemyTitle = new Label { Text = "敌人（全部非全局敌种）" };
		enemyTitle.AddThemeFontSizeOverride("font_size", 22);
		box.AddChild(enemyTitle);
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		box.AddChild(scroll);
		_enemyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scroll.AddChild(_enemyList);

		_status = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		box.AddChild(_status);
		var close = new Button { Text = "关闭  Esc", CustomMinimumSize = new Vector2(0, 44) };
		close.Pressed += Close;
		box.AddChild(close);
	}

	private int CountValue() => (_count?.Selected ?? 0) switch { 1 => 3, 2 => 5, _ => 1 };

	private void PopulateEnemies()
	{
		if (_enemyList == null) return;
		foreach (Node child in _enemyList.GetChildren()) child.QueueFree();
		_enemiesPopulated = false;
		int count = 0;
		foreach (string id in ConfigRows.Ids<EnemyConfig>())
		{
			EnemyConfig? row = ConfigRows.ById<EnemyConfig>(id);
			if (row == null || row.IsGlobal) continue;
			var button = new Button { Text = $"{row.DisplayName}  ·  {row.Id}", Alignment = HorizontalAlignment.Left };
			string captured = id;
			button.Pressed += () => _controller?.SpawnEnemy(captured, CountValue(), _elite.ButtonPressed);
			_enemyList.AddChild(button);
			count++;
		}
		if (count == 0) _enemyList.AddChild(new Label { Text = "敌人配置尚未就绪，稍后重开面板。" });
		else _enemiesPopulated = true;
	}

	public void Open(TrainingGroundController controller)
	{
		_controller = controller;
		Build();
		if (!_enemiesPopulated) PopulateEnemies();
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_status!.Text = "选择木桩/敌人后点击召唤；清空按钮只删除训练召唤物。";
	}

	public void Close()
	{
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public void SetStatus(string text) { if (_status != null) _status.Text = text; }

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		GetViewport().SetInputAsHandled();
		Close();
	}
}
