using Godot;
using System.Text.RegularExpressions;

public partial class MainMenu : Control
{
    [Export] public bool ShowTestModeButton { get; set; }

    private LineEdit? _joinAddressInput;
    private Panel? _joinPanel;
    private OptionButton? _seatCountOption;
    private SpinBox? _smallBlindSpin;
    private SpinBox? _minBuyInSpin;
    private SpinBox? _maxBuyInSpin;
    private SpinBox? _tableCapSpin;

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

        var root = new HBoxContainer { Name = "MainLayout" };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 56;
        root.OffsetTop = 56;
        root.OffsetRight = -56;
        root.OffsetBottom = -56;
        root.AddThemeConstantOverride("separation", 24);
        AddChild(root);

        var menuPanel = FlatUi.Panel("MenuPanel");
        menuPanel.CustomMinimumSize = new Vector2(420, 520);
        menuPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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

        menu.AddChild(BuildSpinRow("低注/小盲", out _smallBlindSpin, 1, 10000, GameManager.Instance?.SmallBlindAmount ?? Constants.SmallBlind, 1));
        menu.AddChild(BuildSpinRow("最低带入", out _minBuyInSpin, 1, 100000, GameManager.Instance?.MinBuyIn ?? Constants.MinBuyIn, 1));
        menu.AddChild(BuildSpinRow("最高带入", out _maxBuyInSpin, 1, 100000, GameManager.Instance?.MaxBuyIn ?? Constants.MaxBuyIn, 1));
        menu.AddChild(BuildSpinRow("后手上限", out _tableCapSpin, 1, 200000, GameManager.Instance?.TableChipLimit ?? Constants.TableChipLimit, 1));

        AddMenuButton(menu, "创建房间", OnCreateRoomPressed, FlatUi.AccentMuted);
        AddMenuButton(menu, "加入房间", ToggleJoinPanel);
        AddMenuButton(menu, "设置", () => GetTree().ChangeSceneToFile(Constants.SettingsScene));

        var testButton = AddMenuButton(menu, "测试模式", OnTestModePressed);
        testButton.Visible = ShowTestModeButton;

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        menu.AddChild(spacer);
        AddMenuButton(menu, "退出游戏", () => GetTree().Quit(), FlatUi.Danger);

        _joinPanel = FlatUi.Panel("JoinPanel");
        _joinPanel.CustomMinimumSize = new Vector2(380, 520);
        _joinPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(_joinPanel);

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

    private HBoxContainer BuildSpinRow(string label, out SpinBox spin, int min, int max, int value, int step)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var text = FlatUi.MutedLabel(label);
        text.CustomMinimumSize = new Vector2(108, 34);
        row.AddChild(text);
        spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            Step = step,
            CustomMinimumSize = new Vector2(140, 36)
        };
        row.AddChild(spin);
        return row;
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

        box.AddChild(FlatUi.Label("加入房间", 28));
        box.AddChild(FlatUi.MutedLabel("输入 Host 的局域网 IP。这里不是弹窗，不会遮挡主菜单。"));

        _joinAddressInput = new LineEdit
        {
            PlaceholderText = "例如 192.168.1.100",
            Text = "127.0.0.1",
            CustomMinimumSize = new Vector2(280, 40)
        };
        box.AddChild(_joinAddressInput);

        var connect = FlatUi.Button("连接", FlatUi.AccentMuted);
        connect.Pressed += ConnectToAddress;
        box.AddChild(connect);

        var cancel = FlatUi.Button("清空输入");
        cancel.Pressed += () =>
        {
            if (_joinAddressInput != null)
            {
                _joinAddressInput.Text = "";
                _joinAddressInput.GrabFocus();
            }
        };
        box.AddChild(cancel);

        var hint = FlatUi.MutedLabel("如果只是想测试，可以进入牌桌后在工具面板加入 AI。");
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(hint);
    }

    private void OnCreateRoomPressed()
    {
        ApplyRoomRules();
        NetworkManager.Instance?.CreateRoom(Constants.DefaultPort, GetSelectedSeatCount());
        GetTree().ChangeSceneToFile(Constants.LobbyScene);
    }

    private void ToggleJoinPanel()
    {
        _joinAddressInput?.GrabFocus();
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
        ApplyRoomRules();
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

    private void ApplyRoomRules()
    {
        var smallBlind = (int)(_smallBlindSpin?.Value ?? Constants.SmallBlind);
        var minBuyIn = (int)(_minBuyInSpin?.Value ?? Constants.MinBuyIn);
        var maxBuyIn = (int)(_maxBuyInSpin?.Value ?? Constants.MaxBuyIn);
        var tableCap = (int)(_tableCapSpin?.Value ?? Constants.TableChipLimit);
        GameManager.Instance?.ConfigureRoomRules(smallBlind, minBuyIn, maxBuyIn, tableCap);
    }
}
