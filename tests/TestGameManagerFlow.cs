using Godot;
using System.Linq;
using System.Reflection;

public partial class TestGameManagerFlow : Node
{
    public static (int Pass, int Fail) Run()
    {
        int pass = 0, fail = 0;
        var manager = GameManager.Instance;
        if (manager == null)
        {
            Check(false, "GameManagerAvailable", ref pass, ref fail);
            return (pass, fail);
        }

        manager.AutoContinueHands = false;
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.ConfigureRoomRules(Constants.SmallBlind, Constants.StartingChips, Constants.StartingChips, Constants.StartingChips);
        manager.TableChipLimit = Constants.StartingChips;
        manager.TableSeatCount = Constants.MaxPlayers;
        manager.EnsureDemoPlayers(2);
        manager.ConfigureAiPlayers(true, 11);
        Check(manager.Players.Count == Constants.MaxPlayers, "TwelveSeatLimit", ref pass, ref fail);

        manager.ConfigureRoomRules(1, 50, 200, 1000, 9);
        Check(
            manager.SmallBlindAmount == 1 &&
            manager.BigBlindAmount == 2 &&
            manager.MinBuyIn == 50 &&
            manager.MaxBuyIn == 200 &&
            manager.TableChipLimit == 1000 &&
            manager.ThinkingTimeSeconds == 9,
            "RoomRulesConfigurable",
            ref pass,
            ref fail);
        manager.ConfigureRoomRules(Constants.SmallBlind, Constants.StartingChips, Constants.StartingChips, Constants.StartingChips);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = Constants.MaxPlayers;
        manager.EnsureDemoPlayers(2);
        manager.AddAiPlayers(2);
        manager.AddAiPlayers(2);
        Check(manager.Players.Count == 6 && manager.AiPlayerIds.Count == 5, "AppendAiPlayers", ref pass, ref fail);

        manager.StartGame();
        manager.AddAiPlayers(1);
        var lateAi = manager.Players.First(player => player.Id == manager.Players.Max(item => item.Id));
        Check(lateAi.IsSittingOut && !lateAi.WantsSitOutNextHand, "LateAiWaitsNextHand", ref pass, ref fail);

        var sittingOut = manager.Players.First(player => player.Id == 2);
        manager.SetSittingOut(sittingOut.Id, true);
        manager.StartGame();
        Check(sittingOut.IsSittingOut && sittingOut.HoleCards.All(card => card == null), "SittingOutNotDealt", ref pass, ref fail);

        var local = manager.Players.First(player => player.Id == 1);
        local.Chips = 0;
        manager.SetSittingOut(local.Id, false);
        Check(!local.IsSittingOut && !local.WantsSitOutNextHand, "ZeroChipsDoesNotForceSitOut", ref pass, ref fail);

        manager.RebuyPlayer(local.Id, Constants.StartingChips);
        Check(local.Chips == Constants.StartingChips && !local.WantsSitOutNextHand, "RebuyClearsNextHandSitOut", ref pass, ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 2;
        manager.Players.Add(new Player { Id = 1, Name = "P1", Chips = Constants.StartingChips, Position = 0 });
        manager.Players.Add(new Player { Id = 2, Name = "P2", Chips = Constants.StartingChips, Position = 1 });
        manager.StartGame();
        var state = manager.CreateStateDTO().ToDictionary();
        var players = state["players"].AsGodotArray<Godot.Collections.Dictionary>();
        Check(
            !state.ContainsKey("deck") &&
            !state.ContainsKey("card_pool") &&
            state["community_cards"].AsGodotArray<Godot.Collections.Dictionary>().Count == 0 &&
            players.All(player => !player.ContainsKey("hole_cards") && !player.ContainsKey("cards")),
            "NetworkStateHidesPrivateCards",
            ref pass,
            ref fail);

        manager.DealCommunityCards(3);
        var flopState = manager.CreateStateDTO().ToDictionary();
        manager.DealCommunityCards(1);
        var turnState = manager.CreateStateDTO().ToDictionary();
        Check(
            flopState["community_cards"].AsGodotArray<Godot.Collections.Dictionary>().Count == 3 &&
            turnState["community_cards"].AsGodotArray<Godot.Collections.Dictionary>().Count == 4 &&
            !turnState.ContainsKey("deck") &&
            !turnState.ContainsKey("card_pool"),
            "NetworkStateRevealsOnlyDealtCommunityCards",
            ref pass,
            ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 3;
        typeof(GameManager)
            .GetProperty(nameof(GameManager.DealerPosition), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(manager, 0);
        manager.Players.Add(new Player { Id = 1, Name = "P1", Chips = Constants.StartingChips, Position = 0 });
        manager.Players.Add(new Player { Id = 2, Name = "P2", Chips = 0, Position = 1 });
        manager.Players.Add(new Player { Id = 3, Name = "P3", Chips = Constants.StartingChips, Position = 2 });
        manager.StartGame();
        var brokeSeat = manager.Players.First(player => player.Id == 2);
        Check(
            manager.GetBlindRole(1) == "大盲" &&
            manager.CurrentBettingRound?.GetCurrentPlayerId() == 3 &&
            brokeSeat.HoleCards.All(card => card == null) &&
            manager.CurrentBettingRound?.PlayerBets.ContainsKey(2) == false,
            "PreFlopSkipsBrokeSeatAfterBigBlind",
            ref pass,
            ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 3;
        for (var seat = 0; seat < 3; seat++)
        {
            manager.Players.Add(new Player { Id = seat + 1, Name = $"P{seat + 1}", Chips = Constants.StartingChips, Position = seat });
        }

        manager.StartGame();
        foreach (var player in manager.Players)
        {
            player.Chips = 0;
            player.IsAllIn = true;
            player.IsFolded = false;
        }

        manager.StartBettingRound(GameState.River);
        manager.EndBettingRound();
        Check(manager.CurrentBettingRound == null && manager.CurrentState is GameState.Lobby or GameState.GameOver, "AllInZeroChipsCompletesHand", ref pass, ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 3;
        for (var seat = 0; seat < 3; seat++)
        {
            manager.Players.Add(new Player { Id = seat + 1, Name = $"P{seat + 1}", Chips = Constants.StartingChips, Position = seat });
        }

        manager.StartGame();
        var allInPlayer = manager.Players.First(player => player.Id == 1);
        allInPlayer.Chips = 0;
        allInPlayer.IsAllIn = true;
        allInPlayer.IsFolded = false;
        manager.StartBettingRound(GameState.Flop);
        var nextToAct = manager.CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        Check(nextToAct is 2 or 3 && manager.CurrentState == GameState.Flop, "SingleAllInLetsDeepStacksContinue", ref pass, ref fail);

        allInPlayer.Chips = 0;
        manager.EndHand();
        Check(!allInPlayer.IsSittingOut && !allInPlayer.WantsSitOutNextHand, "BustedWaitsForRebuyWithoutSitOut", ref pass, ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 6;
        manager.Players.Add(new Player { Id = 1, Name = "P1", Chips = 0, Position = 0 });
        for (var seat = 1; seat < 6; seat++)
        {
            manager.Players.Add(new Player
            {
                Id = seat + 1,
                Name = $"P{seat + 1}",
                Chips = Constants.StartingChips,
                Position = seat,
                IsSittingOut = true,
                WantsSitOutNextHand = false
            });
        }

        manager.EndHand();
        Check(
            manager.CurrentState == GameState.Lobby &&
            manager.Players.Where(player => player.Id > 1).All(player => !player.IsSittingOut),
            "WaitingPlayersJoinNextHandAfterEnd",
            ref pass,
            ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 3;
        manager.ConfigureRoomRules(Constants.SmallBlind, 200, 800, 1200);
        manager.Players.Add(new Player { Id = 1, Name = "P1", Chips = Constants.StartingChips, Position = 0 });
        manager.Players.Add(new Player { Id = 2, Name = "AI_2", Chips = 0, Position = 1 });
        manager.Players.Add(new Player { Id = 3, Name = "P3", Chips = Constants.StartingChips, Position = 2 });
        manager.AiPlayerIds.Add(2);
        manager.EndHand();
        Check(manager.Players.First(player => player.Id == 2).Chips == 800, "AiAutoRebuyAfterBust", ref pass, ref fail);
        manager.ConfigureRoomRules(Constants.SmallBlind, Constants.StartingChips, Constants.StartingChips, Constants.StartingChips);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 4;
        for (var seat = 0; seat < 4; seat++)
        {
            manager.Players.Add(new Player
            {
                Id = seat + 1,
                Name = $"P{seat + 1}",
                Chips = Constants.StartingChips,
                Position = seat
            });
        }

        manager.StartGame();
        manager.Players.First(player => player.Id == 1).IsFolded = true;
        manager.Players.First(player => player.Id == 3).IsFolded = true;
        manager.CommunityCards.Clear();
        manager.CommunityCards.Add(new Card { Suit = Suit.Diamonds, Rank = Rank.Ace });
        manager.CommunityCards.Add(new Card { Suit = Suit.Clubs, Rank = Rank.Jack });
        manager.CommunityCards.Add(new Card { Suit = Suit.Clubs, Rank = Rank.Queen });
        manager.CommunityCards.Add(new Card { Suit = Suit.Clubs, Rank = Rank.King });
        manager.StartBettingRound(GameState.Turn);
        if (manager.CurrentBettingRound != null)
        {
            manager.CurrentBettingRound.PlayersToAct.Clear();
            manager.CurrentBettingRound.CurrentPlayerIndex = -1;
        }

        manager.CheckBettingRoundProgress();
        Check(manager.CurrentState != GameState.Turn || manager.CurrentBettingRound?.GetCurrentPlayerId() > 0, "NoCurrentPlayerRoundAutoProgresses", ref pass, ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 3;
        for (var seat = 0; seat < 3; seat++)
        {
            manager.Players.Add(new Player { Id = seat + 1, Name = $"P{seat + 1}", Chips = Constants.StartingChips, Position = seat });
        }

        manager.StartGame();
        manager.DealCommunityCards(3);
        manager.Players.First(player => player.Id == 2).IsFolded = true;
        manager.Players.First(player => player.Id == 3).IsFolded = true;
        manager.StartBettingRound(GameState.Flop);
        manager.WaitForSettlementAnimation = true;
        var standingHistoryStart = manager.HandHistory.Count;
        manager.EndBettingRound();
        manager.EndBettingRound();
        manager.CheckBettingRoundProgress();
        Check(
            !manager.LastHandWentToShowdown &&
            manager.LastWinners.SequenceEqual(new[] { 1 }) &&
            manager.GetVisibleHandRank(1) == null,
            "LastStandingDoesNotExposeHandRank",
            ref pass,
            ref fail);

        var standingHistory = manager.HandHistory.Skip(standingHistoryStart).Where(line => line.Contains("收")).ToList();
        Check(standingHistory.Count == 1, "ResolvedHandDoesNotDuplicateSettlementHistory", ref pass, ref fail);
        manager.WaitForSettlementAnimation = false;
        manager.CompleteSettlementAnimation();

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.TableSeatCount = 2;
        manager.Players.Add(new Player { Id = 1, Name = "P1", Chips = Constants.StartingChips, Position = 0 });
        manager.Players.Add(new Player { Id = 2, Name = "P2", Chips = Constants.StartingChips, Position = 1 });
        var historyStart = manager.HandHistory.Count;
        manager.StartGame();
        var privateCard = manager.Players.First(player => player.Id == 1).HoleCards[0]?.ShortName ?? "";
        var newHistory = manager.HandHistory.Skip(historyStart).ToList();
        Check(!string.IsNullOrEmpty(privateCard) && newHistory.All(line => !line.Contains(privateCard)), "HistoryHidesPrivateHoleCards", ref pass, ref fail);

        var revealResult = manager.RevealHoleCard(1, 0);
        Check(
            revealResult &&
            manager.IsHoleCardRevealed(1, 0) &&
            manager.HandHistory.Last().Contains(privateCard),
            "RevealHoleCardAddsPublicHistory",
            ref pass,
            ref fail);

        var revealIcon = CardIconCache.GetIcon(new Card { Suit = Suit.Diamonds, Rank = Rank.Seven });
        Check(
            revealIcon != null &&
            Godot.FileAccess.FileExists(ProjectSettings.GlobalizePath("res://assets/textures/card_icons/cardDiamonds7_icon.png")),
            "CardIconCacheGeneratesRevealIcons",
            ref pass,
            ref fail);

        manager.LeaveTable();
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
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
