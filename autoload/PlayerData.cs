using Godot;

public partial class PlayerData : Node
{
    public static PlayerData? Instance { get; private set; }

    [Export] public string PlayerName { get; set; } = "玩家";
    [Export] public int LocalPlayerId { get; set; } = 1;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
