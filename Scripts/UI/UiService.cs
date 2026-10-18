using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 界面服务（重构：消除业务代码里的界面节点路径硬编码）。
///
/// 业务系统只说"我要开强化台 / 我要弹奖励"，不再关心界面挂在哪个节点下。
/// 界面实例靠**类型**在场景树里查找并缓存，场景切换后缓存自动失效重建。
///
/// 用法：
/// <code>
/// UiService.Instance?.OpenUpgrades();
/// UiService.Instance?.OpenRewards(options, "强化池");
/// </code>
/// </summary>
public partial class UiService : Node
{
	public static UiService? Instance { get; private set; }

	private readonly Dictionary<Type, OverlayScreen> _cache = new();

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	// ───────────────────────── 基础：按类型取界面 ─────────────────────────

	/// <summary>按类型取界面实例（跨场景查找 + 缓存）。</summary>
	public T? Screen<T>() where T : OverlayScreen
	{
		if (_cache.TryGetValue(typeof(T), out OverlayScreen? cached) && IsInstanceValid(cached))
		{
			return (T)cached;
		}

		T? found = FindInTree<T>(GetTree().Root);
		if (found != null)
		{
			_cache[typeof(T)] = found;
		}

		return found;
	}

	/// <summary>清缓存（场景切换、界面被销毁后由调用方或本类自动处理）。</summary>
	public void Invalidate() => _cache.Clear();

	private static T? FindInTree<T>(Node node) where T : class
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is T typed)
			{
				return typed;
			}

			if (FindInTree<T>(child) is { } deeper)
			{
				return deeper;
			}
		}

		return null;
	}

	// ───────────────────────── 语义化接口（业务只调这些）─────────────────────────
	/// <summary>只有基地挂载符文面板；局内没有换装入口。</summary>
	public bool OpenRunes()
	{
		if (Screen<RuneScreen>() is not { } screen) return false;
		screen.Open();
		return true;
	}

	// ⚠️ P6a 删掉了 `OpenUpgrades()`（它开的 `BaseScreen` 是基地里那页旧的「永久强化」，
	// 整份删除）—— 那一页的职责由 `OpenAbilityMatrix()`（能力卡：解锁 / 升级 / 激活 / 额度扩增）接管。

	/// <summary>打开装备配置台（角色 / 武器）。</summary>
	public bool OpenLoadout(Action<WeaponPresetKind> onChosen)
	{
		if (Screen<StartScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.Open(onChosen);
		return true;
	}

	/// <summary>打开操作说明。</summary>
	public bool OpenHelp()
	{
		if (Screen<SettingsScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.OpenHelp();
		return true;
	}

	/// <summary>
	/// 打开**首领状况预览**（本关攒了多少 Pressure、抽到了哪些词条、首领当前数值）。
	/// 快捷键由界面自己处理（<see cref="BossPreviewScreen.ToggleKey"/>，默认 B），
	/// 这里保留语义化入口给以后"交互时自动弹一次"或图鉴复用。
	/// </summary>
	public bool OpenBossPreview()
	{
		if (Screen<BossPreviewScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.Open();
		return true;
	}

	/// <summary>
	/// 打开**全屏设置菜单**（画面 / 控制 / 音量，分类可扩展）。
	/// 快捷键由界面自己处理（<see cref="SettingsScreen.ToggleKey"/>，默认 O）；这里留语义化入口给按钮用。
	/// </summary>
	public bool OpenSettings()
	{
		if (Screen<SettingsScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.Open();
		return true;
	}

	/// <summary>打开**「角色持有」面板**（本局构筑，图标矩阵 + 悬停说明）。快捷键 C。</summary>
	public bool OpenBuild()
	{
		if (Screen<BuildScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.Open();
		return true;
	}

	/// <summary>
	/// 打开**能力矩阵界面**（局外卡牌：解锁 / 升级 / 激活 / 额度扩增）。
	/// 快捷键由界面自己处理（<see cref="AbilityMatrixScreen.ToggleKey"/>，默认 K）；这里留语义化入口给按钮用。
	/// </summary>
	public bool OpenAbilityMatrix()
	{
		if (Screen<AbilityMatrixScreen>() is not { } screen)
		{
			return false;
		}

		screen.CloseOnCancel = true;
		screen.Open();
		return true;
	}

	/// <summary>打开三选一奖励（关卡节点用）。</summary>
	public bool OpenRewards(IReadOnlyList<RewardDefinition> options, string title)
	{
		if (Screen<RewardScreen>() is not { IsOpen: false } screen)
		{
			return false;
		}

		screen.ScreenLayer = OverlayScreen.UiLayer.Modal;
		screen.Open(options, title);
		return true;
	}

	/// <summary>打开通用多选一（商店 / 武器改造 / 基地选角色）。<paramref name="hint"/> 留空用界面默认提示。</summary>
	public bool OpenChoose(string title, IReadOnlyList<string> options, Action<int> onChosen, bool allowCancel = false, string? hint = null)
	{
		if (Screen<ChooseScreen>() is not { IsOpen: false } screen)
		{
			return false;
		}

		screen.ScreenLayer = OverlayScreen.UiLayer.Modal;
		screen.AllowCancel = allowCancel;
		screen.Open(title, options, onChosen, hint);
		return true;
	}

	/// <summary>弹一条带确认按钮的消息（结算 / 关卡提示）。</summary>
	public bool OpenMessage(string title, string body, string buttonLabel)
	{
		if (Screen<RewardScreen>() is not { IsOpen: false } screen)
		{
			return false;
		}

		screen.ScreenLayer = OverlayScreen.UiLayer.Modal;
		screen.OpenMessage(title, body, buttonLabel);
		return true;
	}

	/// <summary>关闭全部界面（切换场景、重开一局时用）。</summary>
	public void CloseAll()
	{
		foreach (OverlayScreen screen in _cache.Values)
		{
			if (IsInstanceValid(screen) && screen.IsOpen)
			{
				screen.Close();
			}
		}

		Invalidate();
	}
}
