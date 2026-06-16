using Godot;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static string ToDisplayName(this GameState state)
    {
        return state switch
        {
            GameState.PreFlop => "翻牌前",
            GameState.Flop => "翻牌",
            GameState.Turn => "转牌",
            GameState.River => "河牌",
            GameState.Showdown => "摊牌",
            GameState.GameOver => "游戏结束",
            _ => state.ToString()
        };
    }

    public static string ToDisplayName(this PlayerAction action)
    {
        return action switch
        {
            PlayerAction.Fold => "弃牌",
            PlayerAction.Check => "过牌",
            PlayerAction.Call => "跟注",
            PlayerAction.Bet => "下注",
            PlayerAction.Raise => "加注",
            PlayerAction.AllIn => "全下",
            _ => action.ToString()
        };
    }

    public static string ToDisplayName(this HandCategory category)
    {
        return category switch
        {
            HandCategory.RoyalFlush => "皇家同花顺",
            HandCategory.StraightFlush => "同花顺",
            HandCategory.FourOfAKind => "四条",
            HandCategory.FullHouse => "葫芦",
            HandCategory.Flush => "同花",
            HandCategory.Straight => "顺子",
            HandCategory.ThreeOfAKind => "三条",
            HandCategory.TwoPair => "两对",
            HandCategory.OnePair => "一对",
            HandCategory.HighCard => "高牌",
            _ => category.ToString()
        };
    }

    public static string ToDetailedDisplayName(this HandRank rank)
    {
        var kickers = rank.Kickers;
        return rank.Category switch
        {
            HandCategory.RoyalFlush => "皇家同花顺",
            HandCategory.StraightFlush => $"同花顺{RankName(kickers.FirstOrDefault())}",
            HandCategory.FourOfAKind => $"{RankName(kickers.FirstOrDefault())}四条",
            HandCategory.FullHouse => $"{RankName(kickers.ElementAtOrDefault(0))}{RankName(kickers.ElementAtOrDefault(1))}葫芦",
            HandCategory.Flush => $"{RankName(kickers.FirstOrDefault())}同花",
            HandCategory.Straight => $"顺子{RankName(kickers.FirstOrDefault())}",
            HandCategory.ThreeOfAKind => $"{RankName(kickers.FirstOrDefault())}三条",
            HandCategory.TwoPair => $"{RankName(kickers.ElementAtOrDefault(0))}{RankName(kickers.ElementAtOrDefault(1))}两对",
            HandCategory.OnePair => $"{RankName(kickers.FirstOrDefault())}对子",
            HandCategory.HighCard => $"{RankName(kickers.FirstOrDefault())}高牌",
            _ => rank.Category.ToDisplayName()
        };
    }

    private static string RankName(int value)
    {
        return value switch
        {
            14 => "A",
            13 => "K",
            12 => "Q",
            11 => "J",
            10 => "10",
            >= 2 and <= 9 => value.ToString(),
            _ => ""
        };
    }

    public static Godot.Collections.Array<int> ToGodotArray(this IEnumerable<int> values)
    {
        var array = new Godot.Collections.Array<int>();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static string JoinCards(this IEnumerable<Card?> cards)
    {
        return string.Join(" ", cards.Where(card => card != null).Select(card => card!.ShortName));
    }
}
