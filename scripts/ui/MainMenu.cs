using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class MainMenu : Control
{
    [Export] public bool ShowTestModeButton { get; set; }

    private LineEdit? _joinAddressInput;
    private LineEdit? _joinPortInput;
    private TextEdit? _joinInviteInput;
    private Label? _joinStatusLabel;
    private ColorRect? _joinOverlay;
    private Panel? _joinPanel;
    private OptionButton? _seatCountOption;
    private VBoxContainer? _discoveredRoomsList;
    private Label? _discoveryStatusLabel;
    private Label? _updateStatusLabel;
    private Button? _downloadUpdateButton;
    private Panel? _addressKeypadPanel;
    private CenterContainer? _mainCenter;
    private Panel? _menuPanel;
    private CenterContainer? _joinCenter;
    private LineEdit? _keypadTarget;
    private Label? _keypadTargetLabel;
    private Button? _keypadDotButton;
    private Button? _keypadColonButton;
    private string _availableApkUrl = "";
    private readonly Dictionary<string, Button> _discoveredRoomButtons = new();
    private Vector2 _lastResponsiveViewport = new(-1, -1);

    public override void _Ready()
    {
        BuildUi();
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.JoinSucceeded += OnJoinSucceeded;
            NetworkManager.Instance.JoinFailed += OnJoinFailed;
            NetworkManager.Instance.LanRoomDiscovered += OnLanRoomDiscovered;
            NetworkManager.Instance.LanDiscoveryFinished += OnLanDiscoveryFinished;
        }
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.StatusChanged += OnUpdateStatusChanged;
            UpdateManager.Instance.AppUpdateAvailable += OnAppUpdateAvailable;
            OnUpdateStatusChanged(UpdateManager.Instance.StatusText, UpdateManager.Instance.StatusIsDanger);
            var manifest = UpdateManager.Instance.LastManifest;
            if (manifest != null &&
                (UpdatePolicy.CompareVersions(Constants.AppVersion, manifest.LatestAppVersion) < 0 ||
                 UpdatePolicy.CompareVersions(Constants.AppVersion, manifest.MinimumAppVersion) < 0 ||
                 manifest.ProtocolVersion != Constants.NetworkProtocolVersion))
            {
                var required = UpdatePolicy.CompareVersions(Constants.AppVersion, manifest.MinimumAppVersion) < 0 ||
                               manifest.ProtocolVersion != Constants.NetworkProtocolVersion;
                OnAppUpdateAvailable(manifest.LatestAppVersion, manifest.ApkUrl, required);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            ApplyResponsiveLayout();
        }
    }

    public override void _ExitTree()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.JoinSucceeded -= OnJoinSucceeded;
            NetworkManager.Instance.JoinFailed -= OnJoinFailed;
            NetworkManager.Instance.LanRoomDiscovered -= OnLanRoomDiscovered;
            NetworkManager.Instance.LanDiscoveryFinished -= OnLanDiscoveryFinished;
            NetworkManager.Instance.StopLanRoomScan();
        }
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.StatusChanged -= OnUpdateStatusChanged;
            UpdateManager.Instance.AppUpdateAvailable -= OnAppUpdateAvailable;
        }
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Name = "Background", Color = FlatUi.Background };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { Name = "MainCenter" };
        _mainCenter = center;
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.OffsetLeft = 48;
        center.OffsetTop = 48;
        center.OffsetRight = -48;
        center.OffsetBottom = -48;
        AddChild(center);

        var root = new VBoxContainer { Name = "MainLayout" };
        root.Alignment = BoxContainer.AlignmentMode.Center;
        root.CustomMinimumSize = new Vector2(440, 0);
        root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        root.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        root.AddThemeConstantOverride("separation", 18);
        center.AddChild(root);

        var menuPanel = FlatUi.Panel("MenuPanel");
        _menuPanel = menuPanel;
        menuPanel.CustomMinimumSize = new Vector2(440, 520);
        root.AddChild(menuPanel);

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsPreset(LayoutPreset.FullRect);
        menu.OffsetLeft = 28;
        menu.OffsetTop = 28;
        menu.OffsetRight = -28;
        menu.OffsetBottom = -28;
        menu.AddThemeConstantOverride("separation", 14);
        menuPanel.AddChild(menu);

        var title = FlatUi.Label("Texas Hold'em", 48, HorizontalAlignment.Center);
        menu.AddChild(title);
        var isAndroid = OS.GetName() == "Android";
        menu.AddChild(FlatUi.MutedLabel(isAndroid
            ? "创建房间，或自动发现并加入同一网络内的牌局。"
            : "创建房间、加入局域网对局，或直接用 AI 进行本地测试。"));

        var roomRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        roomRow.AddChild(FlatUi.MutedLabel("房间人数"));
        _seatCountOption = new OptionButton { CustomMinimumSize = new Vector2(120, 40) };
        _seatCountOption.AddItem("9人局", 9);
        _seatCountOption.AddItem("12人局", 12);
        _seatCountOption.Selected = 0;
        roomRow.AddChild(_seatCountOption);
        menu.AddChild(roomRow);

        AddMenuButton(menu, "创建房间", OnCreateRoomPressed, FlatUi.AccentMuted);
        AddMenuButton(menu, "加入房间", ShowJoinPanel);
        AddMenuButton(menu, "设置", () => GetTree().ChangeSceneToFile(Constants.SettingsScene));

        if (!isAndroid)
        {
            var testButton = AddMenuButton(menu, "测试模式", OnTestModePressed);
            testButton.Visible = ShowTestModeButton;
        }

        _updateStatusLabel = FlatUi.MutedLabel($"版本 {Constants.AppVersion}");
        _updateStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _updateStatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        menu.AddChild(_updateStatusLabel);

        _downloadUpdateButton = FlatUi.Button("获取新版本", FlatUi.AccentMuted);
        _downloadUpdateButton.Visible = false;
        _downloadUpdateButton.Pressed += OpenAvailableUpdate;
        menu.AddChild(_downloadUpdateButton);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        menu.AddChild(spacer);
        AddMenuButton(menu, "退出游戏", () => GetTree().Quit(), FlatUi.Danger);

        _joinOverlay = new ColorRect
        {
            Name = "JoinOverlay",
            Color = new Color(0, 0, 0, 0.52f),
            Visible = false
        };
        _joinOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_joinOverlay);

        var joinCenter = new CenterContainer();
        _joinCenter = joinCenter;
        joinCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        joinCenter.OffsetLeft = 24;
        joinCenter.OffsetTop = 24;
        joinCenter.OffsetRight = -24;
        joinCenter.OffsetBottom = -24;
        _joinOverlay.AddChild(joinCenter);

        _joinPanel = FlatUi.Panel("JoinPanel");
        _joinPanel.CustomMinimumSize = new Vector2(500, 800);
        joinCenter.AddChild(_joinPanel);

        BuildJoinPanel();
        BuildAddressKeypad();
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0 || _menuPanel == null || viewport.IsEqualApprox(_lastResponsiveViewport))
        {
            return;
        }
        _lastResponsiveViewport = viewport;

        var safe = ResponsiveUi.GetSafeMargins(this);
        var margin = ResponsiveUi.MarginFor(viewport);
        if (_mainCenter != null)
        {
            ResponsiveUi.ApplySafeCenter(_mainCenter, this, margin);
        }

        if (_menuPanel != null)
        {
            var preferredHeight = OS.GetName() == "Android" ? 620f : 710f;
            _menuPanel.CustomMinimumSize = ResponsiveUi.FitPanel(viewport, safe, 440f, preferredHeight, margin);
        }

        if (_joinCenter != null)
        {
            ResponsiveUi.ApplySafeCenter(_joinCenter, this, margin);
        }

        if (_joinPanel != null)
        {
            _joinPanel.CustomMinimumSize = ResponsiveUi.FitPanel(viewport, safe, 520f, 860f, margin);
        }

        if (_addressKeypadPanel != null)
        {
            var keypadMargin = margin + Mathf.Max(safe.Left, safe.Right);
            var keypadWidth = Mathf.Min(520f, Mathf.Max(300f, viewport.X - keypadMargin * 2f));
            var keypadHeight = Mathf.Clamp(viewport.Y * 0.23f, 380f, 460f);
            _addressKeypadPanel.OffsetLeft = -keypadWidth / 2f;
            _addressKeypadPanel.OffsetRight = keypadWidth / 2f;
            _addressKeypadPanel.OffsetTop = -(keypadHeight + safe.Bottom + margin);
            _addressKeypadPanel.OffsetBottom = -(safe.Bottom + margin);
        }

        ResponsiveUi.EnsureTouchTargets(this);
    }

    private Button AddMenuButton(VBoxContainer parent, string text, System.Action action, Color? color = null)
    {
        var button = FlatUi.Button(text, color);
        button.CustomMinimumSize = new Vector2(280, 44);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void BuildJoinPanel()
    {
        if (_joinPanel == null)
        {
            return;
        }

        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.OffsetLeft = 24;
        scroll.OffsetTop = 24;
        scroll.OffsetRight = -24;
        scroll.OffsetBottom = -24;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _joinPanel.AddChild(scroll);

        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(box);

        var header = new HBoxContainer();
        header.AddChild(FlatUi.Label("加入房间", 28));
        var headerSpacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(headerSpacer);
        var close = FlatUi.Button("关闭");
        close.CustomMinimumSize = new Vector2(72, 36);
        close.Pressed += HideJoinPanel;
        header.AddChild(close);
        box.AddChild(header);

        var hint = FlatUi.MutedLabel("可粘贴 Host 复制的邀请信息，也可直接输入 IP:端口。跨网络时填写公网 IP 和已开放端口。");
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(hint);

        var discoveryHeader = new HBoxContainer();
        discoveryHeader.AddChild(FlatUi.Label("同一网络房间", 19));
        var discoverySpacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        discoveryHeader.AddChild(discoverySpacer);
        var refreshDiscovery = FlatUi.Button("重新搜索");
        refreshDiscovery.CustomMinimumSize = new Vector2(96, 36);
        refreshDiscovery.Pressed += StartLanDiscovery;
        discoveryHeader.AddChild(refreshDiscovery);
        box.AddChild(discoveryHeader);

        _discoveryStatusLabel = FlatUi.MutedLabel("打开页面后自动搜索");
        box.AddChild(_discoveryStatusLabel);

        var discoveredPanel = FlatUi.Panel("DiscoveredRooms");
        discoveredPanel.CustomMinimumSize = new Vector2(0, 154);
        box.AddChild(discoveredPanel);
        _discoveredRoomsList = new VBoxContainer();
        _discoveredRoomsList.SetAnchorsPreset(LayoutPreset.FullRect);
        _discoveredRoomsList.OffsetLeft = 10;
        _discoveredRoomsList.OffsetTop = 8;
        _discoveredRoomsList.OffsetRight = -10;
        _discoveredRoomsList.OffsetBottom = -8;
        _discoveredRoomsList.AddThemeConstantOverride("separation", 6);
        discoveredPanel.AddChild(_discoveredRoomsList);

        _joinInviteInput = new TextEdit
        {
            PlaceholderText = "粘贴邀请信息，例如 {\"ip\":\"192.168.1.100\",\"port\":7000,...}",
            CustomMinimumSize = new Vector2(0, 96),
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        box.AddChild(_joinInviteInput);

        _joinAddressInput = new LineEdit
        {
            Name = "JoinAddressInput",
            PlaceholderText = "IP 或 IP:端口，例如 192.168.1.100:7000",
            Text = "127.0.0.1",
            CustomMinimumSize = new Vector2(280, 44),
            MaxLength = 64,
            VirtualKeyboardEnabled = false
        };
        _joinAddressInput.FocusEntered += () => ShowAddressKeypad(_joinAddressInput, "地址");
        box.AddChild(_joinAddressInput);

        _joinPortInput = new LineEdit
        {
            Name = "JoinPortInput",
            PlaceholderText = "端口",
            Text = Constants.DefaultPort.ToString(),
            CustomMinimumSize = new Vector2(280, 44),
            MaxLength = 5,
            VirtualKeyboardEnabled = false
        };
        _joinPortInput.FocusEntered += () => ShowAddressKeypad(_joinPortInput, "端口");
        box.AddChild(_joinPortInput);

        _joinStatusLabel = FlatUi.MutedLabel("");
        _joinStatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_joinStatusLabel);

        var connect = FlatUi.Button("连接", FlatUi.AccentMuted);
        connect.CustomMinimumSize = new Vector2(280, 46);
        connect.Pressed += ConnectToAddress;
        box.AddChild(connect);

        var clear = FlatUi.Button("清空输入");
        clear.Pressed += () =>
        {
            if (_joinAddressInput != null)
            {
                _joinAddressInput.Text = "";
            }

            if (_joinInviteInput != null)
            {
                _joinInviteInput.Text = "";
            }

            if (_joinPortInput != null)
            {
                _joinPortInput.Text = Constants.DefaultPort.ToString();
            }

            _joinAddressInput?.GrabFocus();
        };
        box.AddChild(clear);

        var footer = FlatUi.MutedLabel("同 Wi-Fi 推荐使用局域网 IP；不同网络需要 Host 开放 UDP 端口，或后续接入中继/房间服务器。");
        footer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(footer);
    }

    private void BuildAddressKeypad()
    {
        if (_joinOverlay == null)
        {
            return;
        }

        _addressKeypadPanel = FlatUi.Panel("AddressKeypad");
        _addressKeypadPanel.AnchorLeft = 0.5f;
        _addressKeypadPanel.AnchorTop = 1f;
        _addressKeypadPanel.AnchorRight = 0.5f;
        _addressKeypadPanel.AnchorBottom = 1f;
        _addressKeypadPanel.OffsetLeft = -246;
        _addressKeypadPanel.OffsetTop = -326;
        _addressKeypadPanel.OffsetRight = 246;
        _addressKeypadPanel.OffsetBottom = -20;
        _addressKeypadPanel.Visible = false;
        _addressKeypadPanel.ZIndex = 50;
        _joinOverlay.AddChild(_addressKeypadPanel);

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 12;
        box.OffsetTop = 10;
        box.OffsetRight = -12;
        box.OffsetBottom = -10;
        box.AddThemeConstantOverride("separation", 8);
        _addressKeypadPanel.AddChild(box);

        var header = new HBoxContainer();
        _keypadTargetLabel = FlatUi.Label("输入地址", 18);
        header.AddChild(_keypadTargetLabel);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);
        var close = FlatUi.Button("关闭");
        close.CustomMinimumSize = new Vector2(72, 34);
        close.Pressed += HideAddressKeypad;
        header.AddChild(close);
        box.AddChild(header);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(grid);

        AddKeypadButton(grid, "1", () => AppendKeypadText("1"));
        AddKeypadButton(grid, "2", () => AppendKeypadText("2"));
        AddKeypadButton(grid, "3", () => AppendKeypadText("3"));
        AddKeypadButton(grid, "←", BackspaceKeypad);
        AddKeypadButton(grid, "4", () => AppendKeypadText("4"));
        AddKeypadButton(grid, "5", () => AppendKeypadText("5"));
        AddKeypadButton(grid, "6", () => AppendKeypadText("6"));
        _keypadDotButton = AddKeypadButton(grid, ".", () => AppendKeypadText("."));
        AddKeypadButton(grid, "7", () => AppendKeypadText("7"));
        AddKeypadButton(grid, "8", () => AppendKeypadText("8"));
        AddKeypadButton(grid, "9", () => AppendKeypadText("9"));
        _keypadColonButton = AddKeypadButton(grid, ":", () => AppendKeypadText(":"));
        AddKeypadButton(grid, "清空", ClearKeypadTarget, FlatUi.SurfaceAlt);
        AddKeypadButton(grid, "0", () => AppendKeypadText("0"));
        AddKeypadButton(grid, "收起", HideAddressKeypad, FlatUi.SurfaceAlt);
        AddKeypadButton(grid, "连接", () =>
        {
            HideAddressKeypad();
            ConnectToAddress();
        }, FlatUi.AccentMuted);
    }

    private static Button AddKeypadButton(GridContainer grid, string text, System.Action action, Color? color = null)
    {
        var button = FlatUi.Button(text, color);
        button.CustomMinimumSize = new Vector2(104, 54);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.AddThemeFontSizeOverride("font_size", 20);
        button.Pressed += action;
        grid.AddChild(button);
        return button;
    }

    private void ShowAddressKeypad(LineEdit target, string targetName)
    {
        _keypadTarget = target;
        if (_keypadTargetLabel != null)
        {
            _keypadTargetLabel.Text = $"输入{targetName}";
        }
        var addressTarget = ReferenceEquals(target, _joinAddressInput);
        if (_keypadDotButton != null)
        {
            _keypadDotButton.Disabled = !addressTarget;
        }
        if (_keypadColonButton != null)
        {
            _keypadColonButton.Disabled = !addressTarget;
        }
        if (_addressKeypadPanel != null)
        {
            _addressKeypadPanel.Visible = true;
            _addressKeypadPanel.MoveToFront();
        }
    }

    private void HideAddressKeypad()
    {
        if (_addressKeypadPanel != null)
        {
            _addressKeypadPanel.Visible = false;
        }
        _keypadTarget?.ReleaseFocus();
    }

    private void AppendKeypadText(string value)
    {
        var target = _keypadTarget ?? _joinAddressInput;
        if (target == null)
        {
            return;
        }

        if (ReferenceEquals(target, _joinPortInput) && !char.IsAsciiDigit(value[0]))
        {
            return;
        }

        if (ReferenceEquals(target, _joinAddressInput))
        {
            if (value == "." && (target.Text.Count(character => character == '.') >= 3 || target.Text.Contains(':')))
            {
                return;
            }
            if (value == ":" && target.Text.Contains(':'))
            {
                return;
            }
        }

        var caret = Mathf.Clamp(target.CaretColumn, 0, target.Text.Length);
        target.Text = target.Text.Insert(caret, value);
        target.CaretColumn = caret + value.Length;
    }

    private void BackspaceKeypad()
    {
        var target = _keypadTarget ?? _joinAddressInput;
        if (target == null || target.Text.Length == 0)
        {
            return;
        }

        var caret = Mathf.Clamp(target.CaretColumn, 0, target.Text.Length);
        if (caret <= 0)
        {
            return;
        }
        target.Text = target.Text.Remove(caret - 1, 1);
        target.CaretColumn = caret - 1;
    }

    private void ClearKeypadTarget()
    {
        var target = _keypadTarget ?? _joinAddressInput;
        if (target != null)
        {
            target.Text = "";
            target.CaretColumn = 0;
        }
    }

    private void OnCreateRoomPressed()
    {
        NetworkManager.Instance?.CreateRoom(Constants.DefaultPort, GetSelectedSeatCount());
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
    }

    private void ShowJoinPanel()
    {
        if (_joinOverlay != null)
        {
            _joinOverlay.Visible = true;
            _joinOverlay.MoveToFront();
        }

        if (_joinStatusLabel != null)
        {
            _joinStatusLabel.Text = "";
        }

        StartLanDiscovery();
    }

    private void HideJoinPanel()
    {
        NetworkManager.Instance?.StopLanRoomScan();
        HideAddressKeypad();
        if (_joinOverlay != null)
        {
            _joinOverlay.Visible = false;
        }
    }

    private void ConnectToAddress()
    {
        var address = "";
        var port = Constants.DefaultPort;
        if (!TryParseJoinTarget(out address, out port, out var error))
        {
            SetJoinStatus(error, true);
            return;
        }

        if (string.IsNullOrEmpty(address))
        {
            _joinAddressInput?.GrabFocus();
            return;
        }

        SetJoinStatus($"正在连接 {address}:{port} ...", false);
        NetworkManager.Instance?.JoinRoom(address, port);
    }

    private bool TryParseJoinTarget(out string address, out int port, out string error)
    {
        address = "";
        port = Constants.DefaultPort;
        error = "";

        var invite = _joinInviteInput?.Text.Trim() ?? "";
        if (!string.IsNullOrEmpty(invite))
        {
            var parsed = Json.ParseString(invite);
            if (parsed.VariantType == Variant.Type.Dictionary)
            {
                var dict = parsed.AsGodotDictionary();
                address = dict.GetValueOrDefault("ip", "").AsString().Trim();
                port = dict.GetValueOrDefault("port", Constants.DefaultPort).AsInt32();
                if (!string.IsNullOrEmpty(address) && port > 0)
                {
                    return true;
                }
            }
        }

        address = _joinAddressInput?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(address))
        {
            error = "请输入 Host IP 或粘贴邀请信息。";
            return false;
        }

        var match = Regex.Match(address, @"^(.+):(\d+)$");
        if (match.Success)
        {
            address = match.Groups[1].Value.Trim();
            port = int.Parse(match.Groups[2].Value);
            return true;
        }

        if (!int.TryParse(_joinPortInput?.Text.Trim(), out port))
        {
            port = Constants.DefaultPort;
        }

        return true;
    }

    private void OnJoinSucceeded()
    {
        NetworkManager.Instance?.StopLanRoomScan();
        SetJoinStatus("连接成功，正在进入房间...", false);
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
    }

    private void OnJoinFailed(string reason)
    {
        SetJoinStatus(
            $"连接失败：{reason}。请确认双方位于可互访的同一局域网，Host 的 UDP {Constants.DefaultPort} 未被防火墙拦截；模拟器创建的房间不能直接被手机访问。",
            true);
    }

    private void SetJoinStatus(string text, bool danger)
    {
        if (_joinStatusLabel == null)
        {
            return;
        }

        _joinStatusLabel.Text = text;
        _joinStatusLabel.AddThemeColorOverride("font_color", danger ? FlatUi.Danger : FlatUi.MutedText);
    }

    private void StartLanDiscovery()
    {
        ClearDiscoveredRooms();
        if (_discoveryStatusLabel != null)
        {
            _discoveryStatusLabel.Text = "正在自动搜索局域网房间...";
            _discoveryStatusLabel.AddThemeColorOverride("font_color", FlatUi.Accent);
        }
        NetworkManager.Instance?.DiscoverLanRooms();
    }

    private void ClearDiscoveredRooms()
    {
        _discoveredRoomButtons.Clear();
        if (_discoveredRoomsList == null)
        {
            return;
        }

        foreach (var child in _discoveredRoomsList.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void OnLanRoomDiscovered(string address, int port, string roomCode, int playerCount, int maxPlayers)
    {
        if (_discoveredRoomsList == null)
        {
            return;
        }

        var key = $"{address}:{port}";
        if (_discoveredRoomButtons.ContainsKey(key))
        {
            return;
        }

        var isFull = playerCount >= maxPlayers;
        var button = FlatUi.Button($"房间 {roomCode}    {playerCount}/{maxPlayers}    {address}:{port}", isFull ? FlatUi.SurfaceAlt : FlatUi.AccentMuted);
        button.CustomMinimumSize = new Vector2(0, 44);
        button.Disabled = isFull;
        button.TooltipText = isFull ? "房间已满" : "点击直接加入";
        button.Pressed += () => ConnectToDiscoveredRoom(address, port);
        _discoveredRoomButtons[key] = button;
        _discoveredRoomsList.AddChild(button);
        if (_discoveryStatusLabel != null)
        {
            _discoveryStatusLabel.Text = $"已发现 {_discoveredRoomButtons.Count} 个房间";
            _discoveryStatusLabel.AddThemeColorOverride("font_color", FlatUi.Accent);
        }
    }

    private void OnLanDiscoveryFinished()
    {
        if (_discoveryStatusLabel == null)
        {
            return;
        }

        _discoveryStatusLabel.Text = _discoveredRoomButtons.Count > 0
            ? $"已发现 {_discoveredRoomButtons.Count} 个房间"
            : "未发现房间。请检查访客 Wi-Fi/AP 隔离；模拟器 Host 无法被手机直接发现。";
        _discoveryStatusLabel.AddThemeColorOverride("font_color", FlatUi.MutedText);
    }

    private void ConnectToDiscoveredRoom(string address, int port)
    {
        if (_joinAddressInput != null)
        {
            _joinAddressInput.Text = address;
        }
        if (_joinPortInput != null)
        {
            _joinPortInput.Text = port.ToString();
        }
        if (_joinInviteInput != null)
        {
            _joinInviteInput.Text = "";
        }

        SetJoinStatus($"正在连接 {address}:{port} ...", false);
        NetworkManager.Instance?.JoinRoom(address, port);
    }

    private void OnTestModePressed()
    {
        NetworkManager.Instance?.CreateRoom(Constants.DefaultPort, GetSelectedSeatCount());
        var room = NetworkManager.Instance?.RoomCode ?? "";
        var ip = NetworkManager.Instance?.GetLocalIP() ?? "";
        var ok = Regex.IsMatch(room, "^\\d{6}$") && !string.IsNullOrWhiteSpace(ip);
        GD.Print(ok ? "Connection successful" : "Connection test failed");
        GD.Print($"RoomCode={room}, LocalIP={ip}");
    }

    private void OnUpdateStatusChanged(string status, bool danger)
    {
        if (_updateStatusLabel == null)
        {
            return;
        }

        _updateStatusLabel.Text = status;
        _updateStatusLabel.AddThemeColorOverride("font_color", danger ? FlatUi.Danger : FlatUi.MutedText);
    }

    private void OnAppUpdateAvailable(string version, string apkUrl, bool required)
    {
        _availableApkUrl = apkUrl;
        if (_downloadUpdateButton != null)
        {
            _downloadUpdateButton.Text = required ? $"必须更新至 {version}" : $"下载 {version}";
            _downloadUpdateButton.Visible = !string.IsNullOrWhiteSpace(apkUrl);
        }
    }

    private void OpenAvailableUpdate()
    {
        if (!string.IsNullOrWhiteSpace(_availableApkUrl))
        {
            OS.ShellOpen(_availableApkUrl);
        }
    }

    private int GetSelectedSeatCount()
    {
        if (_seatCountOption == null)
        {
            return 9;
        }

        var selectedId = _seatCountOption.GetSelectedId();
        return selectedId == 12 ? 12 : 9;
    }

}
