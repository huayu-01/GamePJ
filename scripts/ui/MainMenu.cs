using Godot;
using System.Text.RegularExpressions;

public partial class MainMenu : Control
{
    [Export] public bool ShowTestModeButton { get; set; }

    private LineEdit? _joinAddressInput;
    private ColorRect? _joinOverlay;
    private Panel? _joinPanel;
    private OptionButton? _seatCountOption;

    public override void _Ready()
    {
        BuildUi();
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
        _joinPanel.CustomMinimumSize = new Vector2(420, 420);
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

        var hint = FlatUi.MutedLabel("输入 Host 的局域网 IP，连接后进入房间。");
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(hint);

        _joinAddressInput = new LineEdit
        {
            PlaceholderText = "例如 192.168.1.100",
            Text = "127.0.0.1",
            CustomMinimumSize = new Vector2(280, 44)
        };
        box.AddChild(_joinAddressInput);

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
                _joinAddressInput.GrabFocus();
            }
        };
        box.AddChild(clear);

        var footer = FlatUi.MutedLabel("如果只是想测试，可以创建房间后在牌桌内追加 AI。");
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
        var address = _joinAddressInput?.Text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            _joinAddressInput?.GrabFocus();
            return;
        }

        NetworkManager.Instance?.JoinRoom(address);
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
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
