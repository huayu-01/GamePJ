using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameTable : Control
{
    private const float StageAspect = 1f / 2f;

    private readonly List<PlayerHUD> _seatHuds = new();
    private Control? _stage;
    private HBoxContainer? _topBar;
    private Control? _seatLayer;
    private Panel? _centerPanel;
    private CommunityCards? _communityCards;
    private Label? _potLabel;
    private Label? _stateLabel;
    private Label? _turnPromptLabel;
    private ProgressBar? _turnTimerBar;
    private Label? _turnTimerLabel;
    private Label? _currentBetLabel;
    private BettingPanel? _bettingPanel;
    private ColorRect? _drawerDismissLayer;
    private Panel? _leftDrawerPanel;
    private Panel? _sidePanel;
    private Control? _settlementLayer;
    private Texture2D? _settlementChipTexture;
    private Panel? _bustedPanel;
    private Label? _bustedLabel;
    private Button? _rebuyButton;
    private Button? _chatToggle;
    private Button? _sideToggle;
    private Button? _restartButton;
    private Button? _leaveButton;
    private CheckButton? _sitOutToggle;
    private CheckButton? _aiToggle;
    private SpinBox? _aiCount;
    private SpinBox? _chipLimitSpin;
    private bool _sideCollapsed = true;
    private bool _chatCollapsed = true;
    private bool _syncingSitOutToggle;
    private bool _revealWindowOpen;

    public override void _Ready()
    {
        BuildUi();
        WireSignals();
        if (GameManager.Instance?.Players.Count < 2 && NetworkManager.Instance?.IsConnected != true)
        {
            GameManager.Instance?.EnsureDemoPlayers(2);
            GameManager.Instance?.StartGame();
        }

        Refresh();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            ApplyResponsiveLayout();
            LayoutPlayerHuds();
        }
    }

    public override void _Process(double delta)
    {
        UpdateTurnTimerVisual();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = new Color(0.035f, 0.08f, 0.06f, 1f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _stage = new Control { Name = "TenByTwentyOneStage" };
        AddChild(_stage);

        BuildTopBar();
        BuildTableArea();
        BuildSidePanel();
        BuildBettingPanel();
        BuildSettlementLayer();
        BuildBustedPanel();
        ApplyResponsiveLayout();
    }

    private void BuildTopBar()
    {
        var top = new HBoxContainer { Name = "TopBar" };
        _topBar = top;
        top.SetAnchorsPreset(LayoutPreset.TopWide);
        top.OffsetLeft = 24;
        top.OffsetTop = 16;
        top.OffsetRight = -24;
        top.OffsetBottom = 62;
        top.AddThemeConstantOverride("separation", 10);

        _stateLabel = FlatUi.Label("牌局", 22);
        _stateLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _stateLabel.CustomMinimumSize = Vector2.Zero;
        _stateLabel.ClipText = true;
        _stateLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        top.AddChild(_stateLabel);

        _chatToggle = FlatUi.Button("聊天");
        _chatToggle.Pressed += ToggleChatPanel;
        top.AddChild(_chatToggle);

        _sideToggle = FlatUi.Button("记录");
        _sideToggle.Pressed += ToggleSidePanel;
        top.AddChild(_sideToggle);

        _restartButton = FlatUi.Button("新一局");
        _restartButton.Pressed += () => GameManager.Instance?.StartGame();
        top.AddChild(_restartButton);

        _sitOutToggle = new CheckButton { Text = "暂离" };
        _sitOutToggle.AddThemeColorOverride("font_color", FlatUi.Text);
        _sitOutToggle.Pressed += ToggleSitOut;
        top.AddChild(_sitOutToggle);

        _leaveButton = FlatUi.Button("离开", FlatUi.Danger);
        _leaveButton.Pressed += LeaveTable;
        top.AddChild(_leaveButton);
        AddToStage(top);
    }

    private void BuildTableArea()
    {
        _seatLayer = new Control { Name = "SeatLayer" };
        _seatLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        _seatLayer.MouseFilter = MouseFilterEnum.Ignore;
        AddToStage(_seatLayer);

        var centerPanel = FlatUi.Panel("TableCenter");
        _centerPanel = centerPanel;
        centerPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        centerPanel.SetAnchorsPreset(LayoutPreset.Center);
        centerPanel.OffsetLeft = -280;
        centerPanel.OffsetRight = 100;
        centerPanel.OffsetTop = -120;
        centerPanel.OffsetBottom = 95;
        centerPanel.MouseFilter = MouseFilterEnum.Stop;
        AddToStage(centerPanel);

        var centerBox = new VBoxContainer();
        centerBox.SetAnchorsPreset(LayoutPreset.FullRect);
        centerBox.OffsetLeft = 18;
        centerBox.OffsetTop = 16;
        centerBox.OffsetRight = -18;
        centerBox.OffsetBottom = -16;
        centerBox.AddThemeConstantOverride("separation", 5);
        centerPanel.AddChild(centerBox);

        _turnPromptLabel = FlatUi.Label("等待行动", 20, HorizontalAlignment.Center);
        _turnPromptLabel.AddThemeColorOverride("font_color", FlatUi.Accent);
        centerBox.AddChild(_turnPromptLabel);

        _turnTimerBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10)
        };
        centerBox.AddChild(_turnTimerBar);

        _turnTimerLabel = FlatUi.MutedLabel("", 13);
        _turnTimerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        centerBox.AddChild(_turnTimerLabel);

        var potRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var chip = new TextureRect
        {
            Texture = LoadTexture("res://assets/textures/chips/chipRedWhite.png"),
            CustomMinimumSize = new Vector2(34, 34),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        potRow.AddChild(chip);
        _potLabel = FlatUi.Label("底池: 0", 22, HorizontalAlignment.Center);
        potRow.AddChild(_potLabel);
        centerBox.AddChild(potRow);

        _currentBetLabel = FlatUi.Label("当前加注: 0", 18, HorizontalAlignment.Center);
        _currentBetLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 1.0f));
        centerBox.AddChild(_currentBetLabel);

        _communityCards = new CommunityCards { Alignment = BoxContainer.AlignmentMode.Center };
        centerBox.AddChild(_communityCards);
    }

    private void BuildSidePanel()
    {
        BuildDrawerDismissLayer();
        BuildChatDrawer();
        BuildHistoryDrawer();
    }

    private void BuildDrawerDismissLayer()
    {
        _drawerDismissLayer = new ColorRect
        {
            Name = "DrawerDismissLayer",
            Color = new Color(0, 0, 0, 0.08f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _drawerDismissLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        _drawerDismissLayer.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                CloseDrawers();
            }
        };
        AddToStage(_drawerDismissLayer);
    }

    private void BuildChatDrawer()
    {
        _leftDrawerPanel = FlatUi.Panel("ChatDrawer");
        _leftDrawerPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddToStage(_leftDrawerPanel);

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 12;
        box.OffsetTop = 10;
        box.OffsetRight = -12;
        box.OffsetBottom = -10;
        _leftDrawerPanel.AddChild(box);

        var chat = new ChatPanel();
        chat.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(chat);
    }

    private void BuildHistoryDrawer()
    {
        _sidePanel = FlatUi.Panel("SidePanel");
        _sidePanel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddToStage(_sidePanel);

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 12;
        box.OffsetTop = 10;
        box.OffsetRight = -12;
        box.OffsetBottom = -10;
        box.AddThemeConstantOverride("separation", 10);
        _sidePanel.AddChild(box);

        box.AddChild(FlatUi.Label("对局记录", 20));

        var debug = FlatUi.Panel("DebugPanel");
        debug.CustomMinimumSize = new Vector2(0, 122);
        box.AddChild(debug);
        var debugBox = new VBoxContainer();
        debugBox.SetAnchorsPreset(LayoutPreset.FullRect);
        debugBox.OffsetLeft = 10;
        debugBox.OffsetTop = 8;
        debugBox.OffsetRight = -10;
        debugBox.OffsetBottom = -8;
        debug.AddChild(debugBox);

        _aiToggle = new CheckButton { Text = "启用 AI 追加" };
        _aiToggle.AddThemeColorOverride("font_color", FlatUi.Text);
        _aiToggle.ButtonPressed = true;
        debugBox.AddChild(_aiToggle);

        var row = new HBoxContainer();
        row.AddChild(FlatUi.MutedLabel("AI 数量"));
        _aiCount = new SpinBox { MinValue = 1, MaxValue = Constants.MaxPlayers - 1, Value = 1, Step = 1, CustomMinimumSize = new Vector2(92, 34) };
        row.AddChild(_aiCount);
        var apply = FlatUi.Button("添加");
        apply.Pressed += ApplyAiSettings;
        row.AddChild(apply);
        debugBox.AddChild(row);

        var limitRow = new HBoxContainer();
        limitRow.AddChild(FlatUi.MutedLabel("桌面上限"));
        _chipLimitSpin = new SpinBox
        {
            MinValue = 1,
            MaxValue = 100000,
            Value = GameManager.Instance?.TableChipLimit ?? Constants.TableChipLimit,
            Step = 1,
            CustomMinimumSize = new Vector2(110, 34)
        };
        limitRow.AddChild(_chipLimitSpin);
        var applyLimit = FlatUi.Button("设置");
        applyLimit.Pressed += ApplyChipLimit;
        limitRow.AddChild(applyLimit);
        debugBox.AddChild(limitRow);

        var history = new HandHistoryPanel();
        history.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(history);
    }

    private void BuildBettingPanel()
    {
        _bettingPanel = new BettingPanel();
        _bettingPanel.SetAnchorsPreset(LayoutPreset.TopWide);
        _bettingPanel.OffsetLeft = 24;
        _bettingPanel.OffsetRight = -384;
        _bettingPanel.OffsetTop = -170;
        _bettingPanel.OffsetBottom = -18;
        _bettingPanel.ActionSubmitted += OnActionSubmitted;
        AddToStage(_bettingPanel);
    }

    private void BuildSettlementLayer()
    {
        _settlementLayer = new Control { Name = "SettlementLayer", MouseFilter = MouseFilterEnum.Ignore };
        _settlementLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        _settlementChipTexture = LoadTexture("res://assets/textures/chips/chipRedWhite.png");
        AddToStage(_settlementLayer);
    }

    private void BuildBustedPanel()
    {
        _bustedPanel = FlatUi.Panel("BustedPanel");
        _bustedPanel.Visible = false;
        _bustedPanel.SetAnchorsPreset(LayoutPreset.TopLeft);
        AddToStage(_bustedPanel);

        var box = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 10;
        box.OffsetTop = 8;
        box.OffsetRight = -10;
        box.OffsetBottom = -8;
        box.AddThemeConstantOverride("separation", 10);
        _bustedPanel.AddChild(box);

        _bustedLabel = FlatUi.Label("你已无筹码，已锁定暂离", 18, HorizontalAlignment.Center);
        box.AddChild(_bustedLabel);

        _rebuyButton = FlatUi.Button("补码", FlatUi.AccentMuted);
        _rebuyButton.Pressed += RebuyLocalPlayer;
        box.AddChild(_rebuyButton);

        var leave = FlatUi.Button("离开", FlatUi.Danger);
        leave.Pressed += LeaveTable;
        box.AddChild(leave);
    }

    private void WireSignals()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.WaitForSettlementAnimation = true;
        manager.StateChanged += _ => Refresh();
        manager.PlayerActionRequired += (_, _) => Refresh();
        manager.CardsDealt += (_, _) => Refresh();
        manager.PotUpdated += (_, _) => Refresh();
        manager.GameEnded += (_, _) => OnGameEnded();
    }

    private void Refresh()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        EnsureSeatHuds();
        var currentId = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        var localSeat = GetLocalSeatIndex();
        var seatCount = GetSeatCount();
        for (var visualSlot = 0; visualSlot < Constants.MaxPlayers; visualSlot++)
        {
            var hud = _seatHuds[visualSlot];
            if (visualSlot >= seatCount)
            {
                hud.Visible = false;
                continue;
            }

            ConfigureHudForSlot(hud, visualSlot, seatCount);
            hud.SetEmptySeat(GetRealSeatForVisualSlot(visualSlot, localSeat) + 1);
            hud.SetRevealActions(false, false, null, null);
        }

        foreach (var player in manager.Players)
        {
            var visualSlot = GetVisualSlotForSeat(player.Position, localSeat);
            if (visualSlot < 0 || visualSlot >= _seatHuds.Count)
            {
                continue;
            }

            var hud = _seatHuds[visualSlot];
            var reveal = player.Id == localId || manager.LastShownHands.ContainsKey(player.Id);
            var revealFirst = manager.IsHoleCardRevealed(player.Id, 0);
            var revealSecond = manager.IsHoleCardRevealed(player.Id, 1);
            ConfigureHudForSlot(hud, visualSlot, seatCount);
            hud.SetPlayer(player, reveal, revealFirst, revealSecond);
            var canRevealButtons = _revealWindowOpen && player.Id == localId && !manager.LastShownHands.ContainsKey(player.Id);
            hud.SetRevealActions(
                canRevealButtons && player.HoleCards[0] != null && !revealFirst,
                canRevealButtons && player.HoleCards[1] != null && !revealSecond,
                player.HoleCards[0],
                player.HoleCards[1]);
            hud.SetBlindRole(manager.GetBlindRole(player.Id));
            hud.SetTurnActive(player.Id == currentId);
            hud.SetWinnerState(manager.LastWinners.Contains(player.Id), manager.LastWinnings.GetValueOrDefault(player.Id, 0));
        }

        _communityCards?.SetCards(manager.CommunityCards);
        UpdatePotLabel();
        UpdateStateLabel();
        UpdateBettingPanel();
        UpdateBustedPanel();
        LayoutPlayerHuds();
    }

    private void EnsureSeatHuds()
    {
        if (_seatLayer == null)
        {
            return;
        }

        while (_seatHuds.Count < Constants.MaxPlayers)
        {
            var hud = new PlayerHUD { Name = $"SeatHUD_{_seatHuds.Count + 1}", MouseFilter = MouseFilterEnum.Stop };
            _seatLayer.AddChild(hud);
            _seatHuds.Add(hud);
        }
    }

    private void LayoutPlayerHuds()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        EnsureSeatHuds();
        var positions = GetSeatPositions();
        var seatCount = GetSeatCount();
        for (var visualSlot = 0; visualSlot < _seatHuds.Count; visualSlot++)
        {
            var hud = _seatHuds[visualSlot];
            if (visualSlot >= seatCount)
            {
                hud.Visible = false;
                continue;
            }

            hud.SetAnchorsPreset(LayoutPreset.TopLeft);
            ConfigureHudForSlot(hud, visualSlot, seatCount);
            hud.Position = positions[visualSlot] - hud.Size / 2f;
        }
    }

    private void ConfigureHudForSlot(PlayerHUD hud, int visualSlot, int seatCount)
    {
        var stageWidth = GetStageSize().X;
        if (seatCount > 9 && visualSlot != 0)
        {
            stageWidth *= 0.65f;
        }

        hud.ConfigureForStage(stageWidth, visualSlot == 0);
    }

    private List<Vector2> GetSeatPositions()
    {
        var stageSize = GetStageSize();
        var result = new List<Vector2>();
        var seatCount = GetSeatCount();
        for (var visualSlot = 0; visualSlot < seatCount; visualSlot++)
        {
            result.Add(GetSeatCenter(visualSlot, seatCount, stageSize));
        }

        return result;
    }

    private int GetLocalSeatIndex()
    {
        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        var player = GameManager.Instance?.Players.FirstOrDefault(item => item.Id == localId);
        return NormalizeSeat(player?.Position ?? 0);
    }

    private static int GetVisualSlotForSeat(int seat, int localSeat)
    {
        return NormalizeSeat(seat - localSeat);
    }

    private static int GetRealSeatForVisualSlot(int visualSlot, int localSeat)
    {
        return NormalizeSeat(localSeat + visualSlot);
    }

    private static int NormalizeSeat(int seat)
    {
        var seatCount = GameManager.Instance?.TableSeatCount ?? Constants.MaxPlayers;
        var value = seat % seatCount;
        return value < 0 ? value + seatCount : value;
    }

    private int GetSeatCount()
    {
        return Mathf.Clamp(GameManager.Instance?.TableSeatCount ?? Constants.MaxPlayers, 2, Constants.MaxPlayers);
    }

    private static Vector2 GetSeatCenter(int visualSlot, int seatCount, Vector2 stageSize)
    {
        var twelveSeats = visualSlot switch
        {
            0 => new Vector2(0.50f, 0.835f),
            1 => new Vector2(0.12f, 0.735f),
            2 => new Vector2(0.12f, 0.570f),
            3 => new Vector2(0.12f, 0.405f),
            4 => new Vector2(0.12f, 0.240f),
            5 => new Vector2(0.28f, 0.125f),
            6 => new Vector2(0.50f, 0.105f),
            7 => new Vector2(0.72f, 0.125f),
            8 => new Vector2(0.88f, 0.240f),
            9 => new Vector2(0.88f, 0.405f),
            10 => new Vector2(0.88f, 0.570f),
            11 => new Vector2(0.88f, 0.735f),
            _ => new Vector2(0.50f, 0.50f)
        };

        var nineSeats = visualSlot switch
        {
            0 => new Vector2(0.50f, 0.835f),
            1 => new Vector2(0.12f, 0.695f),
            2 => new Vector2(0.12f, 0.500f),
            3 => new Vector2(0.12f, 0.305f),
            4 => new Vector2(0.38f, 0.145f),
            5 => new Vector2(0.62f, 0.145f),
            6 => new Vector2(0.88f, 0.305f),
            7 => new Vector2(0.88f, 0.500f),
            8 => new Vector2(0.88f, 0.695f),
            _ => twelveSeats
        };

        var normalized = seatCount <= 9 ? nineSeats : twelveSeats;
        return new Vector2(stageSize.X * normalized.X, stageSize.Y * normalized.Y);
    }

    private void UpdatePotLabel()
    {
        var manager = GameManager.Instance;
        if (manager == null || _potLabel == null)
        {
            return;
        }

        _potLabel.Text = $"底池: {GetTotalPot()}";
        if (_currentBetLabel != null)
        {
            _currentBetLabel.Text = $"当前加注: {manager.CurrentBettingRound?.CurrentBet ?? 0}";
        }
    }

    private void UpdateStateLabel()
    {
        var manager = GameManager.Instance;
        if (manager == null || _stateLabel == null)
        {
            return;
        }

        var currentId = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        var currentName = currentId > 0 ? manager.Players.Find(player => player.Id == currentId)?.Name ?? currentId.ToString() : "-";
        _stateLabel.Text = $"{manager.CurrentState.ToDisplayName()}  ·  当前行动: {currentName}";
        if (_turnPromptLabel != null)
        {
            var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
            _turnPromptLabel.Text = currentId == localId ? "轮到你行动" : currentId > 0 ? $"等待 {currentName}" : manager.CurrentState.ToDisplayName();
            _turnPromptLabel.AddThemeColorOverride("font_color", currentId == localId ? new Color(1.0f, 0.82f, 0.32f) : FlatUi.Accent);
            if (currentId == localId)
            {
                var tween = CreateTween();
                tween.TweenProperty(_turnPromptLabel, "scale", new Vector2(1.08f, 1.08f), 0.18);
                tween.TweenProperty(_turnPromptLabel, "scale", Vector2.One, 0.18);
            }
        }
    }

    private void UpdateTurnTimerVisual()
    {
        if (_turnTimerBar == null || _turnTimerLabel == null)
        {
            return;
        }

        var manager = GameManager.Instance;
        var limit = manager?.CurrentTurnTimeLimitSeconds ?? 0;
        var currentId = manager?.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        if (manager == null || limit <= 0 || currentId <= 0 || manager.CurrentTurnStartedMsec <= 0)
        {
            _turnTimerBar.Visible = false;
            _turnTimerLabel.Visible = false;
            return;
        }

        var elapsed = (Time.GetTicksMsec() - manager.CurrentTurnStartedMsec) / 1000.0;
        var remaining = Mathf.Clamp((float)(limit - elapsed), 0f, limit);
        var progress = limit <= 0 ? 0f : remaining / limit;
        _turnTimerBar.Visible = true;
        _turnTimerLabel.Visible = true;
        _turnTimerBar.Value = progress;
        var fill = new StyleBoxFlat
        {
            BgColor = progress < 0.25f ? FlatUi.Danger : new Color(1.0f, 0.82f, 0.32f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
        var background = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.07f, 0.92f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
        _turnTimerBar.AddThemeStyleboxOverride("fill", fill);
        _turnTimerBar.AddThemeStyleboxOverride("background", background);
        _turnTimerLabel.Text = $"{Mathf.CeilToInt(remaining)}s";
    }


    private void UpdateBettingPanel()
    {
        var manager = GameManager.Instance;
        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        var player = manager?.Players.Find(item => item.Id == localId);
        if (manager?.CurrentBettingRound == null || player == null || _bettingPanel == null)
        {
            _bettingPanel?.UpdateState(0, 0, 0, GameManager.Instance?.BigBlindAmount ?? Constants.BigBlind, 0, false, GameManager.Instance?.SmallBlindAmount ?? Constants.SmallBlind);
            return;
        }

        var bet = manager.CurrentBettingRound.PlayerBets.GetValueOrDefault(localId, player.CurrentBet);
        var localTurn = manager.CurrentBettingRound.GetCurrentPlayerId() == localId && !player.IsFolded && !player.IsAllIn && !player.IsSittingOut;
        var raiseAction = manager.CurrentBettingRound.CurrentBet == 0 ? PlayerAction.Bet : PlayerAction.Raise;
        var suggestedRaise = manager.CurrentBettingRound.CurrentBet == 0
            ? manager.BigBlindAmount
            : manager.CurrentBettingRound.CurrentBet + manager.CurrentBettingRound.LastRaiseAmount;
        var canBetOrRaiseByRule = manager.CurrentBettingRound.IsValidAction(localId, raiseAction, suggestedRaise);
        _bettingPanel.UpdateState(
            manager.CurrentBettingRound.CurrentBet,
            bet,
            player.Chips,
            manager.CurrentBettingRound.LastRaiseAmount,
            GetTotalPot(),
            localTurn,
            manager.SmallBlindAmount,
            canBetOrRaiseByRule);
    }

    private void ToggleSidePanel()
    {
        var willOpen = _sideCollapsed;
        _sideCollapsed = !willOpen;
        if (willOpen)
        {
            _chatCollapsed = true;
        }

        if (_sidePanel != null)
        {
            _sidePanel.Visible = !_sideCollapsed;
        }

        if (_leftDrawerPanel != null)
        {
            _leftDrawerPanel.Visible = !_chatCollapsed;
        }

        if (_sideToggle != null)
        {
            _sideToggle.Text = _sideCollapsed ? "记录" : "收起";
        }

        if (_bettingPanel != null)
        {
            _bettingPanel.OffsetRight = -24;
        }

        ApplyResponsiveLayout();
        LayoutPlayerHuds();
    }

    private void ToggleChatPanel()
    {
        var willOpen = _chatCollapsed;
        _chatCollapsed = !willOpen;
        if (willOpen)
        {
            _sideCollapsed = true;
        }

        if (_sidePanel != null)
        {
            _sidePanel.Visible = !_sideCollapsed;
        }

        if (_leftDrawerPanel != null)
        {
            _leftDrawerPanel.Visible = !_chatCollapsed;
        }

        if (_chatToggle != null)
        {
            _chatToggle.Text = _chatCollapsed ? "聊天" : "收起";
        }

        ApplyResponsiveLayout();
        LayoutPlayerHuds();
    }

    private void CloseDrawers()
    {
        _sideCollapsed = true;
        _chatCollapsed = true;
        if (_sidePanel != null)
        {
            _sidePanel.Visible = false;
        }

        if (_leftDrawerPanel != null)
        {
            _leftDrawerPanel.Visible = false;
        }

        if (_drawerDismissLayer != null)
        {
            _drawerDismissLayer.Visible = false;
        }

        ApplyResponsiveLayout();
        LayoutPlayerHuds();
    }

    private void ApplyAiSettings()
    {
        if (_aiToggle?.ButtonPressed != true)
        {
            return;
        }

        GameManager.Instance?.AddAiPlayers((int)(_aiCount?.Value ?? 1));

        if (GameManager.Instance?.CurrentState is GameState.Menu or GameState.Lobby or GameState.GameOver)
        {
            GameManager.Instance?.StartGame();
        }
        Refresh();
    }

    private void ApplyChipLimit()
    {
        var limit = (int)(_chipLimitSpin?.Value ?? Constants.TableChipLimit);
        GameManager.Instance?.SetTableChipLimit(limit);
        Refresh();
    }

    private void ToggleSitOut()
    {
        if (_syncingSitOutToggle)
        {
            return;
        }

        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        GameManager.Instance?.SetSittingOut(localId, _sitOutToggle?.ButtonPressed == true);
        Refresh();
    }

    private void RebuyLocalPlayer()
    {
        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        GameManager.Instance?.RebuyPlayer(localId);
        Refresh();
    }

    private void LeaveTable()
    {
        GameManager.Instance?.LeaveTable();
        NetworkManager.Instance?.LeaveRoom();
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
    }

    private void OnActionSubmitted(int action, int amount)
    {
        var manager = GameManager.Instance;
        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        if (manager?.CurrentBettingRound?.GetCurrentPlayerId() != localId)
        {
            return;
        }

        Logger.Info($"[LOCAL_UI] Player{localId} {(PlayerAction)action} {amount}");
        NetworkManager.Instance?.SubmitLocalAction(localId, (PlayerAction)action, amount);
    }

    private async void OnGameEnded()
    {
        Refresh();
        await RunRevealWindow();
        await PlaySettlementSequence();
        GameManager.Instance?.CompleteSettlementAnimation();
        Refresh();
    }

    private async Task RunRevealWindow()
    {
        _revealWindowOpen = true;
        Refresh();
        for (var remaining = 5; remaining > 0; remaining--)
        {
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        }

        _revealWindowOpen = false;
        Refresh();
    }

    private async Task PlaySettlementSequence()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        _settlementLayer?.MoveToFront();
        foreach (var award in manager.LastPotAwards)
        {
            await AnimatePotAward(award);
        }
    }

    private async Task AnimatePotAward(PotAward award)
    {
        if (_settlementLayer == null || _centerPanel == null || award.Shares.Count == 0)
        {
            return;
        }

        var start = GetSettlementLocalCenter(_centerPanel);
        var tween = CreateTween();
        tween.SetParallel(true);
        var index = 0;
        foreach (var pair in award.Shares.OrderBy(item => item.Key))
        {
            var hud = FindHudForPlayer(pair.Key);
            if (hud == null)
            {
                continue;
            }

            var target = GetSettlementLocalCenter(hud);
            var chip = CreateFlyingChip(pair.Value);
            var spread = new Vector2((index - (award.Shares.Count - 1) / 2f) * 18f, -Mathf.Abs(index - 0.5f) * 8f);
            chip.Position = start + spread - chip.Size / 2f;
            chip.Scale = new Vector2(0.72f, 0.72f);
            chip.Modulate = new Color(1f, 1f, 1f, 0.96f);
            _settlementLayer.AddChild(chip);

            var duration = 0.42 + Mathf.Min(0.22, award.PotIndex * 0.04);
            tween.TweenProperty(chip, "position", target - chip.Size / 2f, duration).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(chip, "rotation_degrees", 360f + 70f * index, duration);
            tween.Parallel().TweenProperty(chip, "scale", Vector2.One, duration * 0.62).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(chip, "modulate:a", 0.0f, 0.16).SetDelay(duration * 0.92);
            tween.TweenCallback(Callable.From(() => chip.QueueFree())).SetDelay(duration + 0.18);
            hud.PlayPotReceived(pair.Value);
            index++;
        }

        await ToSignal(tween, Tween.SignalName.Finished);
        ClearSettlementLayer();
        await ToSignal(GetTree().CreateTimer(0.16), SceneTreeTimer.SignalName.Timeout);
    }

    private void ClearSettlementLayer()
    {
        if (_settlementLayer == null)
        {
            return;
        }

        foreach (var child in _settlementLayer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private Control CreateFlyingChip(int amount)
    {
        var root = new Control
        {
            Size = new Vector2(78, 64),
            PivotOffset = new Vector2(39, 32),
            MouseFilter = MouseFilterEnum.Ignore
        };

        var chip = new TextureRect
        {
            Texture = _settlementChipTexture,
            Position = new Vector2(14, 0),
            Size = new Vector2(50, 50),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(chip);

        var label = FlatUi.Label($"+{amount}", 14, HorizontalAlignment.Center);
        label.Position = new Vector2(0, 42);
        label.Size = new Vector2(78, 22);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.34f));
        root.AddChild(label);
        return root;
    }

    private PlayerHUD? FindHudForPlayer(int playerId)
    {
        return _seatHuds.FirstOrDefault(hud => hud.Visible && hud.PlayerId == playerId);
    }

    private Vector2 GetSettlementLocalCenter(Control control)
    {
        if (_settlementLayer == null)
        {
            return control.Position + control.Size / 2f;
        }

        var rect = control.GetGlobalRect();
        var globalCenter = rect.Position + rect.Size / 2f;
        return _settlementLayer.GetGlobalTransformWithCanvas().AffineInverse() * globalCenter;
    }

    private void ApplyResponsiveLayout()
    {
        UpdateStageRect();
        var size = GetStageSize();
        var margin = Mathf.Clamp(size.X * 0.025f, 8f, 24f);
        var topHeight = Mathf.Clamp(size.Y * 0.035f, 52f, 82f);
        var localHudHeight = Mathf.Clamp(size.X * 0.52f, 310f, 590f);
        var centerWidth = Mathf.Clamp(size.X * 0.58f, 260f, 560f);
        var centerHeight = Mathf.Clamp(size.Y * 0.090f, 132f, 210f);

        _communityCards?.SetCardSize(new Vector2(Mathf.Clamp(size.X * 0.086f, 42f, 86f), Mathf.Clamp(size.X * 0.086f, 42f, 86f) * 1.43f));

        if (_drawerDismissLayer != null)
        {
            _drawerDismissLayer.Visible = !_sideCollapsed || !_chatCollapsed;
            _drawerDismissLayer.OffsetLeft = 0;
            _drawerDismissLayer.OffsetTop = 0;
            _drawerDismissLayer.OffsetRight = 0;
            _drawerDismissLayer.OffsetBottom = 0;
            if (_drawerDismissLayer.Visible)
            {
                _drawerDismissLayer.MoveToFront();
            }
        }

        if (_sidePanel != null)
        {
            _sidePanel.Visible = !_sideCollapsed;
            var drawerWidth = Mathf.Clamp(size.X * 0.70f, 360f, size.X - margin * 2f);
            _sidePanel.OffsetLeft = size.X - drawerWidth - margin;
            _sidePanel.OffsetTop = topHeight + margin;
            _sidePanel.OffsetRight = -margin;
            _sidePanel.OffsetBottom = -margin;
            if (!_sideCollapsed)
            {
                _sidePanel.MoveToFront();
            }
        }

        if (_leftDrawerPanel != null)
        {
            _leftDrawerPanel.Visible = !_chatCollapsed;
            var drawerWidth = Mathf.Clamp(size.X * 0.70f, 360f, size.X - margin * 2f);
            _leftDrawerPanel.OffsetLeft = margin;
            _leftDrawerPanel.OffsetTop = topHeight + margin;
            _leftDrawerPanel.OffsetRight = -(size.X - drawerWidth - margin);
            _leftDrawerPanel.OffsetBottom = -margin;
            if (!_chatCollapsed)
            {
                _leftDrawerPanel.MoveToFront();
            }
        }

        if (_sideToggle != null)
        {
            _sideToggle.Text = _sideCollapsed ? "记录" : "收起";
            _sideToggle.CustomMinimumSize = new Vector2(58, 36);
        }

        if (_chatToggle != null)
        {
            _chatToggle.Text = _chatCollapsed ? "聊天" : "收起";
            _chatToggle.CustomMinimumSize = new Vector2(58, 36);
        }

        if (_aiCount != null)
        {
            var realPlayers = GameManager.Instance?.Players.Count(player => GameManager.Instance.IsAiPlayer(player.Id) == false) ?? 1;
            _aiCount.MaxValue = Mathf.Max(1, GetSeatCount() - realPlayers);
        }

        if (_restartButton != null)
        {
            _restartButton.Text = "新局";
            _restartButton.CustomMinimumSize = new Vector2(58, 36);
        }

        if (_sitOutToggle != null)
        {
            var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
            _syncingSitOutToggle = true;
            _sitOutToggle.ButtonPressed = GameManager.Instance?.Players.Find(player => player.Id == localId)?.WantsSitOutNextHand == true;
            _syncingSitOutToggle = false;
            _sitOutToggle.CustomMinimumSize = new Vector2(72, 36);
        }

        if (_leaveButton != null)
        {
            _leaveButton.CustomMinimumSize = new Vector2(58, 36);
        }

        if (_topBar != null)
        {
            _topBar.OffsetLeft = margin;
            _topBar.OffsetTop = margin;
            _topBar.OffsetRight = -margin;
            _topBar.OffsetBottom = margin + topHeight;
            _topBar.AddThemeConstantOverride("separation", Mathf.RoundToInt(Mathf.Clamp(size.X * 0.012f, 4f, 10f)));
        }

        if (_centerPanel != null)
        {
            _centerPanel.OffsetLeft = -centerWidth / 2f;
            _centerPanel.OffsetRight = centerWidth / 2f;
            _centerPanel.OffsetTop = -centerHeight / 2f - size.Y * 0.155f;
            _centerPanel.OffsetBottom = centerHeight / 2f - size.Y * 0.155f;
        }

        if (_bettingPanel != null)
        {
            var actionTop = size.Y - localHudHeight - Mathf.Clamp(size.Y * 0.075f, 86f, 150f);
            _bettingPanel.OffsetLeft = margin;
            _bettingPanel.OffsetRight = -margin;
            _bettingPanel.OffsetTop = Mathf.Clamp(actionTop, size.Y * 0.50f, size.Y * 0.64f);
            _bettingPanel.OffsetBottom = -margin;
            _bettingPanel.ConfigureForStage(size.X, new Vector2(size.X - margin * 2f, size.Y - _bettingPanel.OffsetTop - margin));
            _bettingPanel.MoveToFront();
        }

        if (_bustedPanel != null)
        {
            var panelWidth = Mathf.Clamp(size.X * 0.76f, 320f, 720f);
            var panelHeight = Mathf.Clamp(size.X * 0.07f, 54f, 78f);
            _bustedPanel.Position = new Vector2((size.X - panelWidth) / 2f, size.Y * 0.72f);
            _bustedPanel.Size = new Vector2(panelWidth, panelHeight);
            _bustedPanel.MoveToFront();
        }

        if (_settlementLayer != null)
        {
            _settlementLayer.SetAnchorsPreset(LayoutPreset.FullRect);
            _settlementLayer.OffsetLeft = 0;
            _settlementLayer.OffsetTop = 0;
            _settlementLayer.OffsetRight = 0;
            _settlementLayer.OffsetBottom = 0;
        }
    }

    private int GetTotalPot()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return 0;
        }

        var totalPot = manager.PotManager.MainPot;
        foreach (var sidePot in manager.PotManager.SidePots)
        {
            totalPot += sidePot.Amount;
        }

        if (manager.CurrentBettingRound != null)
        {
            totalPot += manager.CurrentBettingRound.PlayerBets.Values.Sum();
        }

        return totalPot;
    }

    private void UpdateBustedPanel()
    {
        if (_bustedPanel == null)
        {
            return;
        }

        var localId = PlayerData.Instance?.LocalPlayerId ?? 1;
        var player = GameManager.Instance?.Players.Find(item => item.Id == localId);
        var busted = player != null && player.Chips <= 0;
        _bustedPanel.Visible = busted;
        if (_bustedLabel != null && player != null)
        {
            _bustedLabel.Text = player.AccountBalance > 0
                ? $"你已无筹码，等待补码\n账户: {player.AccountBalance}"
                : "你已无筹码，等待补码";
        }

        if (_rebuyButton != null)
        {
            _rebuyButton.Text = $"补码 {GameManager.Instance?.MaxBuyIn ?? Constants.MaxBuyIn}";
        }
    }

    private static Texture2D? LoadTexture(string path)
    {
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private void AddToStage(Node node)
    {
        (_stage ?? this).AddChild(node);
    }

    private void UpdateStageRect()
    {
        if (_stage == null)
        {
            return;
        }

        var viewport = GetViewportRect().Size;
        var stageWidth = viewport.X;
        var stageHeight = stageWidth / StageAspect;
        if (stageHeight > viewport.Y)
        {
            stageHeight = viewport.Y;
            stageWidth = stageHeight * StageAspect;
        }

        _stage.SetAnchorsPreset(LayoutPreset.TopLeft);
        _stage.Position = new Vector2((viewport.X - stageWidth) / 2f, (viewport.Y - stageHeight) / 2f);
        _stage.Size = new Vector2(stageWidth, stageHeight);
        _stage.ClipContents = true;
    }

    private Vector2 GetStageSize()
    {
        return _stage?.Size ?? GetViewportRect().Size;
    }
}
