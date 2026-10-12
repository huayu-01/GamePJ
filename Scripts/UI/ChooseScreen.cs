using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 通用多选一界面（武器池、以后的商店/事件都用它）：给定标题与若干选项文本，回调选中的下标。
/// 复用 OverlayScreen 的防误触与 Esc 约定。
/// </summary>
public partial class ChooseScreen : OverlayScreen
{
	[Signal]
	public delegate void OptionChosenEventHandler(int index);

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _title = null!;
	private Label _hint = null!;
	private VBoxContainer _cards = null!;
	private Button _template = null!;
	private Action<int>? _onChosen;
	private int _count;

	/// <summary>允许用 Esc 放弃（回调收到 -1）。商店这类「可以不买」的界面开启它。</summary>
	public bool AllowCancel { get; set; }

	public override void _Ready()
	{
		CloseOnCancel = false;
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_title = GetNode<Label>("Root/Panel/VBox/Title");
		_hint = GetNode<Label>("Root/Panel/VBox/Hint");
		_cards = GetNode<VBoxContainer>("Root/Panel/VBox/Cards");
		_template = _cards.GetNode<Button>("CardTemplate");
	}

	public void Open(string title, IReadOnlyList<string> options, Action<int> onChosen)
	{
		if (IsOpen || options.Count == 0)
		{
			return;
		}

		_onChosen = onChosen;
		_count = options.Count;
		_title.Text = title;
		_hint.Text = "按数字键或点击选择（选定后立刻触发战斗）";

		foreach (Node child in _cards.GetChildren())
		{
			if (child != _template)
			{
				child.QueueFree();
			}
		}

		for (int i = 0; i < options.Count; i++)
		{
			var card = (Button)_template.Duplicate();
			card.Visible = true;
			card.Text = $"[{i + 1}] {options[i]}";
			card.Alignment = HorizontalAlignment.Left;

			// 标题能对上节点类型时给个图标（"强化池 / 进化池…"）；对不上就只留文字。
			card.Icon = UiIcons.ForTitle(title);

			int index = i;
			card.Pressed += () => Choose(index);
			_cards.AddChild(card);
		}

		SetKeyHints(AllowCancel
			? new[] { KeyHint.Text("1 ~ 4", "选择"), KeyHint.Of(Key.Escape, "放弃") }
			: new[] { KeyHint.Text("1 ~ 4", "选择") });

		BeginOpen();
	}

	private void Cancel()
	{
		if (InputBlocked || !AllowCancel)
		{
			return;
		}

		Action<int>? callback = _onChosen;
		_onChosen = null;
		Close();
		callback?.Invoke(-1);
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	private void Choose(int index)
	{
		if (InputBlocked || index < 0 || index >= _count)
		{
			return;
		}

		Action<int>? callback = _onChosen;
		_onChosen = null;
		EmitSignal(SignalName.OptionChosen, index);
		Close();
		callback?.Invoke(index);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen)
		{
			return;
		}

		// 允许放弃的界面：Esc 直接关掉并回调 -1。
		if (AllowCancel && @event.IsActionPressed("ui_cancel"))
		{
			Cancel();
			return;
		}

		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		int index = key.Keycode switch
		{
			Key.Key1 or Key.Kp1 => 0,
			Key.Key2 or Key.Kp2 => 1,
			Key.Key3 or Key.Kp3 => 2,
			Key.Key4 or Key.Kp4 => 3,
			_ => -1,
		};

		if (index >= 0)
		{
			Choose(index);
		}
	}
}
