using System;
using System.Collections.Generic;
using ActRoguelike.Rewards;
using ActRoguelike.Config;
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

	private GridContainer _cards = null!;
	private Button _template = null!;
	private Label _title = null!;
	private Label _hint = null!;
	private bool _messageMode;
	private readonly List<RewardDefinition> _options = new();
	public IReadOnlyList<RewardDefinition> Options => _options;
	private string _textRevision = string.Empty;
	private long _offerEpoch;
	private Label _detailName = null!, _detailDescription = null!;
	private int _selectedIndex;
	private bool _menuHint;
	private bool _choiceSubmitted;
	private TextureRect _detailIcon = null!;
	public int SelectedIndex => _selectedIndex;
	public override void _Process(double delta)
	{
		if (!IsOpen) return;
		bool menu = RunController.Instance?.CanCancelAdventure == true;
		if (menu != _menuHint) UpdateKeyHints();
		_cards.Columns = _messageMode || Root.Size.X < 1100 ? 1 : Math.Min(3, Math.Max(1,_options.Count));
		if (_messageMode || _textRevision == ContentText.Revision) return;
		_textRevision = ContentText.Revision;
		int i = 0;
		foreach (Node child in _cards.GetChildren())
		{
			if (child == _template || child is not Button card || i >= _options.Count) continue;
			RewardDefinition row = _options[i++];
			if (card is RewardCard rewardCard) rewardCard.UpdateContent(row, i-1,RunController.Instance?.Build.DisplayName(row,true) ?? row.Name);
		}
		Select(_selectedIndex);
	}

	public override void _Ready()
	{
		// 奖励必须选一个、结算必须确认，所以不允许 Esc 直接关掉，避免流程卡住。
		CloseOnCancel = false;
		InitializeOverlay();
		GlassUi.Prepare(Root,true);
		_title = GetNode<Label>("Root/Panel/VBox/Title");
		_hint = GetNode<Label>("Root/Panel/VBox/Hint");
		_cards = GetNode<GridContainer>("Root/Panel/VBox/ChoicesScroll/Cards");
		_template = GetNode<Button>("Root/Panel/VBox/ChoicesScroll/Cards/CardTemplate");
		var panel = GetNode<PanelContainer>("Root/Panel"); GlassUi.EmptyPanel(panel);
		panel.OffsetLeft = 212; panel.OffsetRight = -212; panel.OffsetTop = 119; panel.OffsetBottom = -137;
		GetNode<VBoxContainer>("Root/Panel/VBox").AddThemeConstantOverride("separation",24);
		GlassUi.FrameTitle(_title);
		var footer = new PanelContainer { Name = "RewardDetails", CustomMinimumSize = new Vector2(0,146) };
		GetNode<VBoxContainer>("Root/Panel/VBox").AddChild(footer); GlassUi.Background(footer,false);
		var margin = new MarginContainer(); footer.AddChild(margin);
		foreach (string side in new[] { "left","right","top","bottom" }) margin.AddThemeConstantOverride("margin_"+side,side=="left"?138:24);
		var row = new HBoxContainer();row.AddThemeConstantOverride("separation",28);margin.AddChild(row);
		_detailIcon=new TextureRect { CustomMinimumSize=new Vector2(90,90),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Modulate=GlassUi.Paper };row.AddChild(_detailIcon);
		_detailName = new Label { CustomMinimumSize=new Vector2(220,0),VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart }; GlassUi.Typography(_detailName,32,700);row.AddChild(_detailName);
		row.AddChild(new VSeparator());
		_detailDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,VerticalAlignment=VerticalAlignment.Center }; GlassUi.Typography(_detailDescription,24);row.AddChild(_detailDescription);
	}

	/// <summary>单按钮提示（胜利 / 失败结算）。</summary>
	public void OpenMessage(string title, string body, string buttonLabel)
	{
		_offerEpoch++;
		_options.Clear();
		_messageMode = true;
		_choiceSubmitted=false;
		_cards.Columns = 1;
		_title.Text = title;
		_hint.Text = buttonLabel;
		_hint.Visible=true;
		ClearCards();
		Root.GetNode<Control>("Panel/VBox/RewardDetails").Visible = false;

		Button button = CreateCard(body);
		button.Alignment = HorizontalAlignment.Center;
		button.Pressed += DismissMessage;

		SetKeyHints((Key.Enter, "确定"), ("点击按钮", "确定"));

		BeginOpen();
		button.GrabFocus();
	}

	public void Open(IReadOnlyList<RewardDefinition> options, string title)
	{
		long epoch = ++_offerEpoch;
		_textRevision = ContentText.Revision;
		_options.Clear();
		_options.AddRange(options);
		_messageMode = false;
		_choiceSubmitted=false;
		_title.Text = title;
		_hint.Text = "";
		_hint.Visible=false;
		ClearCards();
		Root.GetNode<Control>("Panel/VBox/RewardDetails").Visible = true;
		_cards.Columns = Math.Min(3,Math.Max(1,options.Count));

		for (int i = 0; i < _options.Count; i++)
		{
			RewardDefinition definition = _options[i];
			var card = new RewardCard(); _cards.AddChild(card);
			card.Configure(definition,i,RunController.Instance?.Build.DisplayName(definition,true) ?? definition.Name);

			int index = i;
			card.Pressed += () => { if (epoch == _offerEpoch) Choose(index); };
			card.MouseEntered += () => { if (epoch == _offerEpoch) { Select(index); if (UiRouter.Instance?.IsTopScreen(this) != false) card.GrabFocus(); } };
			card.FocusEntered += () => { if (epoch == _offerEpoch) Select(index); };
		}
		_selectedIndex = 0; Select(0); UpdateKeyHints();

		BeginOpen();
		foreach (Node child in _cards.GetChildren()) if (child is RewardCard card) { card.GrabFocus(); break; }
	}
	private void Select(int index)
	{
		if (index < 0 || index >= _options.Count) return;
		_selectedIndex=index; var row=_options[index];
		_detailName.Text=RunController.Instance?.Build.DisplayName(row,true) ?? row.Name;
		_detailName.AddThemeColorOverride("font_color",GlassUi.RewardColor(row,true));
		_detailDescription.Text=row.Description;
		_detailIcon.Texture=UiIcons.ForReward(row);
	}
	private void UpdateKeyHints()
	{
		if (_messageMode) return;
		_menuHint=RunController.Instance?.CanCancelAdventure == true;
		var hints=new List<KeyHint> { KeyHint.Text(string.Join(" / ",System.Linq.Enumerable.Range(1,Math.Min(3,_options.Count))),"选择对应奖励"), KeyHint.Text("方向键","浏览"), KeyHint.Text("Enter / Space","确认焦点"), KeyHint.Text("鼠标左键","选择"), KeyHint.Text("悬停","详情") };
		if (_menuHint) hints.Add(KeyHint.Of(Key.Escape,"设置"));
		SetKeyHints(hints.ToArray());
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
				_cards.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	public override void Close() => BeginClose();

	private void DismissMessage()
	{
		if (IsOpen && !InputBlocked && (UiRouter.Instance == null || UiRouter.Instance.IsTopScreen(this)))
		{
			EmitSignal(SignalName.MessageDismissed);
		}
	}

	private void Choose(int index)
	{
		// 保护期内忽略：防手快 / 防上一次按键穿透。
		if (!IsOpen || InputBlocked || _choiceSubmitted || index < 0 || index >= _options.Count
			|| UiRouter.Instance != null && !UiRouter.Instance.IsTopScreen(this))
		{
			return;
		}

		_choiceSubmitted=true;
		EmitSignal(SignalName.RewardChosen, index);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || UiRouter.Instance != null && !UiRouter.Instance.IsTopScreen(this)
			|| @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		int index = key.Keycode switch
		{
			Key.Key1 or Key.Kp1 => 0,
			Key.Key2 or Key.Kp2 => 1,
			Key.Key3 or Key.Kp3 => 2,
			Key.Enter or Key.KpEnter or Key.Space => _selectedIndex,
			_ => -1,
		};

		if (index < 0)
		{
			return;
		}
		GetViewport().SetInputAsHandled();

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
