using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 设置的持久化（<c>user://settings.cfg</c>，Godot 原生 <see cref="ConfigFile"/>）。
///
/// **为什么不进 <c>Config/Tables/</c>**：那是"策划数值表"（要跟着版本走、要进仓库），
/// 而设置是**玩家机器上的东西**：分辨率 / 音量 / 按键重绑只对他自己有意义。
/// 这条边界就是 <c>user://</c> 与 <c>res://Config/</c> 的分工，和 <c>user://meta.cfg</c>（局外成长）一致。
///
/// 三个启动应用点（<see cref="ApplyOnStartup"/> 由 <see cref="UiRouter"/> 在 autoload 阶段调一次）：
/// 音量（AudioServer 四条总线）、画面（窗口模式 / 分辨率 / 垂直同步 / 最大帧率）、按键重绑（InputMap）。
/// **渲染画质**要等场景树有 WorldEnvironment 才能套用，所以走 <see cref="ApplySceneQuality"/>（界面 _Ready 时调）。
/// </summary>
public static class SettingsStore
{
	public const string FilePath = "user://settings.cfg";

	// 节名（= 分类 id，与设置菜单的分类一一对应；不写死取值的只有"有哪些键"）。
	public const string GraphicsSection = "graphics";
	public const string ControlsSection = "input";
	public const string AudioSection = "audio";

	// 画面键。
	public const string KeyWindowMode = "window_mode";
	public const string KeyWidth = "width";
	public const string KeyHeight = "height";
	public const string KeyVsync = "vsync";
	public const string KeyMaxFps = "max_fps";
	public const string KeyQuality = "quality";
	public const string KeyGlow = "glow";
	public const string KeyGlowGain = "glow_gain";

	private static ConfigFile? _file;
	private static readonly Dictionary<ulong, float> _baseGlowIntensity = new();

	private static ConfigFile Cfg
	{
		get
		{
			if (_file != null)
			{
				return _file;
			}

			_file = new ConfigFile();
			Error error = _file.Load(FilePath);
			if (error != Error.Ok && error != Error.FileNotFound)
			{
				GameLog.Write("UI", $"设置文件 {FilePath} 读取失败（{error}）—— 本次运行全部用默认值，不会覆盖它");
				_file = new ConfigFile();
			}

			return _file;
		}
	}

	/// <summary>玩家改过设置吗（自检与文档都读它）。</summary>
	public static bool Exists() => System.IO.File.Exists(ProjectSettings.GlobalizePath(FilePath));

	/// <summary>取一个浮点值；没存过就返回 <paramref name="fallback"/>。</summary>
	public static float GetFloat(string section, string key, float fallback)
		=> (float)Cfg.GetValue(section, key, fallback).AsDouble();

	/// <summary>取一个整数值；没存过就返回 <paramref name="fallback"/>。</summary>
	public static int GetInt(string section, string key, int fallback)
		=> Cfg.GetValue(section, key, fallback).AsInt32();

	/// <summary>取一个布尔值；没存过就返回 <paramref name="fallback"/>。</summary>
	public static bool GetBool(string section, string key, bool fallback)
		=> Cfg.GetValue(section, key, fallback).AsBool();

	/// <summary>某个键存过没有（用它决定"要不要动系统设置"）。</summary>
	public static bool Has(string section, string key) => Cfg.HasSectionKey(section, key);

	/// <summary>写一个值并立刻落盘（设置是低频操作，直接存最简单）。</summary>
	public static void Set(string section, string key, Variant value)
	{
		Cfg.SetValue(section, key, value);
		Error error = Cfg.Save(FilePath);
		if (error != Error.Ok)
		{
			GameLog.Write("UI", $"设置保存失败（{error}）：{section}/{key}");
		}
	}

	/// <summary>清掉一个键（"恢复默认"用它）。</summary>
	public static void Erase(string section, string key)
	{
		Cfg.EraseSectionKey(section, key);
		Cfg.Save(FilePath);
	}

	/// <summary>启动时的总应用点（由 <see cref="UiRouter._Ready"/> 调一次）。</summary>
	public static void ApplyOnStartup()
	{
		ApplyAudio();
		ApplyGraphics();
		ApplyInput();
		GameLog.Write("UI", $"设置：{DescribeStartup()}");
	}

	// ───────────────────────── 音量（4 条总线，按 AudioServer 里真实存在的枚举，不写死三条）─────────────────────────

	/// <summary>音量以**百分比**存（0~1），套用时转成分贝 —— 玩家看得懂的是百分比。</summary>
	public static void SetBusVolume(int busIndex, float percent, bool mute)
	{
		string name = AudioServer.GetBusName(busIndex);
		AudioServer.SetBusVolumeDb(busIndex, percent <= 0.0001f ? -80f : Mathf.LinearToDb(percent));
		AudioServer.SetBusMute(busIndex, mute);
		Set(AudioSection, BusVolumeKey(name), percent);
		Set(AudioSection, BusMuteKey(name), mute);
	}

	/// <summary>某条总线当前的音量百分比（没存过就读系统的当前值 —— 界面显示的就是"真实生效的值"）。</summary>
	public static float BusVolumePercent(int busIndex)
	{
		string name = AudioServer.GetBusName(busIndex);
		if (Has(AudioSection, BusVolumeKey(name)))
		{
			return GetFloat(AudioSection, BusVolumeKey(name), 1f);
		}

		return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex));
	}

	/// <summary>某条总线当前是否静音。</summary>
	public static bool BusMuted(int busIndex)
	{
		string name = AudioServer.GetBusName(busIndex);
		return Has(AudioSection, BusMuteKey(name)) ? GetBool(AudioSection, BusMuteKey(name), false) : AudioServer.IsBusMute(busIndex);
	}

	private static string BusVolumeKey(string busName) => $"{busName}_volume";

	private static string BusMuteKey(string busName) => $"{busName}_mute";

	private static void ApplyAudio()
	{
		for (int i = 0; i < AudioServer.BusCount; i++)
		{
			string name = AudioServer.GetBusName(i);
			if (Has(AudioSection, BusMuteKey(name)))
			{
				AudioServer.SetBusMute(i, GetBool(AudioSection, BusMuteKey(name), false));
			}

			if (Has(AudioSection, BusVolumeKey(name)))
			{
				float percent = GetFloat(AudioSection, BusVolumeKey(name), 1f);
				AudioServer.SetBusVolumeDb(i, percent <= 0.0001f ? -80f : Mathf.LinearToDb(percent));
			}
		}
	}

	// ───────────────────────── 画面 ─────────────────────────

	/// <summary>窗口模式：0 = 窗口化，1 = 全屏，2 = 无边框全屏。</summary>
	public static int WindowMode => GetInt(GraphicsSection, KeyWindowMode, 0);

	/// <summary>画质档：0 低 / 1 中 / 2 高（默认 1 = 与改动前一致）。</summary>
	public static int QualityLevel => Mathf.Clamp(GetInt(GraphicsSection, KeyQuality, 1), 0, 2);

	/// <summary>glow 总开关（默认开 = 场景文件里那组参数）。</summary>
	public static bool GlowEnabled => GetBool(GraphicsSection, KeyGlow, true);

	/// <summary>glow 强度倍率（1 = 场景文件里的原值）。</summary>
	public static float GlowGain => GetFloat(GraphicsSection, KeyGlowGain, 1f);

	public static void SetWindowMode(int mode)
	{
		Set(GraphicsSection, KeyWindowMode, mode);
		ApplyWindowMode(mode);
	}

	public static void SetResolution(int width, int height)
	{
		Set(GraphicsSection, KeyWidth, width);
		Set(GraphicsSection, KeyHeight, height);
		ApplyResolution();
	}

	public static void SetVsync(bool enabled)
	{
		Set(GraphicsSection, KeyVsync, enabled);
		DisplayServer.WindowSetVsyncMode(enabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
	}

	public static void SetMaxFps(int fps)
	{
		Set(GraphicsSection, KeyMaxFps, fps);
		Engine.MaxFps = Mathf.Max(0, fps);
	}

	public static void SetQuality(int quality)
	{
		Set(GraphicsSection, KeyQuality, Mathf.Clamp(quality, 0, 2));
	}

	public static void SetGlow(bool enabled)
	{
		Set(GraphicsSection, KeyGlow, enabled);
	}

	public static void SetGlowGain(float gain)
	{
		Set(GraphicsSection, KeyGlowGain, Mathf.Clamp(gain, 0f, 2f));
	}

	private static void ApplyGraphics()
	{
		ApplyWindowMode(WindowMode);
		if (WindowMode == 0)
		{
			ApplyResolution();
		}

		if (Has(GraphicsSection, KeyVsync))
		{
			DisplayServer.WindowSetVsyncMode(GetBool(GraphicsSection, KeyVsync, true)
				? DisplayServer.VSyncMode.Enabled
				: DisplayServer.VSyncMode.Disabled);
		}

		if (Has(GraphicsSection, KeyMaxFps))
		{
			Engine.MaxFps = Mathf.Max(0, GetInt(GraphicsSection, KeyMaxFps, 0));
		}
	}

	private static void ApplyWindowMode(int mode)
	{
		DisplayServer.WindowSetMode(mode switch
		{
			1 => DisplayServer.WindowMode.Fullscreen,
			2 => DisplayServer.WindowMode.ExclusiveFullscreen,
			_ => DisplayServer.WindowMode.Windowed,
		});
	}

	private static void ApplyResolution()
	{
		var size = new Vector2I(GetInt(GraphicsSection, KeyWidth, 1920), GetInt(GraphicsSection, KeyHeight, 1080));
		if (size.X <= 0 || size.Y <= 0)
		{
			return;
		}

		DisplayServer.WindowSetSize(size);
	}

	/// <summary>
	/// 画质要在**有场景**之后才能套用（glow 是 <see cref="WorldEnvironment"/> 上的东西）：
	/// 由 <see cref="SettingsScreen._Ready"/> 每次进场景调一次 —— 场景切了、环境换了都自动跟上。
	/// 只碰 <c>GlowEnabled</c> / <c>GlowIntensity</c>（MSAA 与 3D 缩放走 Viewport），
	/// **不改场景文件里其它渲染参数**（那是风格渲染 agent 的地盘）。
	/// </summary>
	public static void ApplySceneQuality(Node root)
	{
		int quality = QualityLevel;
		if (root.GetTree().Root is Window window)
		{
			window.Msaa3D = quality >= 2 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
			window.Scaling3DScale = quality <= 0 ? 0.75f : 1f;
		}

		bool glow = GlowEnabled;
		float gain = GlowGain;
		foreach (WorldEnvironment node in FindAll<WorldEnvironment>(root.GetTree().Root))
		{
			if (node.Environment is not { } environment)
			{
				continue;
			}

			ulong id = node.GetInstanceId();
			if (!_baseGlowIntensity.TryGetValue(id, out float baseIntensity))
			{
				// 第一次见到这个环境：把它**文件里的原值**记成基准（字典按实例 id 记，切场景后新环境自然重新采）。
				baseIntensity = environment.GlowIntensity;
				_baseGlowIntensity[id] = baseIntensity;
			}

			environment.GlowEnabled = glow;
			environment.GlowIntensity = baseIntensity * gain;
		}
	}

	/// <summary>递归收集某一类节点（场景树很小，而且只在切场景时跑一次）。</summary>
	private static IEnumerable<T> FindAll<T>(Node node)
		where T : Node
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is T typed)
			{
				yield return typed;
			}

			foreach (T deeper in FindAll<T>(child))
			{
				yield return deeper;
			}
		}
	}

	// ───────────────────────── 按键重绑 ─────────────────────────

	/// <summary>把某个动作重绑到一个键盘键（同时落盘）。</summary>
	public static void RebindKey(string action, Key key)
	{
		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
		Set(ControlsSection, action, (long)key);
		GameLog.Write("UI", $"按键重绑：{action} → {OS.GetKeycodeString(key)}");
	}

	/// <summary>把某个动作重绑到一个鼠标键。</summary>
	public static void RebindMouse(string action, MouseButton button)
	{
		InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
		Set(ControlsSection, action, $"mouse:{(int)button}");
		GameLog.Write("UI", $"按键重绑：{action} → {KeyHint.MouseText(button)}");
	}

	/// <summary>清掉某个动作的重绑记录（回到 <c>project.godot</c> 里的默认，需要调用方自己恢复事件）。</summary>
	public static void ClearRebind(string action) => Erase(ControlsSection, action);

	/// <summary>把存过的重绑全部套回 <see cref="InputMap"/>（启动时一次）。</summary>
	private static void ApplyInput()
	{
		int applied = 0;
		if (!Cfg.HasSection(ControlsSection))
		{
			return;   // 没写过重绑：连节都不存在（直接问 GetSectionKeys 会报 ERROR）
		}

		foreach (string action in Cfg.GetSectionKeys(ControlsSection))
		{
			if (!InputMap.HasAction(action))
			{
				GameLog.Write("UI", $"设置里存了动作「{action}」，但 InputMap 里没有它 —— 跳过");
				continue;
			}

			Variant stored = Cfg.GetValue(ControlsSection, action, default);
			if (stored.VariantType == Variant.Type.Int)
			{
				var key = (Key)(long)stored;
				InputMap.ActionEraseEvents(action);
				InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
				applied++;
			}
			else if (stored.VariantType == Variant.Type.String && stored.AsString().StartsWith("mouse:"))
			{
				string text = stored.AsString();
				if (text.StartsWith("mouse:") && int.TryParse(text.Substring(6), out int button))
				{
					InputMap.ActionEraseEvents(action);
					InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = (MouseButton)button });
					applied++;
				}
			}
		}

		if (applied > 0)
		{
			GameLog.Write("UI", $"设置：套用了 {applied} 条按键重绑（来自 {FilePath}）");
		}
	}

	/// <summary>启动时那一行日志的内容（自检 / 文档都对得上）。</summary>
	private static string DescribeStartup()
	{
		string source = Exists() ? FilePath : $"{FilePath}（还没写过，全部默认值）";
		return $"{source} ｜ 窗口模式 {WindowMode} 画质 {QualityLevel} glow {(GlowEnabled ? "开" : "关")}×{GlowGain:0.##} ｜ 最大帧率 {Engine.MaxFps} ｜ 总线 {AudioServer.BusCount} 条";
	}
}
