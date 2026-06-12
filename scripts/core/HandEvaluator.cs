using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct HandRank : IComparable<HandRank>
{
    public HandCategory Category { get; }
    public int[] Kickers { get; }

    public HandRank(HandCategory category, IEnumerable<int> kickers)
    {
        Category = category;
        Kickers = kickers.ToArray();
    }

    public int CompareTo(HandRank other)
    {
        if (Category != other.Category)
        {
            return (int)other.Category - (int)Category;
        }

        var count = Math.Max(Kickers.Length, other.Kickers.Length);
        for (var i = 0; i < count; i++)
        {
            var a = i < Kickers.Length ? Kickers[i] : 0;
            var b = i < other.Kickers.Length ? other.Kickers[i] : 0;
            if (a != b)
            {
                return a - b;
            }
        }

        return 0;
    }

    public override string ToString()
    {
        return $"{Category} [{string.Join(",", Kickers)}]";
    }
}

public static class HandEvaluator
{
    public static HandRank EvaluateHand(Card[] holeCards, Card[] communityCards)
    {
        if (holeCards.Length != 2)
        {
            throw new ArgumentException("holeCards must contain exactly 2 cards.");
        }

        if (communityCards.Length < 3 || communityCards.Length > 5)
        {
            throw new ArgumentException("communityCards must contain between 3 and 5 cards.");
        }

        var cards = holeCards.Concat(communityCards).Where(card => card != null).ToArray();
        if (cards.Length < 5)
        {
            throw new ArgumentException("At least 5 cards are required.");
        }

        HandRank? best = null;
        for (var a = 0; a < cards.Length - 4; a++)
        for (var b = a + 1; b < cards.Length - 3; b++)
        for (var c = b + 1; c < cards.Length - 2; c++)
        for (var d = c + 1; d < cards.Length - 1; d++)
        for (var e = d + 1; e < cards.Length; e++)
        {
            var rank = EvaluateFive(new[] { cards[a], cards[b], cards[c], cards[d], cards[e] });
            if (best == null || rank.CompareTo(best.Value) > 0)
            {
                best = rank;
            }
        }

        return best ?? new HandRank(HandCategory.HighCard, Array.Empty<int>());
    }

    private static HandRank EvaluateFive(Card[] cards)
    {
        var values = cards.Select(card => card.Value).OrderByDescending(value => value).ToArray();
        var groups = values
            .GroupBy(value => value)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.Value)
            .ToArray();

        var isFlush = cards.All(card => card.Suit == cards[0].Suit);
        var straightHigh = GetStraightHigh(values);

        if (isFlush && straightHigh == 14)
        {
            return new HandRank(HandCategory.RoyalFlush, new[] { 14 });
        }

        if (isFlush && straightHigh > 0)
        {
            return new HandRank(HandCategory.StraightFlush, new[] { straightHigh });
        }

        var four = groups.FirstOrDefault(group => group.Count == 4);
        if (four != null)
        {
            var kicker = values.First(value => value != four.Value);
            return new HandRank(HandCategory.FourOfAKind, new[] { four.Value, kicker });
        }

        var three = groups.FirstOrDefault(group => group.Count == 3);
        var pairForFullHouse = groups.FirstOrDefault(group => group.Count == 2);
        if (three != null && pairForFullHouse != null)
        {
            return new HandRank(HandCategory.FullHouse, new[] { three.Value, pairForFullHouse.Value });
        }

        if (isFlush)
        {
            return new HandRank(HandCategory.Flush, values);
        }

        if (straightHigh > 0)
        {
            return new HandRank(HandCategory.Straight, new[] { straightHigh });
        }

        if (three != null)
        {
            var kickers = values.Where(value => value != three.Value).Take(2);
            return new HandRank(HandCategory.ThreeOfAKind, new[] { three.Value }.Concat(kickers));
        }

        var pairs = groups.Where(group => group.Count == 2).OrderByDescending(group => group.Value).ToArray();
        if (pairs.Length >= 2)
        {
            var highPair = pairs[0].Value;
            var lowPair = pairs[1].Value;
            var kicker = values.First(value => value != highPair && value != lowPair);
            return new HandRank(HandCategory.TwoPair, new[] { highPair, lowPair, kicker });
        }

        if (pairs.Length == 1)
        {
            var pair = pairs[0].Value;
            var kickers = values.Where(value => value != pair).Take(3);
            return new HandRank(HandCategory.OnePair, new[] { pair }.Concat(kickers));
        }

        return new HandRank(HandCategory.HighCard, values.Take(5));
    }

    private static int GetStraightHigh(IEnumerable<int> values)
    {
        var unique = values.Distinct().OrderByDescending(value => value).ToList();
        if (unique.Contains(14))
        {
            unique.Add(1);
        }

        for (var i = 0; i <= unique.Count - 5; i++)
        {
            var window = unique.Skip(i).Take(5).ToArray();
            if (window[0] - window[4] == 4 && window.Distinct().Count() == 5)
            {
                return window[0] == 14 && window[1] == 5 ? 5 : window[0];
            }
        }

        return 0;
    }
}
