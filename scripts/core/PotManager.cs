using System.Collections.Generic;
using System.Linq;

public class SidePot
{
    public int Amount { get; set; }
    public List<int> EligiblePlayers { get; set; } = new();
}

public class PotAward
{
    public int PotIndex { get; set; }
    public int Amount { get; set; }
    public List<int> EligiblePlayers { get; set; } = new();
    public List<int> Winners { get; set; } = new();
    public Dictionary<int, int> Shares { get; set; } = new();
}

public class PotManager
{
    public int MainPot { get; private set; }
    public List<SidePot> SidePots { get; } = new();

    public void Reset()
    {
        MainPot = 0;
        SidePots.Clear();
    }

    public void CollectBets(Dictionary<int, int> playerBets)
    {
        Reset();
        var positiveBets = playerBets
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Value)
            .ToArray();

        if (positiveBets.Length == 0)
        {
            return;
        }

        var previousLevel = 0;
        var firstPot = true;
        foreach (var level in positiveBets.Select(pair => pair.Value).Distinct().OrderBy(value => value))
        {
            var eligiblePlayers = positiveBets
                .Where(pair => pair.Value >= level)
                .Select(pair => pair.Key)
                .OrderBy(id => id)
                .ToList();

            var amount = (level - previousLevel) * eligiblePlayers.Count;
            if (amount > 0)
            {
                if (firstPot)
                {
                    MainPot = amount;
                    firstPot = false;
                }
                else
                {
                    SidePots.Add(new SidePot { Amount = amount, EligiblePlayers = eligiblePlayers });
                }
            }

            previousLevel = level;
        }
    }

    public Dictionary<int, int> AwardPots(List<List<int>> winnersByPot)
    {
        var winnings = new Dictionary<int, int>();
        foreach (var award in BuildPotAwards(winnersByPot))
        {
            foreach (var share in award.Shares)
            {
                winnings.TryAdd(share.Key, 0);
                winnings[share.Key] += share.Value;
            }
        }

        return winnings;
    }

    public List<PotAward> BuildPotAwards(List<List<int>> winnersByPot)
    {
        var awards = new List<PotAward>();
        AddPotAward(awards, 0, MainPot, new List<int>(), winnersByPot.Count > 0 ? winnersByPot[0] : new List<int>());

        for (var i = 0; i < SidePots.Count; i++)
        {
            var winners = i + 1 < winnersByPot.Count ? winnersByPot[i + 1] : new List<int>();
            AddPotAward(awards, i + 1, SidePots[i].Amount, SidePots[i].EligiblePlayers, winners);
        }

        return awards;
    }

    private static void AddPotAward(List<PotAward> awards, int potIndex, int amount, List<int> eligiblePlayers, List<int> winners)
    {
        if (amount <= 0 || winners.Count == 0)
        {
            return;
        }

        var orderedWinners = winners.OrderBy(id => id).ToArray();
        var share = amount / orderedWinners.Length;
        var remainder = amount % orderedWinners.Length;
        var shares = new Dictionary<int, int>();

        foreach (var winner in orderedWinners)
        {
            shares[winner] = share;
        }

        for (var i = 0; i < remainder; i++)
        {
            shares[orderedWinners[i]] += 1;
        }

        awards.Add(new PotAward
        {
            PotIndex = potIndex,
            Amount = amount,
            EligiblePlayers = eligiblePlayers.ToList(),
            Winners = orderedWinners.ToList(),
            Shares = shares
        });
    }
}
