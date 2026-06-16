using Godot;
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

    public override void _Ready()
    {
        BuildUi();
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.JoinSucceeded += OnJoinSucceeded;
            NetworkManager.Instance.JoinFailed += OnJoinFailed;
        }
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Name = "Background", Color = FlatUi.Background };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { Name = "MainCenter" };
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
        menu.AddChild(FlatUi.MutedLabel("创建房间、加入局域网对局，或直接用 AI 进行本地测试。"));

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

        var testButton = AddMenuButton(menu, "测试模式", OnTestModePressed);
        testButton.Visible = ShowTestModeButton;

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
        joinCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        joinCenter.OffsetLeft = 24;
        joinCenter.OffsetTop = 24;
        joinCenter.OffsetRight = -24;
        joinCenter.OffsetBottom = -24;
        _joinOverlay.AddChild(joinCenter);

        _joinPanel = FlatUi.Panel("JoinPanel");
        _joinPanel.CustomMinimumSize = new Vector2(460, 560);
        joinCenter.AddChild(_joinPanel);

        BuildJoinPanel();
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

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 24;
        box.OffsetTop = 24;
        box.OffsetRight = -24;
        box.OffsetBottom = -24;
        box.AddThemeConstantOverride("separation", 12);
        _joinPanel.AddChild(box);

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

        _joinInviteInput = new TextEdit
        {
            PlaceholderText = "粘贴邀请信息，例如 {\"ip\":\"192.168.1.100\",\"port\":7000,...}",
            CustomMinimumSize = new Vector2(0, 96),
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        box.AddChild(_joinInviteInput);

        _joinAddressInput = new LineEdit
        {
            PlaceholderText = "IP 或 IP:端口，例如 192.168.1.100:7000",
            Text = "127.0.0.1",
            CustomMinimumSize = new Vector2(280, 44)
        };
        box.AddChild(_joinAddressInput);

        _joinPortInput = new LineEdit
        {
            PlaceholderText = "端口",
            Text = Constants.DefaultPort.ToString(),
            CustomMinimumSize = new Vector2(280, 44)
        };
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

        _joinAddressInput?.GrabFocus();
    }

    private void HideJoinPanel()
    {
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
        SetJoinStatus("连接成功，正在进入房间...", false);
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
    }

    private void OnJoinFailed(string reason)
    {
        SetJoinStatus($"连接失败：{reason}", true);
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

    private void OnTestModePressed()
    {
        NetworkManager.Instance?.CreateRoom(Constants.DefaultPort, GetSelectedSeatCount());
        var room = NetworkManager.Instance?.RoomCode ?? "";
        var ip = NetworkManager.Instance?.GetLocalIP() ?? "";
        var ok = Regex.IsMatch(room, "^\\d{6}$") && !string.IsNullOrWhiteSpace(ip);
        GD.Print(ok ? "Connection successful" : "Connection test failed");
        GD.Print($"RoomCode={room}, LocalIP={ip}");
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
