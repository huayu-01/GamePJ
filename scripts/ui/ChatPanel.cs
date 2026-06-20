using Godot;

public partial class ChatPanel : Panel
{
    [Signal] public delegate void ChatMessageSentEventHandler(int playerId, string message);

    private TextEdit? _history;
    private LineEdit? _input;
    private VBoxContainer? _body;
    private Button? _toggle;
    private bool _collapsed;

    public override void _Ready()
    {
        BuildUi();
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.ChatMessageReceived += ReceiveChatMessage;
        }
    }

    public override void _ExitTree()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.ChatMessageReceived -= ReceiveChatMessage;
        }
    }

    private void BuildUi()
    {
        CustomMinimumSize = new Vector2(300, 230);
        AddThemeStyleboxOverride("panel", FlatUi.PanelStyle(FlatUi.SurfaceAlt));

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8;
        root.OffsetTop = 8;
        root.OffsetRight = -8;
        root.OffsetBottom = -8;
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);
        var title = FlatUi.Label("聊天", 18);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        _toggle = FlatUi.Button("收起");
        _toggle.CustomMinimumSize = new Vector2(72, 32);
        _toggle.Pressed += ToggleCollapsed;
        header.AddChild(_toggle);

        _body = new VBoxContainer();
        _body.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(_body);

        _history = new TextEdit
        {
            Name = "ChatHistory",
            Editable = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(280, 112)
        };
        _body.AddChild(_history);

        var inputRow = new HBoxContainer { Name = "InputContainer" };
        _body.AddChild(inputRow);
        _input = new LineEdit { Name = "ChatInput", SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "发送消息" };
        inputRow.AddChild(_input);
        var send = FlatUi.Button("发送", FlatUi.AccentMuted);
        send.CustomMinimumSize = new Vector2(64, 36);
        send.Pressed += Submit;
        inputRow.AddChild(send);

        var quick = new HBoxContainer { Name = "QuickPhrases" };
        _body.AddChild(quick);
        foreach (var phrase in new[] { "好牌", "诈唬", "不错" })
        {
            var button = FlatUi.Button(phrase);
            button.CustomMinimumSize = new Vector2(72, 32);
            button.Pressed += () => Send(phrase);
            quick.AddChild(button);
        }
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        if (_body != null)
        {
            _body.Visible = !_collapsed;
        }

        if (_toggle != null)
        {
            _toggle.Text = _collapsed ? "展开" : "收起";
        }

        CustomMinimumSize = _collapsed ? new Vector2(300, 52) : new Vector2(300, 230);
    }

    private void Submit()
    {
        var message = _input?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Send(message);
        if (_input != null)
        {
            _input.Text = "";
        }
    }

    private void Send(string message)
    {
        var playerId = PlayerData.Instance?.LocalPlayerId ?? 1;
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendChatMessage(playerId, message);
            return;
        }

        ReceiveChatMessage(playerId, message);
    }

    private void ReceiveChatMessage(int playerId, string message)
    {
        var playerName = GameManager.Instance?.Players.Find(player => player.Id == playerId)?.Name ?? $"玩家{playerId}";
        AppendMessage($"[{playerName}]: {message}");
        EmitSignal(SignalName.ChatMessageSent, playerId, message);
    }

    private void AppendMessage(string message)
    {
        if (_history == null)
        {
            return;
        }

        _history.Text += message + "\n";
        _history.ScrollVertical = _history.GetLineCount();
    }
}
