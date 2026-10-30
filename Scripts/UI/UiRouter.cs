using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 弹出界面路由（autoload）。统一负责：
/// - 维护界面栈：栈里有界面就暂停游戏，栈空就恢复；
/// - 鼠标捕获：有界面时释放鼠标，没有时捕获（Esc 不再直接切换鼠标模式）；
/// - Esc：只尝试关闭当前顶层弹层；顶层暂不可关闭时消费按键，没有弹层时打开菜单。
/// </summary>
public partial class UiRouter : Node
{
	public static UiRouter? Instance { get; private set; }

	/// <summary>无弹窗时按 Esc 要打开的菜单界面（由界面自己在 _Ready 里注册）。</summary>
	public System.Action? OpenMenu { get; set; }

	private readonly List<OverlayScreen> _stack = new();
	private Window? _window;

	/// <summary>当前能阻挡底层输入的有效弹层；HUD、已关闭或已退树的界面不参与。</summary>
	public OverlayScreen? TopScreen
	{
		get
		{
			for (int i = _stack.Count - 1; i >= 0; i--)
			{
				OverlayScreen screen = _stack[i];
				if (IsInstanceValid(screen) && screen.IsInsideTree() && screen.IsOpen && screen.ScreenLayer != OverlayScreen.UiLayer.Hud)
				{
					return screen;
				}
			}
			return null;
		}
	}
	public bool AnyOpen => TopScreen != null;
	public int StackDepth => _stack.Count;
	public bool IsTopScreen(OverlayScreen screen) => ReferenceEquals(TopScreen, screen);

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().AutoAcceptQuit = false;
		_window = GetTree().Root;
		_window.CloseRequested += OnCloseRequested;

		// 玩家设置的唯一启动应用点（音量 / 窗口 / 按键重绑）。
		// 刻意**不加新的 autoload**（architecture.md 第 4 条：能不加全局入口就不加）——
		// UiRouter 已经排在场景之前，且每个场景里都有界面会用到这些设置。
		// 渲染画质要等场景里有 WorldEnvironment，由 SettingsScreen._Ready 每次进场景套一次。
		SettingsStore.ApplyOnStartup();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_window)) _window!.CloseRequested -= OnCloseRequested;
		_window = null;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	/// <summary>界面与GDScript共用的有序退出入口。</summary>
	public void RequestQuit(int exitCode) => ActRoguelike.Core.GameExit.Request(GetTree(), exitCode);

	private void OnCloseRequested() => RequestQuit(0);

	public void Push(OverlayScreen screen)
	{
		if (!_stack.Contains(screen))
		{
			_stack.Add(screen);
		}

		Sync();
	}

	public void Pop(OverlayScreen screen)
	{
		_stack.Remove(screen);
		Sync();
	}

	/// <summary>主动切场景前清掉旧场景界面引用，避免模态节点退树后仍保持全局暂停。</summary>
	public void ResetForSceneChange()
	{
		_stack.Clear();
		Sync();
	}

	public bool TryCloseTop()
	{
		// 顶层处于保护期或必须确认时，不得继续向下关闭其他面板。
		return TopScreen?.TryCloseByCancel() ?? false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
		{
			return;
		}

		if (TopScreen != null)
		{
			// 奖励／商店选择不能用Esc直接跳过，但仍可进入暂停菜单取消整局冒险。
			if (!TopScreen.CloseOnCancel && !TopScreen.InputBlocked
				&& ActRoguelike.Rewards.RunController.Instance?.CanCancelAdventure == true && OpenMenu != null)
			{
				OpenMenu(); GetViewport().SetInputAsHandled(); return;
			}
			TryCloseTop();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!AnyOpen && OpenMenu != null)
		{
			OpenMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Sync()
	{
		_stack.RemoveAll(screen => !IsInstanceValid(screen) || !screen.IsInsideTree() || !screen.IsOpen);
		// 分层：只有 Panel / Modal 才暂停游戏与切鼠标模式，Hud 层常驻不影响。
		bool blocking = TopScreen != null;
		GlassUi.SyncWorldHud(GetTree().Root,_stack.Exists(screen=>screen is RewardScreen or BuildScreen or CodexScreen));

		GetTree().Paused = blocking;
		Input.MouseMode = blocking ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}
}
