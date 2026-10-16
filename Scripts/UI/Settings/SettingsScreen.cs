using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **全屏设置菜单**（用户需求第四节 1）：画面 / 控制 / 音量，分类可扩展。
///
/// 走既有 UI 框架，一层都不绕过：
/// <list type="bullet">
/// <item>继承 <see cref="OverlayScreen"/> —— 分层（<see cref="OverlayScreen.UiLayer.Panel"/>，打开即暂停并放开鼠标）、
///   防误触、开合动画、Esc 关闭、**按键提示栏**全部由基类管；</item>
/// <item>由 <see cref="UiService.OpenSettings"/> 打开（业务代码不认识本类的节点路径）；</item>
/// <item>实例挂在场景里（`Base.tscn` / `Main.tscn`），与其他界面同级。</item>
/// </list>
///
/// **分类从注册表来**（<see cref="SettingsSections"/>）：本类不写死"画面/控制/音量"这三个分支，
/// 只遍历注册表建 Tab + 一页内容 —— 加一页 = 注册一行。
/// </summary>
public partial class SettingsScreen : OverlayScreen
{
	/// <summary>设置快捷键；Esc 的路由入口也打开本页。</summary>
	[Export] public Key ToggleKey { get; set; } = Key.O;
	[Export] public Key HelpKey { get; set; } = Key.H;

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _title = null!;
	private VBoxContainer _tabs = null!;
	private VBoxContainer _page = null!;

	private readonly List<ISettingsSection> _sections = new();
	private readonly List<Button> _tabButtons = new();
	private int _active = -1;
	private bool _keyHeld;
	private bool _helpHeld;

	/// <summary>正在等待按键的动作（重绑中）；null = 不在重绑。</summary>
	private string? _awaitAction;
	private Label? _awaitLabel;
	private string _awaitPreviousText = string.Empty;

	/// <summary>注册表里有多少个分类（验证用只读属性）。</summary>
	public int SectionCount => _sections.Count;

	/// <summary>分类 id 列表（验证用只读属性，GDScript 探针可以直接读）。</summary>
	public string SectionIds
	{
		get
		{
			var ids = new List<string>(_sections.Count);
			foreach (ISettingsSection section in _sections)
			{
				ids.Add(section.Id);
			}

			return string.Join(" / ", ids);
		}
	}

	/// <summary>当前选中的分类下标（-1 = 还没选过）。</summary>
	public int ActiveSection => _active;

	/// <summary>是否正在等待玩家按键重绑。</summary>
	public bool AwaitingRebind => _awaitAction != null;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_title = GetNode<Label>("Root/Panel/VBox/Title");
		_tabs = GetNode<VBoxContainer>("Root/Panel/VBox/Body/Tabs");
		_page = GetNode<VBoxContainer>("Root/Panel/VBox/Body/PageScroll/Page");

		BuildSections();
		if (UiRouter.Instance != null)
		{
			UiRouter.Instance.OpenMenu = Open;
		}

		// 画质 / glow 是**场景里的东西**（WorldEnvironment），只能在进场景后套用；
		// 设置界面在每个场景里都有一份，所以放这里最省事、也最不容易漏（切场景自动跟上）。
		SettingsStore.ApplySceneQuality(GetTree().Root);
	}

	public override void _Process(double delta)
	{
		if (_awaitAction != null)
		{
			// 重绑中：不要用 O 把界面关掉（那时玩家按的 O 是"要绑的键"）。
			return;
		}

		bool held = Input.IsPhysicalKeyPressed(ToggleKey);
		if (held && !_keyHeld)
		{
			Toggle();
		}

		_keyHeld = held;
		bool help = Input.IsPhysicalKeyPressed(HelpKey);
		if (help && !_helpHeld)
		{
			if (IsOpen && _active >= 0 && _sections[_active].Id == "help")
			{
				Close();
			}
			else
			{
				OpenHelp();
			}
		}
		_helpHeld = help;
	}

	public override void _ExitTree()
	{
		// 菜单回调属于当前场景，切场景时不可保留已销毁的界面。
		if (UiRouter.Instance is { } router && router.OpenMenu == Open)
		{
			router.OpenMenu = null;
		}
	}

	/// <summary>旧帮助入口兼容到设置的帮助分页，不再叠一个独立弹层。</summary>
	public void OpenHelp()
	{
		Open();
		int helpIndex = _sections.FindIndex(section => section.Id == "help");
		if (helpIndex >= 0)
		{
			ShowSection(helpIndex);
		}
	}

	private void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public void Open()
	{
		if (IsOpen)
		{
			return;
		}

		CancelRebind();
		ShowSection(_active < 0 ? 0 : _active);

		SetKeyHints(
			KeyHint.Of(ToggleKey, "关闭"),
			(Key.Escape, "关闭"),
			(Key.Q, "上一类"),
			(Key.E, "下一类"));

		BeginOpen();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			CancelRebind();
			BeginClose();
		}
	}

	// ───────────────────────── 分类（注册表 → Tab + 一页）─────────────────────────

	private void BuildSections()
	{
		_sections.Clear();
		_sections.AddRange(SettingsSections.CreateAll());

		foreach (Node child in _tabs.GetChildren())
		{
			_tabs.RemoveChild(child);
			child.QueueFree();
		}

		_tabButtons.Clear();
		for (int i = 0; i < _sections.Count; i++)
		{
			int index = i;
			ISettingsSection section = _sections[i];

			// 图标代替文字的主体（icon + 短标题；纯图标会让"设置里有什么"变得要猜）。
			var tab = new Button
			{
				Text = section.Title,
				Icon = UiIcons.ForSection(section.Id),
				Alignment = HorizontalAlignment.Left,
				CustomMinimumSize = new Vector2(0, 52),
				TooltipText = $"{section.Title}（{section.Id}）",
			};
			tab.Pressed += () => ShowSection(index);
			_tabs.AddChild(tab);
			_tabButtons.Add(tab);
		}
	}

	/// <summary>切到第 <paramref name="index"/> 页（重填内容；Tab 高亮跟着走）。</summary>
	public void ShowSection(int index)
	{
		if (_sections.Count == 0)
		{
			return;
		}

		index = Mathf.Clamp(index, 0, _sections.Count - 1);
		_active = index;

		for (int i = 0; i < _tabButtons.Count; i++)
		{
			// 选中态用主按钮样式（蓝带），未选中回默认按钮样式 —— 只改一个类型变体，不新建样式。
			_tabButtons[i].ThemeTypeVariation = i == index ? new StringName("UiPrimaryButton") : (StringName)string.Empty;
		}

		foreach (Node child in _page.GetChildren())
		{
			_page.RemoveChild(child);
			child.QueueFree();
		}

		ISettingsSection section = _sections[index];
		_title.Text = $"设置 · {section.Title}";
		((ScrollContainer)_page.GetParent()).ScrollVertical = 0;
		section.Build(_page, this);
	}

	/// <summary>重填当前页（重绑完成 / 点了"恢复默认"之后调）。</summary>
	public void ShowActiveSection() => ShowSection(_active < 0 ? 0 : _active);

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || _awaitAction != null || _sections.Count == 0)
		{
			return;
		}

		// 分类切换：Q / E（不用 Tab —— Tab 在 Godot 里是控件焦点导航键）。
		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			switch (key.Keycode)
			{
				case Key.Q:
					ShowSection(_active - 1 < 0 ? _sections.Count - 1 : _active - 1);
					break;
				case Key.E:
					ShowSection((_active + 1) % _sections.Count);
					break;
			}
		}
	}

	// ───────────────────────── 按键重绑（控制页调这里）─────────────────────────

	/// <summary>进入"等待按键"状态：下一个按下的键/鼠标键会绑到 <paramref name="action"/>。</summary>
	public void RequestRebind(string action, Label valueLabel)
	{
		CancelRebind();
		_awaitAction = action;
		_awaitLabel = valueLabel;
		_awaitPreviousText = valueLabel.Text;
		valueLabel.Text = "按任意键…（Esc 取消）";
	}

	/// <summary>
	/// 重绑用 <see cref="_Input"/>（在 <see cref="UiRouter"/> 的 <c>_UnhandledInput</c> **之前**）：
	/// 这样"按 Esc 取消重绑"不会被路由当成"关闭界面"，其它键也不会漏到游戏里。
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!IsOpen || _awaitAction == null)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			if (key.Keycode == Key.Escape)
			{
				CancelRebind();
			}
			else
			{
				SettingsStore.RebindKey(_awaitAction, key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
				FinishRebind();
			}

			GetViewport().SetInputAsHandled();
		}
		else if (@event is InputEventMouseButton { Pressed: true } mouse)
		{
			SettingsStore.RebindMouse(_awaitAction, mouse.ButtonIndex);
			FinishRebind();
			GetViewport().SetInputAsHandled();
		}
	}

	private void FinishRebind()
	{
		_awaitAction = null;
		_awaitLabel = null;
		ShowActiveSection();
	}

	private void CancelRebind()
	{
		if (_awaitAction != null && _awaitLabel != null && IsInstanceValid(_awaitLabel))
		{
			_awaitLabel.Text = _awaitPreviousText;
		}

		_awaitAction = null;
		_awaitLabel = null;
	}
}
