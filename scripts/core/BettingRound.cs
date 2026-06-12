using System.Collections.Generic;
using System.Linq;

public class BettingRound
{
    public int CurrentBet { get; set; }
    public int MinRaise { get; set; } = 20;
    public int LastRaiseAmount { get; set; } = 20;
    public int WagerUnit { get; set; } = 1;
    public int CurrentPlayerIndex { get; set; }
    public List<int> PlayersToAct { get; set; } = new();
    public List<int> SeatOrder { get; set; } = new();
    public Dictionary<int, int> PlayerBets { get; set; } = new();
    public Dictionary<int, int> PlayerChips { get; set; } = new();
    public Dictionary<int, int> ActionClosedAtBet { get; set; } = new();
    public HashSet<int> FoldedPlayers { get; } = new();
    public HashSet<int> AllInPlayers { get; } = new();

    public bool ProcessAction(int playerId, PlayerAction action, int amount)
    {
        EnsurePlayer(playerId);
        if (!IsValidAction(playerId, action, amount))
        {
            return false;
        }

        var previousBet = PlayerBets[playerId];
        switch (action)
        {
            case PlayerAction.Fold:
                FoldedPlayers.Add(playerId);
                PlayersToAct.Remove(playerId);
                break;
            case PlayerAction.Check:
                ActionClosedAtBet[playerId] = CurrentBet;
                PlayersToAct.Remove(playerId);
                break;
            case PlayerAction.Call:
                PlayerBets[playerId] = CurrentBet;
                SpendChips(playerId, CurrentBet - previousBet);
                ActionClosedAtBet[playerId] = CurrentBet;
                PlayersToAct.Remove(playerId);
                break;
            case PlayerAction.Bet:
            case PlayerAction.Raise:
                var raiseAmount = amount - CurrentBet;
                PlayerBets[playerId] = amount;
                SpendChips(playerId, amount - previousBet);
                LastRaiseAmount = raiseAmount;
                CurrentBet = amount;
                ActionClosedAtBet[playerId] = CurrentBet;
                ResetPlayersToActAfterAggression(playerId);
                break;
            case PlayerAction.AllIn:
                var totalBet = previousBet + amount;
                PlayerBets[playerId] = totalBet;
                SpendChips(playerId, amount);
                AllInPlayers.Add(playerId);
                if (totalBet > CurrentBet)
                {
                    var allInRaiseAmount = totalBet - CurrentBet;
                    CurrentBet = totalBet;
                    ActionClosedAtBet[playerId] = CurrentBet;
                    if (allInRaiseAmount >= LastRaiseAmount)
                    {
                        LastRaiseAmount = allInRaiseAmount;
                        ResetPlayersToActAfterAggression(playerId);
                    }
                    else
                    {
                        RebuildPlayersToActAfterShortAllIn(playerId);
                    }
                }
                else
                {
                    ActionClosedAtBet[playerId] = CurrentBet;
                    PlayersToAct.Remove(playerId);
                }
                break;
        }

        MoveToNextPlayer();
        return true;
    }

    public bool IsValidAction(int playerId, PlayerAction action, int amount)
    {
        EnsurePlayer(playerId);
        if (FoldedPlayers.Contains(playerId) || AllInPlayers.Contains(playerId))
        {
            return false;
        }

        var currentPlayerBet = PlayerBets[playerId];
        var chips = PlayerChips.GetValueOrDefault(playerId, int.MaxValue / 4);
        var callAmount = CurrentBet - currentPlayerBet;

        return action switch
        {
            PlayerAction.Fold => true,
            PlayerAction.Check => CurrentBet == 0 || currentPlayerBet == CurrentBet,
            PlayerAction.Call => CurrentBet > 0 && callAmount >= 0 && amount >= callAmount && chips >= callAmount,
            PlayerAction.Bet => CurrentBet == 0 && amount >= MinRaise && amount - currentPlayerBet <= chips && IsValidWagerUnit(amount),
            PlayerAction.Raise => CurrentBet > 0 && CanPlayerRaise(playerId) && amount >= CurrentBet + LastRaiseAmount && amount - currentPlayerBet <= chips && IsValidWagerUnit(amount),
            PlayerAction.AllIn => IsValidAllIn(playerId, amount, chips, currentPlayerBet),
            _ => false
        };
    }

    public bool IsRoundComplete()
    {
        var activePlayers = PlayerBets.Keys
            .Where(playerId => !FoldedPlayers.Contains(playerId))
            .ToArray();

        if (activePlayers.Length <= 1)
        {
            return true;
        }

        if (PlayersToAct.Any(playerId =>
            activePlayers.Contains(playerId) &&
            !FoldedPlayers.Contains(playerId) &&
            !AllInPlayers.Contains(playerId)))
        {
            return false;
        }

        return activePlayers.All(playerId =>
            AllInPlayers.Contains(playerId) || PlayerBets.GetValueOrDefault(playerId, 0) == CurrentBet);
    }

    public void MoveToNextPlayer()
    {
        var candidates = PlayersToAct
            .Where(playerId => !FoldedPlayers.Contains(playerId) && !AllInPlayers.Contains(playerId))
            .ToList();

        if (candidates.Count == 0)
        {
            CurrentPlayerIndex = -1;
            return;
        }

        if (CurrentPlayerIndex < 0)
        {
            CurrentPlayerIndex = 0;
            return;
        }

        if (CurrentPlayerIndex >= candidates.Count)
        {
            CurrentPlayerIndex = 0;
        }
    }

    public int GetCurrentPlayerId()
    {
        var candidates = PlayersToAct
            .Where(playerId => !FoldedPlayers.Contains(playerId) && !AllInPlayers.Contains(playerId))
            .ToList();

        if (candidates.Count == 0)
        {
            return -1;
        }

        if (CurrentPlayerIndex < 0 || CurrentPlayerIndex >= candidates.Count)
        {
            CurrentPlayerIndex = 0;
        }

        return candidates[CurrentPlayerIndex];
    }

    private void EnsurePlayer(int playerId)
    {
        PlayerBets.TryAdd(playerId, 0);
        PlayerChips.TryAdd(playerId, int.MaxValue / 4);
    }

    private bool CanPlayerRaise(int playerId)
    {
        if (FoldedPlayers.Contains(playerId) || AllInPlayers.Contains(playerId))
        {
            return false;
        }

        return !ActionClosedAtBet.TryGetValue(playerId, out var closedBet) || CurrentBet - closedBet >= LastRaiseAmount;
    }

    private bool IsValidAllIn(int playerId, int amount, int chips, int currentPlayerBet)
    {
        if (amount <= 0 || amount > chips)
        {
            return false;
        }

        var totalBet = currentPlayerBet + amount;
        return totalBet <= CurrentBet || CurrentBet == 0 || CanPlayerRaise(playerId);
    }

    private bool IsValidWagerUnit(int amount)
    {
        return WagerUnit <= 1 || amount % WagerUnit == 0;
    }

    private void SpendChips(int playerId, int amount)
    {
        if (!PlayerChips.ContainsKey(playerId) || PlayerChips[playerId] >= int.MaxValue / 8)
        {
            return;
        }

        PlayerChips[playerId] = System.Math.Max(0, PlayerChips[playerId] - System.Math.Max(0, amount));
        if (PlayerChips[playerId] == 0)
        {
            AllInPlayers.Add(playerId);
        }
    }

    private void ResetPlayersToActAfterAggression(int aggressorId)
    {
        var sourceOrder = SeatOrder.Count > 0 ? SeatOrder : PlayerBets.Keys.ToList();
        var startIndex = sourceOrder.IndexOf(aggressorId);
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        var ordered = new List<int>();
        for (var i = 1; i <= sourceOrder.Count; i++)
        {
            var playerId = sourceOrder[(startIndex + i) % sourceOrder.Count];
            if (playerId != aggressorId && PlayerBets.ContainsKey(playerId) && !FoldedPlayers.Contains(playerId) && !AllInPlayers.Contains(playerId))
            {
                ordered.Add(playerId);
            }
        }

        PlayersToAct = ordered;
        CurrentPlayerIndex = 0;
    }

    private void RebuildPlayersToActAfterShortAllIn(int playerId)
    {
        PlayersToAct.Remove(playerId);
        var sourceOrder = SeatOrder.Count > 0 ? SeatOrder : PlayerBets.Keys.ToList();
        var startIndex = sourceOrder.IndexOf(playerId);
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        var existing = PlayersToAct.ToHashSet();
        var ordered = new List<int>();
        for (var i = 1; i <= sourceOrder.Count; i++)
        {
            var nextId = sourceOrder[(startIndex + i) % sourceOrder.Count];
            if (!PlayerBets.ContainsKey(nextId) || FoldedPlayers.Contains(nextId) || AllInPlayers.Contains(nextId))
            {
                continue;
            }

            if (existing.Contains(nextId) || PlayerBets.GetValueOrDefault(nextId, 0) < CurrentBet)
            {
                ordered.Add(nextId);
            }
        }

        PlayersToAct = ordered;
        CurrentPlayerIndex = ordered.Count > 0 ? 0 : -1;
    }
}
