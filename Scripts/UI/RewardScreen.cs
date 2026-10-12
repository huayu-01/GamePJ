using System;
using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 三选一奖励界面 / 结算提示。暂停与鼠标由 <see cref="UiRouter"/> 统一管理；
/// 打开后 0.3 秒内忽略点击与按键（防误触），也允许 Esc 关闭开关由 CloseOnCancel 控制。
/// </summary>
public partial class RewardScreen : OverlayScreen
{
	[Signal]
	public delegate void RewardChosenEventHandler(int index);

	[Signal]
	public delegate void MessageDismissedEventHandler();

	protected override Control Root => GetNode<Control>("Root");

	private VBoxContainer _cards = null!;
	private Button _template = null!;
	private Label _title = null!;
	private Label _hint = null!;
	private bool _messageMode;
	private readonly List<RewardDefinition> _options = new();

	public override void _Ready()
	{
		// 奖励必须选一个、结算必须确认，所以不允许 Esc 直接关掉，避免流程卡住。
		CloseOnCancel = false;
		InitializeOverlay();
		_title = GetNode<Label>("Root/Panel/VBox/Title");
		_hint = GetNode<Label>("Root/Panel/VBox/Hint");
		_cards = GetNode<VBoxContainer>("Root/Panel/VBox/Cards");
		_template = GetNode<Button>("Root/Panel/VBox/Cards/CardTemplate");
	}

	/// <summary>单按钮提示（胜利 / 失败结算）。</summary>
	public void OpenMessage(string title, string body, string buttonLabel)
	{
		_options.Clear();
		_messageMode = true;
		_title.Text = title;
		_hint.Text = buttonLabel;
		ClearCards();

		Button button = CreateCard(body);
		button.Alignment = HorizontalAlignment.Center;
		button.Pressed += DismissMessage;

		SetKeyHints((Key.Enter, "确定"), ("点击按钮", "确定"));

		BeginOpen();
	}

	public void Open(IReadOnlyList<RewardDefinition> options, string title)
	{
		_options.Clear();
		_options.AddRange(options);
		_messageMode = false;
		_title.Text = title;
		_hint.Text = "按 1 / 2 / 3 或点击选择";
		ClearCards();

		for (int i = 0; i < _options.Count; i++)
		{
			RewardDefinition definition = _options[i];
			Button card = CreateCard($"[{i + 1}] {definition.Name}（{KindLabel(definition.Kind)}）\n{definition.Description}");

			// 图标代替文字：类型（强化 / 进化 / 异化）一眼可辨，文字留给"这一条到底做什么"。
			card.Icon = UiIcons.ForReward(definition);

			int index = i;
			card.Pressed += () => Choose(index);
		}

		SetKeyHints(("1 / 2 / 3", "选择奖励"), ("悬停", "看说明"));

		BeginOpen();
	}

	private Button CreateCard(string text)
	{
		var card = (Button)_template.Duplicate();
		card.Visible = true;
		card.Text = text;
		card.Alignment = HorizontalAlignment.Left;
		_cards.AddChild(card);
		return card;
	}

	private void ClearCards()
	{
		foreach (Node child in _cards.GetChildren())
		{
			if (child != _template)
			{
				child.QueueFree();
			}
		}
	}

	public override void Close() => BeginClose();

	private void DismissMessage()
	{
		if (!InputBlocked)
		{
			EmitSignal(SignalName.MessageDismissed);
		}
	}

	private void Choose(int index)
	{
		// 保护期内忽略：防手快 / 防上一次按键穿透。
		if (InputBlocked || index < 0 || index >= _options.Count)
		{
			return;
		}

		EmitSignal(SignalName.RewardChosen, index);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		int index = key.Keycode switch
		{
			Key.Key1 or Key.Kp1 => 0,
			Key.Key2 or Key.Kp2 => 1,
			Key.Key3 or Key.Kp3 => 2,
			Key.Enter or Key.KpEnter or Key.Space => 0,
			_ => -1,
		};

		if (index < 0)
		{
			return;
		}

		if (_messageMode)
		{
			DismissMessage();
			return;
		}

		Choose(index);
	}

	private static string KindLabel(RewardKind kind) => kind switch
	{
		RewardKind.Evolution => "进化",
		RewardKind.Mutation => "异化",
		_ => "强化",
	};
}
