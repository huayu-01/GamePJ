using Godot;
using System.Linq;

public partial class VisualTwelveSeatTableProbe : GameTable
{
    private bool probeConfigured;

    public override void _EnterTree()
    {
        ConfigureProbeGame();
    }

    public override async void _Ready()
    {
        ConfigureProbeGame();
        base._Ready();
        LogProbeState("ready");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        LogProbeState("screenshot");

        var image = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://test_outputs"));
        var path = "res://test_outputs/twelve_seat_probe.png";
        image.SavePng(path);
        GD.Print($"VISUAL_PROBE screenshot={ProjectSettings.GlobalizePath(path)}");
        GetTree().Quit();
    }

    private void ConfigureProbeGame()
    {
        if (probeConfigured)
        {
            return;
        }

        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.AutoContinueHands = false;
        manager.TableSeatCount = Constants.MaxPlayers;
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();

        for (var seat = 0; seat < Constants.MaxPlayers; seat++)
        {
            var id = seat + 1;
            manager.Players.Add(new Player
            {
                Id = id,
                Name = seat == 0 ? "玩家" : $"AI_{id}",
                Chips = Constants.StartingChips,
                Position = seat
            });

            if (seat > 0)
            {
                manager.AiPlayerIds.Add(id);
            }
        }

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = 1;
        }

        manager.StartGame();
        probeConfigured = true;
    }

    private static void LogProbeState(string phase)
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        var seats = string.Join(", ", manager.Players
            .OrderBy(player => player.Position)
            .Select(player => $"S{player.Position + 1}:P{player.Id}:{player.Name}:{(player.IsSittingOut ? "out" : "in")}"));
        GD.Print($"VISUAL_PROBE_STATE {phase} count={manager.Players.Count} seatCount={manager.TableSeatCount} state={manager.CurrentState} seats={seats}");
    }
}
