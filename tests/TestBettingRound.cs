using Godot;
using System.Collections.Generic;

public partial class TestBettingRound : Node
{
    public override void _Ready()
    {
        var (pass, fail) = Run();
        GD.Print("\n========== BETTING TEST RESULT ==========");
        GD.Print($"PASS: {pass}/14, FAIL: {fail}/14");
    }

    public static (int Pass, int Fail) Run()
    {
        int pass = 0, fail = 0;

        var round1 = new BettingRound { MinRaise = 20 };
        round1.PlayersToAct = new List<int> { 1, 2 };
        round1.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        Check(round1.ProcessAction(1, PlayerAction.Check, 0), "ValidCheck", ref pass, ref fail);

        var round2 = new BettingRound { MinRaise = 20, CurrentBet = 50 };
        round2.PlayersToAct = new List<int> { 1, 2 };
        round2.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 50 };
        Check(!round2.ProcessAction(1, PlayerAction.Check, 0), "InvalidCheck", ref pass, ref fail);

        var round3 = new BettingRound { MinRaise = 20, CurrentBet = 50 };
        round3.PlayersToAct = new List<int> { 1, 2 };
        round3.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 50 };
        Check(round3.ProcessAction(1, PlayerAction.Call, 50), "ValidCall", ref pass, ref fail);

        var round4 = new BettingRound { MinRaise = 20, CurrentBet = 50, LastRaiseAmount = 20 };
        round4.PlayersToAct = new List<int> { 1, 2 };
        round4.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 50 };
        Check(round4.ProcessAction(1, PlayerAction.Raise, 80), "ValidRaise", ref pass, ref fail);

        var round5 = new BettingRound { MinRaise = 20, CurrentBet = 50, LastRaiseAmount = 20 };
        round5.PlayersToAct = new List<int> { 1, 2 };
        round5.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 50 };
        Check(!round5.ProcessAction(1, PlayerAction.Raise, 60), "InvalidRaise", ref pass, ref fail);

        var round6 = new BettingRound { MinRaise = 20 };
        round6.PlayersToAct = new List<int> { 1, 2 };
        round6.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        round6.ProcessAction(2, PlayerAction.Fold, 0);
        Check(round6.IsRoundComplete(), "RoundCompleteOnePlayer", ref pass, ref fail);

        var round7 = new BettingRound { CurrentBet = 20, MinRaise = 20 };
        round7.PlayersToAct = new List<int> { 6, 7, 1, 2 };
        round7.SeatOrder = new List<int> { 6, 7, 1, 2 };
        round7.PlayerBets = new Dictionary<int, int> { [6] = 0, [7] = 0, [1] = 0, [2] = 0 };
        round7.PlayerChips = new Dictionary<int, int> { [6] = 100, [7] = 100, [1] = 100, [2] = 100 };
        round7.ProcessAction(6, PlayerAction.Call, 20);
        Check(round7.GetCurrentPlayerId() == 7, "ActionAdvancesToNextSeat", ref pass, ref fail);

        var round8 = new BettingRound { CurrentBet = 20, MinRaise = 20, LastRaiseAmount = 20 };
        round8.PlayersToAct = new List<int> { 6, 7, 1, 2, 4, 5 };
        round8.SeatOrder = new List<int> { 4, 5, 6, 7, 1, 2 };
        round8.PlayerBets = new Dictionary<int, int> { [4] = 10, [5] = 20, [6] = 20, [7] = 0, [1] = 0, [2] = 0 };
        round8.PlayerChips = new Dictionary<int, int> { [4] = 100, [5] = 100, [6] = 100, [7] = 100, [1] = 100, [2] = 100 };
        round8.ProcessAction(6, PlayerAction.Raise, 40);
        Check(round8.GetCurrentPlayerId() == 7, "RaiseRestartsFromNextSeat", ref pass, ref fail);

        var round9 = new BettingRound { MinRaise = 20, LastRaiseAmount = 20, WagerUnit = 10 };
        round9.PlayersToAct = new List<int> { 1, 2, 3 };
        round9.SeatOrder = new List<int> { 1, 2, 3 };
        round9.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0 };
        round9.PlayerChips = new Dictionary<int, int> { [1] = 200, [2] = 200, [3] = 200 };
        Check(round9.ProcessAction(1, PlayerAction.Bet, 40), "OpeningBetSetsRaiseStep", ref pass, ref fail);
        Check(!round9.ProcessAction(2, PlayerAction.Raise, 60) && round9.ProcessAction(2, PlayerAction.Raise, 80), "RaiseMustMatchPreviousRaiseStep", ref pass, ref fail);

        var round10 = new BettingRound { MinRaise = 20, CurrentBet = 40, LastRaiseAmount = 40, WagerUnit = 10 };
        round10.PlayersToAct = new List<int> { 2, 3 };
        round10.PlayerBets = new Dictionary<int, int> { [2] = 0, [3] = 40 };
        round10.PlayerChips = new Dictionary<int, int> { [2] = 200, [3] = 160 };
        Check(!round10.ProcessAction(2, PlayerAction.Raise, 85) && round10.ProcessAction(2, PlayerAction.Raise, 80), "RaiseUsesSmallBlindStep", ref pass, ref fail);

        var round11 = new BettingRound { MinRaise = 20, LastRaiseAmount = 100, WagerUnit = 10 };
        round11.PlayersToAct = new List<int> { 1, 2, 3, 4 };
        round11.SeatOrder = new List<int> { 1, 2, 3, 4 };
        round11.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0 };
        round11.PlayerChips = new Dictionary<int, int> { [1] = 1000, [2] = 150, [3] = 1000, [4] = 1000 };
        round11.ProcessAction(1, PlayerAction.Bet, 100);
        round11.ProcessAction(2, PlayerAction.AllIn, 150);
        round11.ProcessAction(3, PlayerAction.Call, 150);
        round11.ProcessAction(4, PlayerAction.Call, 150);
        Check(
            round11.GetCurrentPlayerId() == 1 &&
            round11.IsValidAction(1, PlayerAction.Call, 50) &&
            !round11.IsValidAction(1, PlayerAction.Raise, 250),
            "ShortAllInAllowsCallButDoesNotReopenRaise",
            ref pass,
            ref fail);

        var round12 = new BettingRound { MinRaise = 20, LastRaiseAmount = 100, WagerUnit = 10 };
        round12.PlayersToAct = new List<int> { 1, 2, 3, 4 };
        round12.SeatOrder = new List<int> { 1, 2, 3, 4 };
        round12.PlayerBets = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0 };
        round12.PlayerChips = new Dictionary<int, int> { [1] = 1000, [2] = 150, [3] = 210, [4] = 1000 };
        round12.ProcessAction(1, PlayerAction.Bet, 100);
        round12.ProcessAction(2, PlayerAction.AllIn, 150);
        round12.ProcessAction(3, PlayerAction.AllIn, 210);
        round12.ProcessAction(4, PlayerAction.Call, 210);
        Check(
            round12.GetCurrentPlayerId() == 1 &&
            !round12.IsValidAction(1, PlayerAction.Raise, 300) &&
            round12.IsValidAction(1, PlayerAction.Raise, 310),
            "CumulativeShortAllInsReopenRaiseAtFullIncrement",
            ref pass,
            ref fail);

        var round13 = new BettingRound { CurrentBet = 20, MinRaise = 20 };
        round13.PlayersToAct = new List<int> { 1, 2, 3 };
        round13.SeatOrder = new List<int> { 1, 2, 3 };
        round13.PlayerBets = new Dictionary<int, int> { [1] = 20, [2] = 0, [3] = 20 };
        round13.PlayerChips = new Dictionary<int, int> { [1] = 100, [2] = 100, [3] = 100 };
        Check(
            round13.ForceFold(2) &&
            round13.FoldedPlayers.Contains(2) &&
            !round13.PlayersToAct.Contains(2) &&
            round13.GetCurrentPlayerId() == 1,
            "DisconnectedPlayerForceFoldsWithoutBlockingTurn",
            ref pass,
            ref fail);

        return (pass, fail);
    }

    private static void Check(bool condition, string name, ref int pass, ref int fail)
    {
        if (condition)
        {
            pass++;
            GD.Print($"PASS: {name}");
        }
        else
        {
            fail++;
            GD.Print($"FAIL: {name}");
        }
    }
}
