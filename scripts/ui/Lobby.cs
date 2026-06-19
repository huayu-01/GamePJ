using Godot;

public partial class Lobby : Control
{
    private VBoxContainer? _playersList;
    private Label? _roomLabel;
    private SpinBox? _smallBlindSpin;
    private SpinBox? _minBuyInSpin;
    private SpinBox? _maxBuyInSpin;
    private SpinBox? _chipLimitSpin;
    private SpinBox? _thinkingTimeSpin;
    private Button? _copyButton;
    private Button? _startButton;
    private CenterContainer? _center;
    private Panel? _frame;
    private Vector2 _lastResponsiveViewport = new(-1, -1);

    public override void _Ready()
    {
        BuildUi();
        Refresh();
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.PlayerConnected += OnPlayerConnected;
            NetworkManager.Instance.PlayerDisconnected += OnPlayerDisconnected;
            NetworkManager.Instance.GameStarted += OnGameStarted;
        }
    }

    public override void _ExitTree()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.PlayerConnected -= OnPlayerConnected;
            NetworkManager.Instance.PlayerDisconnected -= OnPlayerDisconnected;
            NetworkManager.Instance.GameStarted -= OnGameStarted;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            ApplyResponsiveLayout();
        }
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = FlatUi.Background };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { Name = "LobbyCenter" };
        _center = center;
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.OffsetLeft = 28;
        center.OffsetTop = 28;
        center.OffsetRight = -28;
        center.OffsetBottom = -28;
        AddChild(center);

        var frame = FlatUi.Panel("LobbyFrame");
        _frame = frame;
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

        _roomLabel = new Label
        {
            Name = "RoomLabel",
            Text = "房间号: ------",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _roomLabel.AddThemeColorOverride("font_color", FlatUi.Text);
        _roomLabel.AddThemeFontSizeOverride("font_size", 32);
        left.AddChild(_roomLabel);
        left.AddChild(FlatUi.MutedLabel("同一局域网玩家可粘贴邀请信息加入；跨网络需要 Host 开放 UDP 端口。"));

        _copyButton = FlatUi.Button("复制邀请信息");
        _copyButton.Pressed += () => DisplayServer.ClipboardSet(NetworkManager.Instance?.GetRoomPayload() ?? "");
        left.AddChild(_copyButton);

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

        _startButton = FlatUi.Button("开始游戏", FlatUi.AccentMuted);
        _startButton.CustomMinimumSize = new Vector2(0, 46);
        _startButton.Pressed += OnStartPressed;
        left.AddChild(_startButton);

        var back = FlatUi.Button("返回主菜单");
        back.Pressed += LeaveLobby;
        left.AddChild(back);
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0 || _frame == null || viewport.IsEqualApprox(_lastResponsiveViewport))
        {
            return;
        }
        _lastResponsiveViewport = viewport;

        var safe = ResponsiveUi.GetSafeMargins(this);
        var margin = ResponsiveUi.MarginFor(viewport);
        if (_center != null)
        {
            ResponsiveUi.ApplySafeCenter(_center, this, margin);
        }

        if (_frame != null)
        {
            _frame.CustomMinimumSize = ResponsiveUi.FitPanel(viewport, safe, 660f, 920f, margin);
        }

        _roomLabel?.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(Mathf.Clamp(viewport.X * 0.036f, 24f, 34f)));
        ResponsiveUi.EnsureTouchTargets(this);
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
            ? $"房间号: {network.RoomCode}  ·  {network.GetLocalIP()}:{Constants.DefaultPort}  ·  {network.RoomMaxPlayers}人局"
            : "等待 Host 开始游戏...";

        var isHost = network?.IsHost == true;
        if (_copyButton != null)
        {
            _copyButton.Visible = isHost;
        }

        if (_startButton != null)
        {
            _startButton.Visible = isHost;
            var canStart = (network?.Players.Count ?? 0) >= 2;
            _startButton.Disabled = !canStart;
            _startButton.Text = canStart ? "开始游戏" : "等待至少 2 名玩家";
        }

        foreach (var spin in new[] { _smallBlindSpin, _minBuyInSpin, _maxBuyInSpin, _chipLimitSpin, _thinkingTimeSpin })
        {
            if (spin != null)
            {
                spin.Editable = isHost;
            }
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
        if (NetworkManager.Instance?.IsHost != true)
        {
            return;
        }

        if (NetworkManager.Instance.Players.Count < 2)
        {
            Refresh();
            return;
        }

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

    private void OnGameStarted()
    {
        if (NetworkManager.Instance?.IsHost == true)
        {
            return;
        }

        GetTree().ChangeSceneToFile(Constants.GameTableScene);
    }

    private void OnPlayerConnected(int id, string playerName)
    {
        Refresh();
    }

    private void OnPlayerDisconnected(int id)
    {
        Refresh();
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
