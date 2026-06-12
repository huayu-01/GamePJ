using Godot;

public partial class SettingsMenu : Control
{
    private HSlider? _master;
    private HSlider? _sfx;
    private HSlider? _bgm;
    private CheckButton? _animations;
    private LineEdit? _nameInput;

    public override void _Ready()
    {
        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { CustomMinimumSize = new Vector2(420, 420) };
        center.AddChild(panel);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 24;
        root.OffsetTop = 24;
        root.OffsetRight = -24;
        root.OffsetBottom = -24;
        root.AddThemeConstantOverride("separation", 12);
        panel.AddChild(root);

        var title = new Label { Text = "设置", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        root.AddChild(title);

        _master = AddSlider(root, "主音量");
        _sfx = AddSlider(root, "音效");
        _bgm = AddSlider(root, "音乐");
        _animations = new CheckButton { Text = "启用动画" };
        root.AddChild(_animations);
        _nameInput = new LineEdit { PlaceholderText = "玩家名称" };
        root.AddChild(_nameInput);

        var save = new Button { Text = "保存" };
        save.Pressed += SaveValues;
        root.AddChild(save);

        var back = new Button { Text = "返回" };
        back.Pressed += () => GetTree().ChangeSceneToFile(Constants.MainMenuScene);
        root.AddChild(back);
    }

    private HSlider AddSlider(VBoxContainer root, string label)
    {
        root.AddChild(new Label { Text = label });
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1 };
        root.AddChild(slider);
        return slider;
    }

    private void LoadValues()
    {
        var settings = SettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        _master!.Value = settings.MasterVolume * 100;
        _sfx!.Value = settings.SFXVolume * 100;
        _bgm!.Value = settings.BGMVolume * 100;
        _animations!.ButtonPressed = settings.AnimationsEnabled;
        _nameInput!.Text = settings.PlayerName;
    }

    private void SaveValues()
    {
        var settings = SettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        settings.MasterVolume = (float)(_master!.Value / 100.0);
        settings.SFXVolume = (float)(_sfx!.Value / 100.0);
        settings.BGMVolume = (float)(_bgm!.Value / 100.0);
        settings.AnimationsEnabled = _animations!.ButtonPressed;
        settings.PlayerName = _nameInput!.Text;
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.PlayerName = settings.PlayerName;
        }
        settings.SaveSettings();
    }
}
