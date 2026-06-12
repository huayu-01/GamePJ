using Godot;
using System.Linq;

public partial class SeatOrderProbe : Node
{
    public override void _Ready()
    {
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

        for (var hand = 1; hand <= 4; hand++)
        {
            manager.StartGame();
            LogHandOrder(manager, hand);
            LogFirstActionAdvance(manager, hand);
            FoldUntilHandEnds(manager);
        }

        GetTree().Quit();
    }

    private static void LogHandOrder(GameManager manager, int hand)
    {
        var orderedPlayers = manager.CurrentBettingRound?.SeatOrder
            .Select(id => manager.Players.First(player => player.Id == id))
            .Select(player => $"S{player.Position + 1}:P{player.Id}")
            .ToArray() ?? System.Array.Empty<string>();

        var smallBlind = manager.Players.FirstOrDefault(player => manager.GetBlindRole(player.Id) == "小盲");
        var bigBlind = manager.Players.FirstOrDefault(player => manager.GetBlindRole(player.Id) == "大盲");
        var orderText = string.Join(" -> ", orderedPlayers);
        GD.Print($"SEAT_PROBE hand={hand} dealerSeat={manager.DealerPosition + 1} smallBlind={FormatPlayer(smallBlind)} bigBlind={FormatPlayer(bigBlind)} preflopOrder={orderText}");
    }

    private static void LogFirstActionAdvance(GameManager manager, int hand)
    {
        var before = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        var beforePlayer = manager.Players.FirstOrDefault(player => player.Id == before);
        if (beforePlayer == null || manager.CurrentBettingRound == null)
        {
            return;
        }

        var currentBet = manager.CurrentBettingRound.CurrentBet;
        var playerBet = manager.CurrentBettingRound.PlayerBets.GetValueOrDefault(before, beforePlayer.CurrentBet);
        var callAmount = System.Math.Max(0, currentBet - playerBet);
        var action = manager.CurrentBettingRound.IsValidAction(before, PlayerAction.Check, 0) ? PlayerAction.Check : PlayerAction.Call;
        manager.ProcessPlayerAction(before, action, callAmount);

        var after = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        var afterPlayer = manager.Players.FirstOrDefault(player => player.Id == after);
        GD.Print($"SEAT_PROBE_ADVANCE hand={hand} acted={FormatPlayer(beforePlayer)} action={action} next={FormatPlayer(afterPlayer)}");
    }

    private static string FormatPlayer(Player? player)
    {
        return player == null ? "-" : $"S{player.Position + 1}:P{player.Id}";
    }

    private static void FoldUntilHandEnds(GameManager manager)
    {
        var guard = 0;
        while (manager.CurrentState != GameState.Lobby && manager.CurrentState != GameState.GameOver && guard++ < 80)
        {
            var currentId = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
            if (currentId < 0)
            {
                manager.EndBettingRound();
                continue;
            }

            manager.ProcessPlayerAction(currentId, PlayerAction.Fold, 0);
        }
    }
}
