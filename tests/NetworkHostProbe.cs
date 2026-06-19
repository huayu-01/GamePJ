using Godot;

public partial class NetworkHostProbe : Node
{
    public override async void _Ready()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        NetworkManager.Instance?.CreateRoom(Constants.DefaultPort, 9);
        GD.Print($"NETWORK_HOST_PROBE_READY {NetworkManager.Instance?.GetLocalIP()}:{Constants.DefaultPort}");
        await ToSignal(GetTree().CreateTimer(90), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit();
    }
}
