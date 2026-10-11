using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 操作说明与流程说明。H 打开/关闭；没有其它弹窗时 Esc 也打开它（暂时充当「菜单」，
/// 等后续 UI 框架接入真正的菜单）。支持 Esc 关闭，但受基类的防误触保护期限制。
/// </summary>
public partial class HelpScreen : OverlayScreen
{
	[Export] public Key ToggleKey { get; set; } = Key.H;

	protected override Control Root => _root;

	private Control _root = null!;
	private bool _keyHeld;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();

		if (UiRouter.Instance != null)
		{
			UiRouter.Instance.OpenMenu = Open;
		}
	}

	public override void _Process(double delta)
	{
		bool held = Input.IsPhysicalKeyPressed(ToggleKey);
		if (held && !_keyHeld)
		{
			Toggle();
		}

		_keyHeld = held;
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
		if (!IsOpen)
		{
			BeginOpen();
		}
	}

	public override void Close()
	{
		// 保护期内不响应关闭，避免「开→瞬间关」的误触。
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}
}
