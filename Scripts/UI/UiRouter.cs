using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 弹出界面路由（autoload）。统一负责：
/// - 维护界面栈：栈里有界面就暂停游戏，栈空就恢复；
/// - 鼠标捕获：有界面时释放鼠标，没有时捕获（Esc 不再直接切换鼠标模式）；
/// - Esc：关闭当前最顶层可关闭的界面；没有界面时打开菜单（当前用帮助界面占位，等 UI 框架）。
/// </summary>
public partial class UiRouter : Node
{
	public static UiRouter? Instance { get; private set; }

	/// <summary>无弹窗时按 Esc 要打开的菜单界面（由界面自己在 _Ready 里注册）。</summary>
	public System.Action? OpenMenu { get; set; }

	private readonly List<OverlayScreen> _stack = new();

	public bool AnyOpen => _stack.Count > 0;
	public int StackDepth => _stack.Count;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		// 玩家设置的唯一启动应用点（音量 / 窗口 / 按键重绑）。
		// 刻意**不加新的 autoload**（architecture.md 第 4 条：能不加全局入口就不加）——
		// UiRouter 已经排在场景之前，且每个场景里都有界面会用到这些设置。
		// 渲染画质要等场景里有 WorldEnvironment，由 SettingsScreen._Ready 每次进场景套一次。
		SettingsStore.ApplyOnStartup();
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

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

	public bool TryCloseTop()
	{
		for (int i = _stack.Count - 1; i >= 0; i--)
		{
			if (_stack[i].TryCloseByCancel())
			{
				return true;
			}
		}

		return false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
		{
			return;
		}

		if (TryCloseTop())
		{
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
		// 分层：只有 Panel / Modal 才暂停游戏与切鼠标模式，Hud 层常驻不影响。
		bool blocking = false;
		foreach (OverlayScreen screen in _stack)
		{
			if (screen.ScreenLayer != OverlayScreen.UiLayer.Hud)
			{
				blocking = true;
				break;
			}
		}

		GetTree().Paused = blocking;
		Input.MouseMode = blocking ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}
}
