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
