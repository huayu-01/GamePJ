using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 一个设置分类 = 设置菜单里的一页（画面 / 控制 / 音量 …）。
///
/// **可扩展是硬要求**（用户原话：「不写死，避免后面要加的时候大改动」）：
/// 加一页 = 写一个类 + 在 <see cref="SettingsSections"/> 里 <c>Register</c> 一行，
/// <see cref="SettingsScreen"/> 一行都不用改（它只遍历注册表，不知道有哪些分类）。
/// </summary>
public interface ISettingsSection
{
	/// <summary>分类 id（也是 <see cref="SettingsStore"/> 里的节名，例如 <c>graphics</c>）。</summary>
	string Id { get; }

	/// <summary>分类标题（Tab 上的字）。</summary>
	string Title { get; }

	/// <summary>把这一页的控件填进 <paramref name="page"/>（每次切换到这个分类都会重填一次）。</summary>
	void Build(VBoxContainer page, SettingsScreen screen);
}

/// <summary>
/// 分类注册表 —— 设置菜单的"目录"。**只有这里决定有哪些页**。
///
/// 自带三页（画面 / 控制 / 音量）在静态构造里注册；
/// 第三批（能力系统）要加"能力页"就 <c>SettingsSections.Register(() =&gt; new AbilitySettingsSection());</c> 一行。
/// </summary>
public static class SettingsSections
{
	/// <summary>分类工厂（每页建独立实例，互不共享状态）。</summary>
	public delegate ISettingsSection Factory();

	private static readonly List<Factory> Factories = new();

	static SettingsSections()
	{
		// 自带三页。按注册顺序排列 —— 想调顺序就调这里的顺序。
		Register(() => new GraphicsSettingsSection());
		Register(() => new ControlsSettingsSection());
		Register(() => new AudioSettingsSection());
	}

	/// <summary>注册一个分类（重复注册同一个 id 会覆盖旧的，避免热重载时冒出两页）。</summary>
	public static void Register(Factory factory)
	{
		string id = factory().Id;
		Factories.RemoveAll(existing => existing().Id == id);
		Factories.Add(factory);
	}

	/// <summary>按注册顺序造出全部分类。</summary>
	public static List<ISettingsSection> CreateAll()
	{
		var sections = new List<ISettingsSection>(Factories.Count);
		foreach (Factory factory in Factories)
		{
			sections.Add(factory());
		}

		return sections;
	}

	/// <summary>注册表里的分类 id（自检 / 文档用）。</summary>
	public static string Ids()
	{
		var ids = new List<string>(Factories.Count);
		foreach (Factory factory in Factories)
		{
			ids.Add(factory().Id);
		}

		return string.Join(" / ", ids);
	}
}

/// <summary>设置页的控件工厂 —— 三个自带分类共用，加新分类时照抄这几行即可。</summary>
public static class SettingsUi
{
	/// <summary>一行的控件区宽度（左侧标题固定这么宽，右侧控件对齐成一条线）。</summary>
	private const int LabelWidth = 220;

	/// <summary>页内小标题（**不是**标题带：外层的 Tab 与窗口标题已经说明了这是哪一页）。</summary>
	public static Label AddTitle(VBoxContainer page, string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Left };
		label.AddThemeFontSizeOverride("font_size", 22);
		page.AddChild(label);
		return label;
	}

	/// <summary>说明文字（灰）。</summary>
	public static Label AddCaption(VBoxContainer page, string text)
	{
		var label = new Label { Text = text, ThemeTypeVariation = "UiCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		page.AddChild(label);
		return label;
	}

	/// <summary>[标题] + 若干控件，横排一行。</summary>
	public static HBoxContainer AddRow(VBoxContainer page, string label, params Control[] controls)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);

		var title = new Label
		{
			Text = label,
			CustomMinimumSize = new Vector2(LabelWidth, 0),
			VerticalAlignment = VerticalAlignment.Center,
		};
		row.AddChild(title);

		foreach (Control control in controls)
		{
			control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(control);
		}

		page.AddChild(row);
		return row;
	}

	/// <summary>下拉框行。</summary>
	public static OptionButton AddOption(VBoxContainer page, string label, string[] options, int selected, Action<int> onChanged)
	{
		var option = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		foreach (string text in options)
		{
			option.AddItem(text);
		}

		option.Selected = Mathf.Clamp(selected, 0, Mathf.Max(0, options.Length - 1));
		option.ItemSelected += index => onChanged((int)index);
		AddRow(page, label, option);
		return option;
	}

	/// <summary>勾选框行。</summary>
	public static CheckBox AddCheck(VBoxContainer page, string label, bool value, Action<bool> onChanged)
	{
		var check = new CheckBox { ButtonPressed = value };
		check.Toggled += pressed => onChanged(pressed);
		AddRow(page, label, check);
		return check;
	}

	/// <summary>
	/// 滑条行：左边标题、中间滑条、右边实时数值（由 <paramref name="format"/> 决定怎么写）。
	/// </summary>
	public static HSlider AddSlider(
		VBoxContainer page,
		string label,
		float min,
		float max,
		float step,
		float value,
		Func<float, string> format,
		Action<float> onChanged)
	{
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = Mathf.Clamp(value, min, max),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 28),
		};

		var readout = new Label
		{
			Text = format((float)slider.Value),
			CustomMinimumSize = new Vector2(80, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};

		slider.ValueChanged += v =>
		{
			readout.Text = format((float)v);
			onChanged((float)v);
		};

		AddRow(page, label, slider, readout);
		return slider;
	}

	/// <summary>普通按钮行（"恢复默认"这类）。</summary>
	public static Button AddButton(VBoxContainer page, string label, string text, Action onPressed)
	{
		var button = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		button.Pressed += onPressed;
		AddRow(page, label, button);
		return button;
	}
}

/// <summary>【画面】分辨率 / 全屏 / 垂直同步 / 帧率 / 画质 / glow。</summary>
public sealed class GraphicsSettingsSection : ISettingsSection
{
	private static readonly (string Name, int Width, int Height)[] Resolutions =
	{
		("1280 × 720", 1280, 720),
		("1600 × 900", 1600, 900),
		("1920 × 1080", 1920, 1080),
		("2560 × 1440", 2560, 1440),
		("3840 × 2160", 3840, 2160),
	};

	public string Id => SettingsStore.GraphicsSection;

	public string Title => "画面";

	public void Build(VBoxContainer page, SettingsScreen screen)
	{
		SettingsUi.AddTitle(page, "画面");

		SettingsUi.AddOption(
			page,
			"显示模式",
			new[] { "窗口化", "全屏", "无边框全屏" },
			SettingsStore.WindowMode,
			SettingsStore.SetWindowMode);

		var resolutionNames = new string[Resolutions.Length];
		for (int i = 0; i < Resolutions.Length; i++)
		{
			resolutionNames[i] = Resolutions[i].Name;
		}

		Vector2I window = DisplayServer.WindowGetSize();
		SettingsUi.AddOption(page, "分辨率（窗口化时生效）", resolutionNames, ClosestResolution(window), index =>
		{
			SettingsStore.SetResolution(Resolutions[index].Width, Resolutions[index].Height);
		});

		SettingsUi.AddCheck(page, "垂直同步", SettingsStore.GetBool(SettingsStore.GraphicsSection, SettingsStore.KeyVsync, true), SettingsStore.SetVsync);

		SettingsUi.AddSlider(
			page,
			"最大帧率",
			30f,
			240f,
			10f,
			Mathf.Max(30, SettingsStore.GetInt(SettingsStore.GraphicsSection, SettingsStore.KeyMaxFps, Engine.MaxFps > 0 ? Engine.MaxFps : 240)),
			value => $"{value:0} FPS",
			value => SettingsStore.SetMaxFps((int)value));

		SettingsUi.AddOption(page, "画质预设", new[] { "低（省电：渲染 75%）", "中（默认）", "高（2× MSAA）" }, SettingsStore.QualityLevel, quality =>
		{
			SettingsStore.SetQuality(quality);
			SettingsStore.ApplySceneQuality(screen.GetTree().Root);
		});

		SettingsUi.AddCheck(page, "辉光 glow", SettingsStore.GlowEnabled, enabled =>
		{
			SettingsStore.SetGlow(enabled);
			SettingsStore.ApplySceneQuality(screen.GetTree().Root);
		});

		SettingsUi.AddSlider(page, "glow 强度", 0f, 2f, 0.05f, SettingsStore.GlowGain, value => $"{value * 100f:0}%", value =>
		{
			SettingsStore.SetGlowGain(value);
			SettingsStore.ApplySceneQuality(screen.GetTree().Root);
		});

		SettingsUi.AddCaption(page, "画质只改 MSAA / 3D 缩放与 glow 开关，不动场景文件里其它渲染参数（灯光与后处理由风格渲染负责）。");
	}

	/// <summary>窗口尺寸最接近的候选（当前 1920×1080 就选中第 3 项）。</summary>
	private static int ClosestResolution(Vector2I window)
	{
		int best = 2;
		int bestDistance = int.MaxValue;
		for (int i = 0; i < Resolutions.Length; i++)
		{
			int distance = Mathf.Abs(Resolutions[i].Width - window.X) + Mathf.Abs(Resolutions[i].Height - window.Y);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = i;
			}
		}

		return best;
	}
}

/// <summary>
/// 【控制】列出当前键位并支持重绑。
/// 只列 <c>project.godot</c> 的**玩法动作**；<c>ui_*</c> 内置动作不列（把 Esc 绑掉会把自己锁在界面里）。
/// </summary>
public sealed class ControlsSettingsSection : ISettingsSection
{
	/// <summary>（动作名, 中文名, 默认键）。默认键 = <c>project.godot</c> 里那一行的值，供"恢复默认"用。</summary>
	private static readonly (string Action, string Name, Key Default)[] KeyActions =
	{
		("move_forward", "前进", Key.W),
		("move_back", "后退", Key.S),
		("move_left", "左移", Key.A),
		("move_right", "右移", Key.D),
		("jump", "跳跃", Key.Space),
		// ⚠️ P6a：`dash`（战术滑铲 Q）/ `burst`（火力倾泻 E）/ `control`（操控召唤物 V）/ `summon`（召唤 G）
		// 四条旧角色的按键动作已随旧角色从 `project.godot` 的 [input] 里删除 —— 列在这里会让
		// "重绑"那一行去读一个不存在的动作（`CurrentKeys` 会显示"InputMap 里没有这个动作"）。
		("skill_1", "技能 1", Key.Q),
		("skill_2", "技能 2", Key.E),
		("fall_attack", "下落攻击", Key.Ctrl),
		("reload", "装填", Key.R),
		("interact", "互动（节点 / Boss 门）", Key.F),
	};

	/// <summary>（动作名, 中文名, 默认鼠标键）。</summary>
	private static readonly (string Action, string Name, MouseButton Default)[] MouseActions =
	{
		("fire", "开火", MouseButton.Left),
		("aim", "瞄准", MouseButton.Right),
	};

	public string Id => SettingsStore.ControlsSection;

	public string Title => "控制";

	public void Build(VBoxContainer page, SettingsScreen screen)
	{
		SettingsUi.AddTitle(page, "控制");

		foreach ((string Action, string Name, Key Default) row in KeyActions)
		{
			AddRebindRow(page, screen, row.Action, row.Name);
		}

		foreach ((string Action, string Name, MouseButton Default) row in MouseActions)
		{
			AddRebindRow(page, screen, row.Action, row.Name);
		}

		SettingsUi.AddButton(page, "全部", "恢复默认键位", () =>
		{
			RestoreDefaults();
			screen.ShowActiveSection();
		});

		SettingsUi.AddCaption(page, "重绑只写入 user://settings.cfg（项目文件不动）；点「重绑」后按任意键，Esc 取消。");
	}

	private static void AddRebindRow(VBoxContainer page, SettingsScreen screen, string action, string name)
	{
		var current = new Label
		{
			Text = CurrentKeys(action),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};

		var rebind = new Button { Text = "重绑", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
		rebind.Pressed += () => screen.RequestRebind(action, current);

		SettingsUi.AddRow(page, name, current, rebind);
	}

	/// <summary>一个动作当前绑的键（多个用 + 连起来）。</summary>
	public static string CurrentKeys(string action)
	{
		if (!InputMap.HasAction(action))
		{
			return "（InputMap 里没有这个动作）";
		}

		Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
		if (events.Count == 0)
		{
			return "（未绑定）";
		}

		var parts = new List<string>(events.Count);
		foreach (InputEvent @event in events)
		{
			parts.Add(@event is InputEventMouseButton mouse ? KeyHint.MouseText(mouse.ButtonIndex) : @event.AsText());
		}

		return string.Join(" / ", parts);
	}

	/// <summary>恢复 <c>project.godot</c> 的默认键位（并清掉存档里的重绑记录）。</summary>
	public static void RestoreDefaults()
	{
		foreach ((string Action, string Name, Key Default) row in KeyActions)
		{
			InputMap.ActionEraseEvents(row.Action);
			InputMap.ActionAddEvent(row.Action, new InputEventKey { PhysicalKeycode = row.Default });
			SettingsStore.ClearRebind(row.Action);
		}

		foreach ((string Action, string Name, MouseButton Default) row in MouseActions)
		{
			InputMap.ActionEraseEvents(row.Action);
			InputMap.ActionAddEvent(row.Action, new InputEventMouseButton { ButtonIndex = row.Default });
			SettingsStore.ClearRebind(row.Action);
		}
	}
}

/// <summary>
/// 【音量】四条总线（Master / Music / SFX / UI）各一条滑条 + 静音。
/// **不写死总线名**：循环 <c>AudioServer.BusCount</c>，以后在 <c>default_bus_layout.tres</c> 里加一条总线，
/// 这一页自动多一组控件（只有中文名要在这张表里补一行）。
/// </summary>
public sealed class AudioSettingsSection : ISettingsSection
{
	public string Id => SettingsStore.AudioSection;

	public string Title => "音量";

	public void Build(VBoxContainer page, SettingsScreen screen)
	{
		SettingsUi.AddTitle(page, "音量");

		var names = new List<string>(AudioServer.BusCount);
		for (int i = 0; i < AudioServer.BusCount; i++)
		{
			int index = i;
			string bus = AudioServer.GetBusName(i);
			string label = BusLabel(bus);
			names.Add($"{bus}（{label}）");

			SettingsUi.AddSlider(page, label, 0f, 100f, 1f, SettingsStore.BusVolumePercent(index) * 100f, value => $"{value:0}%", value =>
				SettingsStore.SetBusVolume(index, value / 100f, SettingsStore.BusMuted(index)));

			SettingsUi.AddCheck(page, $"{label} · 静音", SettingsStore.BusMuted(index), muted =>
				SettingsStore.SetBusVolume(index, SettingsStore.BusVolumePercent(index), muted));
		}

		SettingsUi.AddButton(page, "全部", "恢复默认音量", () =>
		{
			for (int i = 0; i < AudioServer.BusCount; i++)
			{
				SettingsStore.SetBusVolume(i, 1f, false);
			}

			screen.ShowActiveSection();
		});

		SettingsUi.AddCaption(page, $"总线直接读 AudioServer（现在 {AudioServer.BusCount} 条：{string.Join("、", names)}）—— 加总线不用改这一页的代码。");
	}

	private static string BusLabel(string bus) => bus switch
	{
		"Master" => "总音量",
		"Music" => "音乐",
		"SFX" => "音效",
		"UI" => "界面音效",
		_ => bus,
	};
}
