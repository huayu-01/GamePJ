using Godot;
using System.Linq;

public partial class VisualNineSeatTableProbe : GameTable
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
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

        var manager = GameManager.Instance;
        if (manager != null)
        {
            var seats = string.Join(", ", manager.Players.OrderBy(player => player.Position).Select(player => $"S{player.Position + 1}:P{player.Id}"));
            GD.Print($"VISUAL_NINE_PROBE count={manager.Players.Count} seatCount={manager.TableSeatCount} seats={seats}");
        }

        var image = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://test_outputs"));
        var path = "res://test_outputs/nine_seat_probe.png";
        image.SavePng(path);
        GD.Print($"VISUAL_NINE_PROBE screenshot={ProjectSettings.GlobalizePath(path)}");
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
        manager.TableSeatCount = 9;
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();

        for (var seat = 0; seat < 6; seat++)
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
                manager.AiPlayerIds.Remove(id);
            }
        }

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = 1;
        }

        manager.StartGame();
        ForceLocalTurn(manager);
        probeConfigured = true;
    }

    private static void ForceLocalTurn(GameManager manager)
    {
        var guard = 0;
        while (manager.CurrentBettingRound != null && manager.CurrentBettingRound.GetCurrentPlayerId() != 1 && guard++ < 12)
        {
            var playerId = manager.CurrentBettingRound.GetCurrentPlayerId();
            var currentBet = manager.CurrentBettingRound.CurrentBet;
            var playerBet = manager.CurrentBettingRound.PlayerBets.GetValueOrDefault(playerId, 0);
            var callAmount = Mathf.Max(0, currentBet - playerBet);
            var action = callAmount > 0 ? PlayerAction.Call : PlayerAction.Check;
            manager.ProcessPlayerAction(playerId, action, callAmount);
        }
    }
}
