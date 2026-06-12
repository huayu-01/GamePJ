using Godot;

public partial class SettingsManager : Node
{
    public static SettingsManager? Instance { get; private set; }

    [Export] public float MasterVolume { get; set; } = 0.8f;
    [Export] public float SFXVolume { get; set; } = 0.8f;
    [Export] public float BGMVolume { get; set; } = 0.6f;
    [Export] public bool AnimationsEnabled { get; set; } = true;
    [Export] public string PlayerName { get; set; } = "玩家";

    private const string SettingsPath = "user://settings.json";

    public override void _Ready()
    {
        Instance = this;
        LoadSettings();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SaveSettings()
    {
        var settings = new Godot.Collections.Dictionary
        {
            ["master_volume"] = MasterVolume,
            ["sfx_volume"] = SFXVolume,
            ["bgm_volume"] = BGMVolume,
            ["animations_enabled"] = AnimationsEnabled,
            ["player_name"] = PlayerName
        };

        using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(Json.Stringify(settings, "\t"));
    }

    public void LoadSettings()
    {
        if (!Godot.FileAccess.FileExists(SettingsPath))
        {
            return;
        }

        using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Read);
        var text = file?.GetAsText() ?? string.Empty;
        var parsed = Json.ParseString(text);
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var dict = parsed.AsGodotDictionary();
        MasterVolume = (float)dict.GetValueOrDefault("master_volume", MasterVolume).AsDouble();
        SFXVolume = (float)dict.GetValueOrDefault("sfx_volume", SFXVolume).AsDouble();
        BGMVolume = (float)dict.GetValueOrDefault("bgm_volume", BGMVolume).AsDouble();
        AnimationsEnabled = dict.GetValueOrDefault("animations_enabled", AnimationsEnabled).AsBool();
        PlayerName = dict.GetValueOrDefault("player_name", PlayerName).AsString();
    }
}
