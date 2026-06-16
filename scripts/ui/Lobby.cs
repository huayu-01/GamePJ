using Godot;

public partial class Lobby : Control
{
    private VBoxContainer? _playersList;
    private Label? _roomLabel;
    private TextureRect? _qrTexture;
    private SpinBox? _smallBlindSpin;
    private SpinBox? _minBuyInSpin;
    private SpinBox? _maxBuyInSpin;
    private SpinBox? _chipLimitSpin;
    private SpinBox? _thinkingTimeSpin;

    public override void _Ready()
    {
        BuildUi();
        Refresh();
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.PlayerConnected += (_, _) => Refresh();
            NetworkManager.Instance.PlayerDisconnected += _ => Refresh();
        }
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = FlatUi.Background };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { Name = "LobbyCenter" };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.OffsetLeft = 28;
        center.OffsetTop = 28;
        center.OffsetRight = -28;
        center.OffsetBottom = -28;
        AddChild(center);

        var frame = FlatUi.Panel("LobbyFrame");
        frame.CustomMinimumSize = new Vector2(620, 860);
        frame.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        frame.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        center.AddChild(frame);

        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.OffsetLeft = 18;
        scroll.OffsetTop = 16;
        scroll.OffsetRight = -18;
        scroll.OffsetBottom = -16;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        frame.AddChild(scroll);

        var left = new VBoxContainer { Name = "LobbyContent" };
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(left);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        left.AddChild(header);

        var title = FlatUi.Label("房间", 30);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var exitTop = FlatUi.Button("退出房间", FlatUi.Danger);
        exitTop.CustomMinimumSize = new Vector2(104, 40);
        exitTop.Pressed += LeaveLobby;
        header.AddChild(exitTop);

        _roomLabel = new Label { Name = "RoomLabel", Text = "房间号: ------" };
        _roomLabel.AddThemeColorOverride("font_color", FlatUi.Text);
        _roomLabel.AddThemeFontSizeOverride("font_size", 32);
        left.AddChild(_roomLabel);
        left.AddChild(FlatUi.MutedLabel("同一局域网玩家可用 IP 加入；二维码用于核对房间信息。"));

        _qrTexture = new TextureRect
        {
            Name = "QRCode",
            CustomMinimumSize = new Vector2(256, 256),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        var qrCenter = new CenterContainer();
        qrCenter.AddChild(_qrTexture);
        left.AddChild(qrCenter);

        var copy = FlatUi.Button("复制房间号");
        copy.Pressed += () => DisplayServer.ClipboardSet(NetworkManager.Instance?.RoomCode ?? "");
        left.AddChild(copy);

        left.AddChild(BuildSectionLabel("房间设置"));
        left.AddChild(BuildSpinRow("低注/小盲", out _smallBlindSpin, 1, 10000, GameManager.Instance?.SmallBlindAmount ?? Constants.SmallBlind, 1));
        left.AddChild(BuildSpinRow("最低带入", out _minBuyInSpin, 1, 100000, GameManager.Instance?.MinBuyIn ?? Constants.MinBuyIn, 1));
        left.AddChild(BuildSpinRow("最高带入", out _maxBuyInSpin, 1, 100000, GameManager.Instance?.MaxBuyIn ?? Constants.MaxBuyIn, 1));
        left.AddChild(BuildSpinRow("后手上限", out _chipLimitSpin, 1, 200000, GameManager.Instance?.TableChipLimit ?? Constants.TableChipLimit, 1));
        left.AddChild(BuildSpinRow("思考时间", out _thinkingTimeSpin, 0, 300, GameManager.Instance?.ThinkingTimeSeconds ?? Constants.ThinkingTimeSeconds, 1));

        left.AddChild(BuildSectionLabel("玩家列表"));
        var playersPanel = FlatUi.Panel("PlayersPanelFrame");
        playersPanel.CustomMinimumSize = new Vector2(0, 132);
        left.AddChild(playersPanel);
        _playersList = new VBoxContainer { Name = "PlayersList" };
        _playersList.SetAnchorsPreset(LayoutPreset.FullRect);
        _playersList.OffsetLeft = 12;
        _playersList.OffsetTop = 10;
        _playersList.OffsetRight = -12;
        _playersList.OffsetBottom = -10;
        playersPanel.AddChild(_playersList);

        var start = FlatUi.Button("开始游戏", FlatUi.AccentMuted);
        start.CustomMinimumSize = new Vector2(0, 46);
        start.Pressed += OnStartPressed;
        left.AddChild(start);

        var back = FlatUi.Button("返回主菜单");
        back.Pressed += LeaveLobby;
        left.AddChild(back);
    }

    private static Label BuildSectionLabel(string text)
    {
        var label = FlatUi.Label(text, 18);
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 1.0f));
        return label;
    }

    private void Refresh()
    {
        var network = NetworkManager.Instance;
        var manager = GameManager.Instance;
        _roomLabel!.Text = network?.IsHost == true
            ? $"房间号: {network.RoomCode}  ·  {network.RoomMaxPlayers}人局  ·  {manager?.SmallBlindAmount}/{manager?.BigBlindAmount}"
            : "等待 Host 开始游戏...";

        if (_qrTexture != null && network?.IsHost == true)
        {
            var generator = new QRCodeGenerator();
            _qrTexture.Texture = generator.GenerateQRCode(network.GetRoomPayload());
        }

        if (_playersList == null)
        {
            return;
        }

        foreach (var child in _playersList.GetChildren())
        {
            child.QueueFree();
        }

        if (network != null && network.Players.Count > 0)
        {
            foreach (var player in network.Players.Values)
            {
                _playersList.AddChild(FlatUi.Label($"座位 {player.SeatIndex + 1}  #{player.Id}  {player.Name}", 18));
            }
        }
        else
        {
            _playersList.AddChild(FlatUi.MutedLabel("当前房间暂无玩家"));
        }
    }

    private void OnStartPressed()
    {
        GameManager.Instance?.SyncPlayersFromNetwork();
        GameManager.Instance?.ConfigureRoomRules(
            (int)(_smallBlindSpin?.Value ?? Constants.SmallBlind),
            (int)(_minBuyInSpin?.Value ?? Constants.MinBuyIn),
            (int)(_maxBuyInSpin?.Value ?? Constants.MaxBuyIn),
            (int)(_chipLimitSpin?.Value ?? Constants.TableChipLimit),
            (int)(_thinkingTimeSpin?.Value ?? Constants.ThinkingTimeSeconds));
        GameManager.Instance?.StartGame();
        NetworkManager.Instance?.StartNetworkGame();
        GetTree().ChangeSceneToFile(Constants.GameTableScene);
    }

    private void LeaveLobby()
    {
        NetworkManager.Instance?.LeaveRoom();
        GameManager.Instance?.LeaveTable();
        GetTree().ChangeSceneToFile(Constants.MainMenuScene);
    }

    private HBoxContainer BuildSpinRow(string label, out SpinBox spin, int min, int max, int value, int step)
    {
        var row = new HBoxContainer();
        var text = FlatUi.MutedLabel(label);
        text.CustomMinimumSize = new Vector2(118, 34);
        row.AddChild(text);
        spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            Step = step,
            CustomMinimumSize = new Vector2(120, 36)
        };
        row.AddChild(spin);
        return row;
    }
}
