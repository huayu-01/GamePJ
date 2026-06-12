using Godot;

public partial class AudioManager : Node
{
    public static AudioManager? Instance { get; private set; }

    [Export] public AudioStream? DealSound { get; set; }
    [Export] public AudioStream? BetSound { get; set; }
    [Export] public AudioStream? WinSound { get; set; }
    [Export] public AudioStream? FoldSound { get; set; }
    [Export] public AudioStream? AllInSound { get; set; }

    public override void _Ready()
    {
        Instance = this;
        DealSound ??= ResourceLoader.Load<AudioStream>("res://assets/audio/sfx/cardSlide1.ogg");
        BetSound ??= ResourceLoader.Load<AudioStream>("res://assets/audio/sfx/chipsCollide1.ogg");
        WinSound ??= ResourceLoader.Load<AudioStream>("res://assets/audio/sfx/chipsCollide1.ogg");
        FoldSound ??= ResourceLoader.Load<AudioStream>("res://assets/audio/sfx/cardPlace1.ogg");
        AllInSound ??= ResourceLoader.Load<AudioStream>("res://assets/audio/sfx/chipsCollide1.ogg");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlaySFX(AudioStream? stream)
    {
        if (stream == null)
        {
            return;
        }

        var player = new AudioStreamPlayer();
        AddChild(player);
        player.Stream = stream;
        var sfxVolume = SettingsManager.Instance?.SFXVolume ?? 0.8f;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.001f, sfxVolume));
        player.Finished += player.QueueFree;
        player.Play();
    }
}
