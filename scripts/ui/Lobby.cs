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

        var topBar = new HBoxContainer { Name = "TopBar" };
        topBar.SetAnchorsPreset(LayoutPreset.TopWide);
        topBar.OffsetLeft = 32;
        topBar.OffsetTop = 20;
        topBar.OffsetRight = -32;
        topBar.OffsetBottom = 68;
        AddChild(topBar);
        var title = FlatUi.Label("房间", 28);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topBar.AddChild(title);
        var exitTop = FlatUi.Button("退出房间", FlatUi.Danger);
        exitTop.Pressed += LeaveLobby;
        topBar.AddChild(exitTop);

        var root = new HBoxContainer { Name = "LobbyRoot" };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 32;
        root.OffsetTop = 86;
        root.OffsetRight = -32;
        root.OffsetBottom = -32;
        root.AddThemeConstantOverride("separation", 18);
        AddChild(root);

        var leftPanel = FlatUi.Panel("HostPanelFrame");
        leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(leftPanel);
        var left = new VBoxContainer { Name = "HostPanel" };
        left.SetAnchorsPreset(LayoutPreset.FullRect);
        left.OffsetLeft = 18;
        left.OffsetTop = 16;
        left.OffsetRight = -18;
        left.OffsetBottom = -16;
        left.AddThemeConstantOverride("separation", 12);
        leftPanel.AddChild(left);

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
        left.AddChild(_qrTexture);

        var copy = FlatUi.Button("复制房间号");
        copy.Pressed += () => DisplayServer.ClipboardSet(NetworkManager.Instance?.RoomCode ?? "");
        left.AddChild(copy);

        left.AddChild(BuildSpinRow("低注/小盲", out _smallBlindSpin, 1, 10000, GameManager.Instance?.SmallBlindAmount ?? Constants.SmallBlind, 1));
        left.AddChild(BuildSpinRow("最低带入", out _minBuyInSpin, 1, 100000, GameManager.Instance?.MinBuyIn ?? Constants.MinBuyIn, 1));
        left.AddChild(BuildSpinRow("最高带入", out _maxBuyInSpin, 1, 100000, GameManager.Instance?.MaxBuyIn ?? Constants.MaxBuyIn, 1));
        left.AddChild(BuildSpinRow("后手上限", out _chipLimitSpin, 1, 200000, GameManager.Instance?.TableChipLimit ?? Constants.TableChipLimit, 1));

        var start = FlatUi.Button("开始游戏", FlatUi.AccentMuted);
        start.Pressed += OnStartPressed;
        left.AddChild(start);

        var back = FlatUi.Button("返回主菜单");
        back.Pressed += LeaveLobby;
        left.AddChild(back);

        var rightPanel = FlatUi.Panel("PlayersPanelFrame");
        rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(rightPanel);
        var right = new VBoxContainer { Name = "PlayersPanel" };
        right.SetAnchorsPreset(LayoutPreset.FullRect);
        right.OffsetLeft = 18;
        right.OffsetTop = 16;
        right.OffsetRight = -18;
        right.OffsetBottom = -16;
        rightPanel.AddChild(right);

        var playerTitle = FlatUi.Label("玩家列表", 28);
        right.AddChild(playerTitle);

        _playersList = new VBoxContainer { Name = "PlayersList" };
        right.AddChild(_playersList);
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
            (int)(_chipLimitSpin?.Value ?? Constants.TableChipLimit));
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
