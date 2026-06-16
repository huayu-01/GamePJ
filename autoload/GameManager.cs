using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    [Signal] public delegate void StateChangedEventHandler(int newState);
    [Signal] public delegate void PlayerActionRequiredEventHandler(int playerId, Godot.Collections.Array<int> validActions);
    [Signal] public delegate void CardsDealtEventHandler(Godot.Collections.Array cards, int dealType);
    [Signal] public delegate void PotUpdatedEventHandler(int mainPot, Godot.Collections.Array sidePots);
    [Signal] public delegate void GameEndedEventHandler(Godot.Collections.Array<int> winners, Godot.Collections.Dictionary winnings);
    [Signal] public delegate void PlayerFoldedEventHandler(int playerId);
    [Signal] public delegate void PlayerAllInEventHandler(int playerId);
    [Signal] public delegate void HandHistoryUpdatedEventHandler();

    [Export] public int SmallBlindAmount { get; set; } = Constants.SmallBlind;
    [Export] public int BigBlindAmount { get; set; } = Constants.BigBlind;
    [Export] public bool AutoTestMode { get; set; }
    [Export] public int MinBuyIn { get; set; } = Constants.MinBuyIn;
    [Export] public int MaxBuyIn { get; set; } = Constants.MaxBuyIn;
    [Export] public int TableChipLimit { get; set; } = Constants.TableChipLimit;
    [Export] public int ThinkingTimeSeconds { get; set; } = Constants.ThinkingTimeSeconds;
    [Export] public bool AutoContinueHands { get; set; } = true;
    [Export] public int TableSeatCount { get; set; } = 9;
    [Export] public bool WaitForSettlementAnimation { get; set; }

    public GameState CurrentState { get; private set; } = GameState.Menu;
    public List<Player> Players { get; } = new();
    public List<Card> CommunityCards { get; } = new();
    public Deck Deck { get; private set; } = new();
    public PotManager PotManager { get; private set; } = new();
    public BettingRound? CurrentBettingRound { get; private set; }
    public int DealerPosition { get; private set; }
    public HashSet<int> AiPlayerIds { get; } = new();
    public Dictionary<int, HandRank> LastShownHands { get; } = new();
    public Dictionary<int, int> LastWinnings { get; } = new();
    public List<int> LastWinners { get; } = new();
    public List<PotAward> LastPotAwards { get; } = new();
    public bool LastHandWentToShowdown { get; private set; }
    public IReadOnlyList<string> HandHistory => _handHistory;
    public ulong CurrentTurnStartedMsec { get; private set; }
    public int CurrentTurnTimeLimitSeconds { get; private set; }

    private readonly List<string> _handHistory = new();
    private readonly Dictionary<int, int> _handContributions = new();
    private readonly Dictionary<int, HashSet<int>> _revealedHoleCards = new();
    private readonly HashSet<int> _currentHandPlayerIds = new();
    private readonly RandomNumberGenerator _rng = new();
    private bool _processingAiTurns;
    private bool _processingSitOutTurns;
    private bool _autoContinueScheduled;
    private bool _roundProgressCheckQueued;
    private bool _settlementPending;
    private bool _handResolved;
    private int _handNumber;
    private int _turnTimerToken;

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();
        if (AutoTestMode)
        {
            RunAutoTest(20);
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void EnsureDemoPlayers(int count = 2)
    {
        if (Players.Count >= count)
        {
            return;
        }

        Players.Clear();
        AiPlayerIds.Clear();
        TableSeatCount = Mathf.Clamp(System.Math.Max(TableSeatCount, count), 2, Constants.MaxPlayers);
        for (var i = 0; i < count; i++)
        {
            var id = i + 1;
            Players.Add(new Player
            {
                Id = id,
                Name = i == 0 ? (PlayerData.Instance?.PlayerName ?? "你") : $"AI_{i + 1}",
                Chips = MaxBuyIn,
                Position = i
            });
            if (i > 0)
            {
                AiPlayerIds.Add(id);
            }
        }
    }

    public void SyncPlayersFromNetwork()
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Players.Count == 0)
        {
            return;
        }

        TableSeatCount = Mathf.Clamp(NetworkManager.Instance.RoomMaxPlayers, 2, Constants.MaxPlayers);
        var networkIds = NetworkManager.Instance.Players.Keys.ToHashSet();
        Players.RemoveAll(player => !AiPlayerIds.Contains(player.Id) && !networkIds.Contains(player.Id));

        foreach (var info in NetworkManager.Instance.Players.Values.OrderBy(player => player.SeatIndex))
        {
            var existing = Players.FirstOrDefault(player => player.Id == info.Id);
            if (existing != null)
            {
                existing.Name = info.Name;
                existing.Position = info.SeatIndex;
                continue;
            }

            var waitingForNextHand = CurrentState is not (GameState.Menu or GameState.Lobby or GameState.GameOver);
            Players.Add(new Player
            {
                Id = info.Id,
                Name = info.Name,
                Chips = MaxBuyIn,
                Position = info.SeatIndex,
                IsSittingOut = waitingForNextHand,
                WantsSitOutNextHand = false,
                IsFolded = waitingForNextHand
            });
        }
    }

    public void ConfigureAiPlayers(bool enabled, int count)
    {
        RemoveAiPlayers();
        if (!enabled)
        {
            return;
        }

        AddAiPlayers(count);
    }

    public void AddAiPlayers(int count)
    {
        var nextId = Players.Count == 0 ? 1 : Players.Max(player => player.Id) + 1;
        for (var i = 0; i < count && Players.Count < TableSeatCount; i++)
        {
            var seat = FindFirstFreeSeat();
            if (seat < 0)
            {
                return;
            }

            var id = nextId++;
            var waitingForNextHand = CurrentState is not (GameState.Menu or GameState.Lobby or GameState.GameOver);
            Players.Add(new Player
            {
                Id = id,
                Name = $"AI_{id}",
                Chips = MaxBuyIn,
                Position = seat,
                IsSittingOut = waitingForNextHand,
                WantsSitOutNextHand = false,
                IsFolded = waitingForNextHand
            });
            AiPlayerIds.Add(id);
        }
    }

    public void RemoveAiPlayers()
    {
        Players.RemoveAll(player => AiPlayerIds.Contains(player.Id));
        AiPlayerIds.Clear();
    }

    public bool IsAiPlayer(int playerId)
    {
        return AiPlayerIds.Contains(playerId);
    }

    public void LeaveTable()
    {
        CurrentBettingRound = null;
        CurrentState = GameState.Lobby;
        CommunityCards.Clear();
        PotManager.Reset();
        _handContributions.Clear();
        LastPotAwards.Clear();
        _revealedHoleCards.Clear();
        _settlementPending = false;
        _handResolved = false;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
    }

    public void StartGame()
    {
        _currentHandPlayerIds.Clear();
        if (Players.Count < 2)
        {
            if (NetworkManager.Instance?.IsConnected != true)
            {
                EnsureDemoPlayers(2);
            }
        }

        foreach (var player in Players)
        {
            player.IsSittingOut = player.WantsSitOutNextHand;
        }

        var eligiblePlayers = GetNextHandEligiblePlayers();
        if (eligiblePlayers.Count < 2)
        {
            Logger.Warn("StartGame requires at least 2 active players.");
            return;
        }

        foreach (var player in eligiblePlayers)
        {
            _currentHandPlayerIds.Add(player.Id);
        }

        _handNumber++;
        Deck = new Deck();
        Deck.Shuffle();
        CommunityCards.Clear();
        PotManager.Reset();
        _handContributions.Clear();
        _revealedHoleCards.Clear();
        LastShownHands.Clear();
        LastWinnings.Clear();
        LastWinners.Clear();
        LastPotAwards.Clear();
        LastHandWentToShowdown = false;
        _settlementPending = false;
        _handResolved = false;
        AppendHistory($"--- 第 {_handNumber} 手 ---");

        foreach (var player in Players)
        {
            ApplyTableChipLimit(player);
            player.ResetForHand();
            if (player.IsSittingOut || player.Chips <= 0)
            {
                player.IsFolded = true;
            }
            else
            {
                _handContributions[player.Id] = 0;
            }
        }

        CollectBlinds();
        DealHoleCards();
        StartBettingRound(GameState.PreFlop);
        BroadcastState();
        QueueAiTurnProcessing();
    }

    public void CollectBlinds()
    {
        var handPlayers = GetHandPlayersInActionOrder(1);
        if (handPlayers.Count < 2)
        {
            return;
        }

        var smallBlind = PostBlind(handPlayers[0], SmallBlindAmount);
        AppendHistory($"{handPlayers[0].Name} 下小盲 {smallBlind}");
        var bigBlind = PostBlind(handPlayers[1 % handPlayers.Count], BigBlindAmount);
        AppendHistory($"{handPlayers[1 % handPlayers.Count].Name} 下大盲 {bigBlind}");
    }

    public void DealHoleCards()
    {
        var handPlayers = GetHandPlayersInActionOrder(1);
        for (var cardIndex = 0; cardIndex < 2; cardIndex++)
        {
            foreach (var player in handPlayers)
            {
                player.HoleCards[cardIndex] = Deck.Deal()!;
            }
        }

        var dealt = new Godot.Collections.Array();
        foreach (var player in handPlayers)
        {
            dealt.Add($"P{player.Id}: dealt");
        }

        AppendHistory($"向 {handPlayers.Count} 名玩家发手牌");
        EmitSignal(SignalName.CardsDealt, dealt, 0);
    }

    public void StartBettingRound(GameState state)
    {
        var handPlayers = GetHandPlayers();
        CurrentState = state;
        var currentBet = handPlayers.Max(player => player.CurrentBet);
        var actionOrder = BuildActionOrder(state);
        CurrentBettingRound = new BettingRound
        {
            MinRaise = BigBlindAmount,
            LastRaiseAmount = BigBlindAmount,
            WagerUnit = SmallBlindAmount,
            CurrentBet = currentBet,
            PlayerBets = handPlayers.ToDictionary(player => player.Id, player => player.CurrentBet),
            PlayerChips = handPlayers.ToDictionary(player => player.Id, player => player.Chips),
            PlayersToAct = actionOrder.ToList(),
            SeatOrder = actionOrder.ToList()
        };

        CurrentBettingRound.CurrentPlayerIndex = 0;
        EmitSignal(SignalName.StateChanged, (int)state);
        if (CurrentBettingRound.GetCurrentPlayerId() < 0 || CurrentBettingRound.IsRoundComplete())
        {
            BroadcastState();
            QueueBettingRoundProgressCheck();
            return;
        }

        NotifyCurrentPlayer();
        QueueSitOutTurnProcessing();
        QueueAiTurnProcessing();
    }

    public void ProcessPlayerAction(int playerId, PlayerAction action, int amount)
    {
        if (CurrentBettingRound == null || _handResolved)
        {
            return;
        }

        if (CurrentBettingRound.GetCurrentPlayerId() != playerId)
        {
            Logger.Warn($"Not player {playerId}'s turn.");
            return;
        }

        var player = Players.FirstOrDefault(item => item.Id == playerId);
        if (player == null)
        {
            return;
        }

        _turnTimerToken++;
        var beforeBet = CurrentBettingRound.PlayerBets.GetValueOrDefault(playerId, 0);
        var processed = CurrentBettingRound.ProcessAction(playerId, action, amount);
        if (!processed)
        {
            Logger.Warn($"Invalid action: P{playerId} {action} {amount}");
            return;
        }

        var afterBet = CurrentBettingRound.PlayerBets.GetValueOrDefault(playerId, beforeBet);
        var added = System.Math.Max(0, afterBet - beforeBet);
        _handContributions[playerId] = _handContributions.GetValueOrDefault(playerId, 0) + added;

        player.CurrentBet = afterBet;
        player.Chips = CurrentBettingRound.PlayerChips.GetValueOrDefault(playerId, player.Chips);
        player.IsFolded = CurrentBettingRound.FoldedPlayers.Contains(playerId);
        player.IsAllIn = CurrentBettingRound.AllInPlayers.Contains(playerId);

        if (player.IsFolded)
        {
            EmitSignal(SignalName.PlayerFolded, playerId);
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.FoldSound);
        }

        if (player.IsAllIn)
        {
            EmitSignal(SignalName.PlayerAllIn, playerId);
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.AllInSound);
        }

        NetworkManager.Instance?.BroadcastAction(playerId, action, amount);
        AppendHistory(FormatActionHistory(player, action, added, afterBet));

        if (CurrentBettingRound.IsRoundComplete())
        {
            EndBettingRound();
        }
        else
        {
            NotifyCurrentPlayer();
        }

        BroadcastState();
        QueueSitOutTurnProcessing();
        QueueAiTurnProcessing();
    }

    public void ProcessRemoteAction(int playerId, int action, int amount)
    {
        if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost)
        {
            return;
        }

        ProcessPlayerAction(playerId, (PlayerAction)action, amount);
    }

    public void EndBettingRound()
    {
        if (CurrentBettingRound == null || _handResolved)
        {
            return;
        }

        PotManager.CollectBets(_handContributions);
        foreach (var player in Players)
        {
            player.CurrentBet = 0;
        }

        EmitPotUpdated();

        if (GetHandPlayers().Count(player => !player.IsFolded) <= 1)
        {
            AwardLastStanding();
            return;
        }

        if (GetHandPlayers().Where(player => !player.IsFolded).All(player => player.IsAllIn))
        {
            DealUntilRiver();
            StartShowdown();
            return;
        }

        switch (CurrentState)
        {
            case GameState.PreFlop:
                DealCommunityCards(3);
                StartBettingRound(GameState.Flop);
                break;
            case GameState.Flop:
                DealCommunityCards(1);
                StartBettingRound(GameState.Turn);
                break;
            case GameState.Turn:
                DealCommunityCards(1);
                StartBettingRound(GameState.River);
                break;
            case GameState.River:
                StartShowdown();
                break;
        }
    }

    public void DealCommunityCards(int count)
    {
        var dealt = new Godot.Collections.Array();
        for (var i = 0; i < count; i++)
        {
            var card = Deck.Deal();
            if (card == null)
            {
                break;
            }

            CommunityCards.Add(card);
            dealt.Add(card.ShortName);
        }

        var dealType = CommunityCards.Count switch
        {
            3 => 1,
            4 => 2,
            _ => 3
        };
        AppendHistory($"{DealTypeName(dealType)}: {FormatCommunityHistory(dealt.Count)}");
        EmitSignal(SignalName.CardsDealt, dealt, dealType);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.DealSound);
    }

    private string FormatCommunityHistory(int newCardCount)
    {
        var splitIndex = System.Math.Max(0, CommunityCards.Count - newCardCount);
        var previous = CommunityCards.Take(splitIndex).Select(card => card.ShortName);
        var newest = CommunityCards.Skip(splitIndex).Select(card => card.ShortName);
        return splitIndex == 0
            ? string.Join(" ", newest)
            : $"{string.Join(" ", previous)} | {string.Join(" ", newest)}";
    }

    public void StartShowdown()
    {
        if (_handResolved)
        {
            return;
        }

        _handResolved = true;
        _turnTimerToken++;
        CurrentBettingRound = null;
        CurrentState = GameState.Showdown;
        LastHandWentToShowdown = true;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);

        var activePlayers = GetHandPlayers().Where(player => !player.IsFolded).ToArray();
        var winnersByPot = new List<List<int>>();
        winnersByPot.Add(FindWinners(activePlayers));

        foreach (var sidePot in PotManager.SidePots)
        {
            var eligible = activePlayers.Where(player => sidePot.EligiblePlayers.Contains(player.Id)).ToArray();
            winnersByPot.Add(FindWinners(eligible));
        }

        LastPotAwards.Clear();
        LastPotAwards.AddRange(PotManager.BuildPotAwards(winnersByPot));
        LastWinnings.Clear();
        foreach (var award in LastPotAwards)
        {
            foreach (var pair in award.Shares)
            {
                LastWinnings[pair.Key] = LastWinnings.GetValueOrDefault(pair.Key, 0) + pair.Value;
                var player = Players.FirstOrDefault(item => item.Id == pair.Key);
                if (player != null)
                {
                    player.Chips += pair.Value;
                }
            }
        }

        var winnerIds = winnersByPot.SelectMany(list => list).Distinct().OrderBy(id => id).ToGodotArray();
        LastWinners.Clear();
        LastWinners.AddRange(winnerIds);
        LastShownHands.Clear();
        foreach (var player in activePlayers)
        {
            LastShownHands[player.Id] = HandEvaluator.EvaluateHand(player.HoleCards, CommunityCards.ToArray());
        }

        foreach (var player in activePlayers.OrderBy(player => player.Position))
        {
            var rank = LastShownHands[player.Id].Category.ToDisplayName();
            AppendHistory($"摊牌 {player.Name}: {player.HoleCards.JoinCards()} ({rank})");
        }

        var winningsDict = new Godot.Collections.Dictionary();
        foreach (var pair in LastWinnings)
        {
            winningsDict[pair.Key] = pair.Value;
        }

        MarkSettlementPendingIfNeeded();
        AppendSettlementHistory();
        EmitSignal(SignalName.GameEnded, winnerIds, winningsDict);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.WinSound);
        CompleteSettlementAfterOptionalAnimation();
    }

    public void EndHand()
    {
        _settlementPending = false;
        _handResolved = false;
        LockBustedPlayers();
        AutoRebuyAiPlayers();
        DealerPosition = GetNextActiveSeatIndex(DealerPosition);
        _currentHandPlayerIds.Clear();
        ReleaseNextHandWaiters();
        CurrentBettingRound = null;
        CurrentState = GetNextHandEligiblePlayers().Count <= 1 ? GameState.GameOver : GameState.Lobby;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        BroadcastState();
        ScheduleAutoContinue();
    }

    public void CompleteSettlementAnimation()
    {
        if (!_settlementPending)
        {
            return;
        }

        EndHand();
    }

    public void RunAutoTest(int hands = 20)
    {
        AutoTestMode = true;
        EnsureDemoPlayers(2);
        Logger.Info($"AutoTest start: {hands} hands");

        for (var hand = 1; hand <= hands; hand++)
        {
            if (Players.Count < 2)
            {
                EnsureDemoPlayers(2);
            }

            Logger.Info($"[HAND {hand}] start");
            StartGame();
            var guard = 0;
            while (CurrentState != GameState.Lobby && CurrentState != GameState.GameOver && guard++ < 200)
            {
                var currentPlayerId = CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
                if (currentPlayerId < 0)
                {
                    EndBettingRound();
                    continue;
                }

                var action = PickAutoAction(currentPlayerId);
                Logger.Info($"[ACTION] Player{currentPlayerId} {action.Action} {action.Amount}");
                ProcessPlayerAction(currentPlayerId, action.Action, action.Amount);
            }

            if (Players.Any(player => player.Chips < 0))
            {
                Logger.Error("AutoTest failed: negative chips detected.");
                break;
            }
        }

        Logger.Info("AutoTest completed");
    }

    public GameStateDTO CreateStateDTO()
    {
        return new GameStateDTO
        {
            CurrentState = CurrentState,
            Players = Players.OrderBy(player => player.Position).Select(PlayerDTO.FromPlayer).ToList(),
            CommunityCards = CommunityCards.Select(CardDTO.FromCard).ToList(),
            MainPot = PotManager.MainPot,
            SidePots = PotManager.SidePots.Select(sidePot => new SidePotDTO
            {
                Amount = sidePot.Amount,
                EligiblePlayers = sidePot.EligiblePlayers.ToList()
            }).ToList(),
            CurrentBet = CurrentBettingRound?.CurrentBet ?? 0,
            CurrentPlayerId = CurrentBettingRound?.GetCurrentPlayerId() ?? -1,
            DealerPosition = DealerPosition,
            TableSeatCount = TableSeatCount
        };
    }

    private int PostBlind(Player player, int amount)
    {
        var posted = System.Math.Min(player.Chips, amount);
        player.Chips -= posted;
        player.CurrentBet += posted;
        player.IsAllIn = player.Chips == 0;
        _handContributions[player.Id] = _handContributions.GetValueOrDefault(player.Id, 0) + posted;
        return posted;
    }

    private List<int> BuildActionOrder(GameState state)
    {
        var orderedPlayers = state == GameState.PreFlop
            ? GetPreFlopActionOrder()
            : GetHandPlayersInActionOrder(1);
        var order = new List<int>();
        foreach (var player in orderedPlayers)
        {
            if (!player.IsFolded && !player.IsAllIn)
            {
                order.Add(player.Id);
            }
        }

        return order;
    }

    private List<Player> GetPreFlopActionOrder()
    {
        var blindOrder = GetHandPlayersInActionOrder(1);
        if (blindOrder.Count <= 2)
        {
            return blindOrder;
        }

        return blindOrder
            .Skip(2)
            .Concat(blindOrder.Take(2))
            .ToList();
    }

    private void NotifyCurrentPlayer()
    {
        var playerId = CurrentBettingRound?.GetCurrentPlayerId() ?? -1;
        if (playerId < 0)
        {
            QueueBettingRoundProgressCheck();
            return;
        }

        EmitSignal(SignalName.PlayerActionRequired, playerId, GetValidActions(playerId).ToGodotArray());
        StartTurnTimer(playerId);
        QueueSitOutTurnProcessing();
    }

    private IEnumerable<int> GetValidActions(int playerId)
    {
        if (CurrentBettingRound == null)
        {
            return Enumerable.Empty<int>();
        }

        return System.Enum.GetValues(typeof(PlayerAction))
            .Cast<PlayerAction>()
            .Where(action => CurrentBettingRound.IsValidAction(playerId, action, GetSuggestedAmount(playerId, action)))
            .Select(action => (int)action);
    }

    private int GetSuggestedAmount(int playerId, PlayerAction action)
    {
        var player = Players.First(item => item.Id == playerId);
        var currentBet = CurrentBettingRound?.CurrentBet ?? 0;
        var ownBet = CurrentBettingRound?.PlayerBets.GetValueOrDefault(playerId, 0) ?? 0;
        return action switch
        {
            PlayerAction.Call => currentBet - ownBet,
            PlayerAction.Bet => BigBlindAmount,
            PlayerAction.Raise => currentBet + (CurrentBettingRound?.LastRaiseAmount ?? BigBlindAmount),
            PlayerAction.AllIn => player.Chips,
            _ => 0
        };
    }

    private void EmitPotUpdated()
    {
        var sidePots = new Godot.Collections.Array();
        foreach (var sidePot in PotManager.SidePots)
        {
            sidePots.Add(new Godot.Collections.Dictionary
            {
                ["amount"] = sidePot.Amount,
                ["eligible_players"] = sidePot.EligiblePlayers.ToGodotArray()
            });
        }

        EmitSignal(SignalName.PotUpdated, PotManager.MainPot, sidePots);
    }

    private void AppendHistory(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _handHistory.Add(message);
        if (_handHistory.Count > 500)
        {
            _handHistory.RemoveRange(0, _handHistory.Count - 500);
        }

        EmitSignal(SignalName.HandHistoryUpdated);
    }

    private static string FormatActionHistory(Player player, PlayerAction action, int added, int totalBet)
    {
        return action switch
        {
            PlayerAction.Fold => $"{player.Name} 弃牌",
            PlayerAction.Check => $"{player.Name} 过牌",
            PlayerAction.Call => $"{player.Name} 跟注 (-{added})",
            PlayerAction.Bet => $"{player.Name} 下注 (-{added}) 到 {totalBet}",
            PlayerAction.Raise => $"{player.Name} 加注 (-{added}) 到 {totalBet}",
            PlayerAction.AllIn => $"{player.Name} 全下 (-{added}) 到 {totalBet}",
            _ => $"{player.Name} {action.ToDisplayName()}"
        };
    }

    private static string DealTypeName(int dealType)
    {
        return dealType switch
        {
            1 => "翻牌",
            2 => "转牌",
            3 => "河牌",
            _ => "公共牌"
        };
    }

    private void AppendSettlementHistory()
    {
        foreach (var pair in LastWinnings.OrderBy(pair => Players.FirstOrDefault(player => player.Id == pair.Key)?.Position ?? int.MaxValue))
        {
            var player = Players.FirstOrDefault(item => item.Id == pair.Key);
            AppendHistory($"{player?.Name ?? $"P{pair.Key}"} 收池 (+{pair.Value})");
        }
    }

    private void AwardLastStanding()
    {
        if (_handResolved)
        {
            return;
        }

        _handResolved = true;
        _turnTimerToken++;
        CurrentBettingRound = null;
        var winner = GetHandPlayers().First(player => !player.IsFolded);
        LastHandWentToShowdown = false;
        LastShownHands.Clear();
        PotManager.CollectBets(_handContributions);
        var total = PotManager.MainPot + PotManager.SidePots.Sum(sidePot => sidePot.Amount);
        winner.Chips += total;
        LastWinners.Clear();
        LastWinners.Add(winner.Id);
        LastWinnings.Clear();
        LastWinnings[winner.Id] = total;
        LastPotAwards.Clear();
        var lastStandingAwards = PotManager.BuildPotAwards(
            Enumerable.Range(0, PotManager.SidePots.Count + 1)
                .Select(_ => new List<int> { winner.Id })
                .ToList());
        if (lastStandingAwards.Count > 0)
        {
            LastPotAwards.AddRange(lastStandingAwards);
        }
        else
        {
            LastPotAwards.Add(new PotAward
            {
                PotIndex = 0,
                Amount = total,
                Winners = new List<int> { winner.Id },
                Shares = new Dictionary<int, int> { [winner.Id] = total }
            });
        }
        var winners = new Godot.Collections.Array<int> { winner.Id };
        var winnings = new Godot.Collections.Dictionary { [winner.Id] = total };
        AppendHistory($"其他玩家弃牌，{winner.Name} 收下底池 {total}");
        MarkSettlementPendingIfNeeded();
        EmitSignal(SignalName.GameEnded, winners, winnings);
        CompleteSettlementAfterOptionalAnimation();
    }

    private void CompleteSettlementAfterOptionalAnimation()
    {
        if (WaitForSettlementAnimation && !AutoTestMode)
        {
            _settlementPending = true;
            return;
        }

        EndHand();
    }

    private void MarkSettlementPendingIfNeeded()
    {
        if (WaitForSettlementAnimation && !AutoTestMode)
        {
            _settlementPending = true;
        }
    }

    private void DealUntilRiver()
    {
        while (CommunityCards.Count < 5)
        {
            DealCommunityCards(CommunityCards.Count == 0 ? 3 : 1);
        }
    }

    private List<int> FindWinners(Player[] players)
    {
        var rankings = new List<(Player Player, HandRank Rank)>();
        foreach (var player in players)
        {
            rankings.Add((player, HandEvaluator.EvaluateHand(player.HoleCards, CommunityCards.ToArray())));
        }

        if (rankings.Count == 0)
        {
            return new List<int>();
        }

        var best = rankings.Max(item => item.Rank);
        return rankings
            .Where(item => item.Rank.CompareTo(best) == 0)
            .Select(item => item.Player.Id)
            .OrderBy(id => id)
            .ToList();
    }

    private (PlayerAction Action, int Amount) PickAutoAction(int playerId)
    {
        var player = Players.First(item => item.Id == playerId);
        var currentBet = CurrentBettingRound?.CurrentBet ?? 0;
        var ownBet = CurrentBettingRound?.PlayerBets.GetValueOrDefault(playerId, 0) ?? 0;
        var callAmount = System.Math.Max(0, currentBet - ownBet);

        if (callAmount == 0)
        {
            return _rng.RandiRange(0, 4) == 0 && player.Chips > BigBlindAmount
                ? (PlayerAction.Bet, System.Math.Min(player.Chips, BigBlindAmount))
                : (PlayerAction.Check, 0);
        }

        if (callAmount >= player.Chips)
        {
            return (PlayerAction.AllIn, player.Chips);
        }

        var roll = _rng.RandiRange(0, 9);
        if (roll == 0)
        {
            return (PlayerAction.Fold, 0);
        }

        var minRaiseTotal = currentBet + (CurrentBettingRound?.LastRaiseAmount ?? BigBlindAmount);
        if (roll >= 8 && ownBet + player.Chips >= minRaiseTotal)
        {
            return (PlayerAction.Raise, minRaiseTotal);
        }

        return (PlayerAction.Call, callAmount);
    }

    private void BroadcastState()
    {
        NetworkManager.Instance?.BroadcastGameState(CreateStateDTO().ToDictionary());
    }

    private async void StartTurnTimer(int playerId)
    {
        if (ThinkingTimeSeconds <= 0)
        {
            CurrentTurnStartedMsec = 0;
            CurrentTurnTimeLimitSeconds = 0;
            EmitSignal(SignalName.StateChanged, (int)CurrentState);
            return;
        }

        var token = ++_turnTimerToken;
        CurrentTurnStartedMsec = Time.GetTicksMsec();
        CurrentTurnTimeLimitSeconds = ThinkingTimeSeconds;
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        await ToSignal(GetTree().CreateTimer(ThinkingTimeSeconds), SceneTreeTimer.SignalName.Timeout);
        if (CurrentBettingRound == null || token != _turnTimerToken || CurrentBettingRound.GetCurrentPlayerId() != playerId)
        {
            return;
        }

        var player = Players.FirstOrDefault(item => item.Id == playerId);
        if (player == null || player.IsFolded || player.IsAllIn || player.IsSittingOut)
        {
            return;
        }

        var action = CurrentBettingRound.IsValidAction(playerId, PlayerAction.Check, 0)
            ? PlayerAction.Check
            : PlayerAction.Fold;
        AppendHistory($"{player.Name} 超时，自动{action.ToDisplayName()}");
        ProcessPlayerAction(playerId, action, 0);
    }

    public HandRank? GetVisibleHandRank(int playerId)
    {
        if (LastShownHands.TryGetValue(playerId, out var lastRank))
        {
            return lastRank;
        }

        if (LastWinners.Count > 0 && !LastHandWentToShowdown)
        {
            return null;
        }

        var player = Players.FirstOrDefault(item => item.Id == playerId);
        if (player == null || player.IsSittingOut || player.HoleCards.Any(card => card == null) || CommunityCards.Count < 3)
        {
            return null;
        }

        return HandEvaluator.EvaluateHand(player.HoleCards, CommunityCards.ToArray());
    }

    public bool RevealHoleCard(int playerId, int cardIndex)
    {
        if (cardIndex is < 0 or > 1 || !_currentHandPlayerIds.Contains(playerId))
        {
            return false;
        }

        var player = Players.FirstOrDefault(item => item.Id == playerId);
        var card = player?.HoleCards[cardIndex];
        if (player == null || card == null || LastShownHands.ContainsKey(playerId))
        {
            return false;
        }

        if (!_revealedHoleCards.TryGetValue(playerId, out var revealed))
        {
            revealed = new HashSet<int>();
            _revealedHoleCards[playerId] = revealed;
        }

        if (!revealed.Add(cardIndex))
        {
            return false;
        }

        AppendHistory($"{player.Name} 亮牌: {FormatRevealedHoleCards(player, revealed)}");
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        return true;
    }

    private static string FormatRevealedHoleCards(Player player, HashSet<int> revealed)
    {
        return string.Join(" ", Enumerable.Range(0, 2).Select(index =>
        {
            var card = player.HoleCards[index];
            return revealed.Contains(index) && card != null ? card.ShortName : "BACK";
        }));
    }

    public bool IsHoleCardRevealed(int playerId, int cardIndex)
    {
        return _revealedHoleCards.TryGetValue(playerId, out var revealed) && revealed.Contains(cardIndex);
    }

    public void SetTableChipLimit(int limit)
    {
        TableChipLimit = System.Math.Max(MaxBuyIn, limit);
        foreach (var player in Players)
        {
            ApplyTableChipLimit(player);
        }
    }

    public void ConfigureRoomRules(int smallBlind, int minBuyIn, int maxBuyIn, int tableChipLimit, int thinkingTimeSeconds = Constants.ThinkingTimeSeconds)
    {
        SmallBlindAmount = System.Math.Max(1, smallBlind);
        BigBlindAmount = SmallBlindAmount * 2;
        MinBuyIn = System.Math.Max(1, minBuyIn);
        MaxBuyIn = System.Math.Max(MinBuyIn, maxBuyIn);
        TableChipLimit = System.Math.Max(MaxBuyIn, tableChipLimit);
        ThinkingTimeSeconds = System.Math.Max(0, thinkingTimeSeconds);

        foreach (var player in Players)
        {
            ApplyTableChipLimit(player);
        }

        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        BroadcastState();
    }

    public void SetSittingOut(int playerId, bool sittingOut)
    {
        var player = Players.FirstOrDefault(item => item.Id == playerId);
        if (player == null)
        {
            return;
        }

        if (!sittingOut && player.Chips <= 0)
        {
            player.WantsSitOutNextHand = false;
            player.IsSittingOut = false;
            EmitSignal(SignalName.StateChanged, (int)CurrentState);
            return;
        }

        player.WantsSitOutNextHand = sittingOut;
        if (CurrentState is GameState.Lobby or GameState.Menu or GameState.GameOver)
        {
            player.IsSittingOut = sittingOut;
        }
        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        QueueSitOutTurnProcessing();
    }

    public void RebuyPlayer(int playerId, int amount = 0)
    {
        var player = Players.FirstOrDefault(item => item.Id == playerId);
        if (player == null)
        {
            return;
        }

        var rebuyAmount = amount <= 0 ? MaxBuyIn : amount;
        var buyIn = System.Math.Clamp(rebuyAmount, MinBuyIn, MaxBuyIn);
        player.Chips = System.Math.Min(TableChipLimit, player.Chips + buyIn);
        player.WantsSitOutNextHand = false;

        if (CurrentState is GameState.Lobby or GameState.Menu or GameState.GameOver)
        {
            player.IsSittingOut = false;
            player.IsFolded = false;
        }
        else
        {
            // 正在进行的牌局不插入补码玩家，避免破坏当前下注轮。
            player.IsSittingOut = true;
            player.IsFolded = true;
        }

        EmitSignal(SignalName.StateChanged, (int)CurrentState);
        BroadcastState();
    }

    public string GetBlindRole(int playerId)
    {
        var handPlayers = GetHandPlayersInActionOrder(1);
        if (handPlayers.Count < 2)
        {
            return "";
        }

        if (handPlayers[0].Id == playerId)
        {
            return "小盲";
        }

        return handPlayers[1 % handPlayers.Count].Id == playerId ? "大盲" : "";
    }

    private List<Player> GetHandPlayers()
    {
        if (_currentHandPlayerIds.Count > 0)
        {
            return Players
                .Where(player => _currentHandPlayerIds.Contains(player.Id) && !player.IsSittingOut)
                .OrderBy(player => player.Position)
                .ToList();
        }

        return GetNextHandEligiblePlayers();
    }

    private List<Player> GetNextHandEligiblePlayers()
    {
        return Players
            .Where(player => !player.WantsSitOutNextHand && player.Chips > 0)
            .OrderBy(player => player.Position)
            .ToList();
    }

    private List<Player> GetHandPlayersInActionOrder(int offsetFromDealer)
    {
        var result = new List<Player>();
        if (TableSeatCount <= 0)
        {
            return result;
        }

        var start = NormalizeSeat(DealerPosition + offsetFromDealer);
        for (var i = 0; i < TableSeatCount; i++)
        {
            var seat = NormalizeSeat(start + i);
            var player = Players.FirstOrDefault(item => item.Position == seat);
            if (player != null && IsHandSeatActive(player))
            {
                result.Add(player);
            }
        }

        return result;
    }

    private bool IsHandSeatActive(Player player)
    {
        if (player.IsSittingOut)
        {
            return false;
        }

        return _currentHandPlayerIds.Count > 0
            ? _currentHandPlayerIds.Contains(player.Id)
            : player.Chips > 0;
    }

    private int GetNextActiveSeatIndex(int currentIndex)
    {
        if (TableSeatCount <= 0)
        {
            return 0;
        }

        for (var i = 1; i <= TableSeatCount; i++)
        {
            var index = NormalizeSeat(currentIndex + i);
            var player = Players.FirstOrDefault(item => item.Position == index);
            if (player is { WantsSitOutNextHand: false, Chips: > 0 })
            {
                return index;
            }
        }

        return NormalizeSeat(currentIndex);
    }

    private int FindFirstFreeSeat()
    {
        for (var seat = 0; seat < TableSeatCount; seat++)
        {
            if (Players.All(player => player.Position != seat))
            {
                return seat;
            }
        }

        return -1;
    }

    private int NormalizeSeat(int seat)
    {
        var seatCount = Mathf.Clamp(TableSeatCount, 2, Constants.MaxPlayers);
        var value = seat % seatCount;
        return value < 0 ? value + seatCount : value;
    }

    private void LockBustedPlayers()
    {
        foreach (var player in Players.Where(player => player.Chips <= 0))
        {
            player.Chips = 0;
            player.IsSittingOut = false;
            player.WantsSitOutNextHand = false;
            player.IsFolded = true;
            player.IsAllIn = false;
        }
    }

    private void ReleaseNextHandWaiters()
    {
        foreach (var player in Players.Where(player => player.Chips > 0 && !player.WantsSitOutNextHand))
        {
            player.IsSittingOut = false;
        }
    }

    private async void ScheduleAutoContinue()
    {
        if (!AutoContinueHands || AutoTestMode || _autoContinueScheduled || CurrentState != GameState.Lobby)
        {
            return;
        }

        _autoContinueScheduled = true;
        await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
        _autoContinueScheduled = false;
        if (CurrentState == GameState.Lobby && GetNextHandEligiblePlayers().Count >= 2)
        {
            StartGame();
        }
    }

    private void ApplyTableChipLimit(Player player)
    {
        if (player.Chips <= TableChipLimit)
        {
            return;
        }

        var overflow = player.Chips - TableChipLimit;
        player.Chips = TableChipLimit;
        player.AccountBalance += overflow;
    }

    private void AutoRebuyAiPlayers()
    {
        foreach (var player in Players.Where(player => AiPlayerIds.Contains(player.Id) && player.Chips <= 0))
        {
            player.Chips = System.Math.Min(MaxBuyIn, TableChipLimit);
            player.IsSittingOut = false;
            player.WantsSitOutNextHand = false;
            player.IsFolded = false;
            player.IsAllIn = false;
        }
    }

    private void QueueAiTurnProcessing()
    {
        if (_processingAiTurns || AiPlayerIds.Count == 0)
        {
            return;
        }

        CallDeferred(nameof(ProcessAiTurns));
    }

    private void QueueSitOutTurnProcessing()
    {
        if (_processingSitOutTurns)
        {
            return;
        }

        CallDeferred(nameof(ProcessSitOutTurns));
    }

    private void QueueBettingRoundProgressCheck()
    {
        if (_roundProgressCheckQueued || CurrentBettingRound == null)
        {
            return;
        }

        _roundProgressCheckQueued = true;
        CallDeferred(nameof(CheckBettingRoundProgress));
    }

    public void CheckBettingRoundProgress()
    {
        _roundProgressCheckQueued = false;
        if (CurrentBettingRound == null)
        {
            return;
        }

        if (CurrentBettingRound.GetCurrentPlayerId() < 0 || CurrentBettingRound.IsRoundComplete())
        {
            EndBettingRound();
            return;
        }

        QueueSitOutTurnProcessing();
        QueueAiTurnProcessing();
    }

    private async void ProcessAiTurns()
    {
        if (_processingAiTurns)
        {
            return;
        }

        _processingAiTurns = true;
        try
        {
            var guard = 0;
            while (CurrentBettingRound != null && guard++ < 80)
            {
                var playerId = CurrentBettingRound.GetCurrentPlayerId();
                if (playerId < 0)
                {
                    QueueBettingRoundProgressCheck();
                    break;
                }

                var player = Players.FirstOrDefault(item => item.Id == playerId);
                if (player != null && ShouldAutoFoldOrCheck(player))
                {
                    QueueSitOutTurnProcessing();
                    break;
                }

                if (!AiPlayerIds.Contains(playerId))
                {
                    break;
                }

                await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
                if (CurrentBettingRound == null || CurrentBettingRound.GetCurrentPlayerId() != playerId)
                {
                    continue;
                }

                var action = PickAutoAction(playerId);
                Logger.Info($"[AI] Player{playerId} {action.Action} {action.Amount}");
                ProcessPlayerAction(playerId, action.Action, action.Amount);
            }
        }
        finally
        {
            _processingAiTurns = false;
        }
    }

    private async void ProcessSitOutTurns()
    {
        if (_processingSitOutTurns)
        {
            return;
        }

        _processingSitOutTurns = true;
        try
        {
            var guard = 0;
            while (CurrentBettingRound != null && guard++ < 80)
            {
                var playerId = CurrentBettingRound.GetCurrentPlayerId();
                if (playerId < 0)
                {
                    QueueBettingRoundProgressCheck();
                    break;
                }

                var player = Players.FirstOrDefault(item => item.Id == playerId);
                if (player == null || !ShouldAutoFoldOrCheck(player))
                {
                    break;
                }

                await ToSignal(GetTree().CreateTimer(0.18), SceneTreeTimer.SignalName.Timeout);
                if (CurrentBettingRound == null || CurrentBettingRound.GetCurrentPlayerId() != playerId || !ShouldAutoFoldOrCheck(player))
                {
                    continue;
                }

                var action = CurrentBettingRound.IsValidAction(playerId, PlayerAction.Check, 0)
                    ? PlayerAction.Check
                    : PlayerAction.Fold;
                Logger.Info($"[SIT_OUT_AUTO] Player{playerId} {action}");
                ProcessPlayerAction(playerId, action, 0);
            }
        }
        finally
        {
            _processingSitOutTurns = false;
        }
    }

    private static bool ShouldAutoFoldOrCheck(Player player)
    {
        return player.WantsSitOutNextHand && !player.IsSittingOut && !player.IsFolded && !player.IsAllIn;
    }
}
