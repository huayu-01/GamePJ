using Godot;

public partial class NetworkPlayer : Node
{
    [Export] public int PlayerId { get; set; }
    [Export] public string PlayerName { get; set; } = "Player";

    public void SubmitAction(PlayerAction action, int amount)
    {
        NetworkManager.Instance?.SubmitLocalAction(PlayerId, action, amount);
    }
}
