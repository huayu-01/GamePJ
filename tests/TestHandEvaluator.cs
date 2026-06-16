using Godot;
using System.Linq;

public partial class TestHandEvaluator : Node
{
    public override void _Ready()
    {
        var (pass, fail) = Run();
        GD.Print("\n========== TEST RESULT ==========");
        GD.Print($"PASS: {pass}/17, FAIL: {fail}/17");
        GD.Print(fail == 0 ? "ALL TESTS PASSED!" : "SOME TESTS FAILED - FIX BEFORE PROCEEDING");
    }

    public static (int Pass, int Fail) Run()
    {
        int pass = 0, fail = 0;

        var result1 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ace), C(Suit.Spades, Rank.King) },
            new[] { C(Suit.Spades, Rank.Queen), C(Suit.Spades, Rank.Jack), C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Three) });
        Check(result1.Category == HandCategory.RoyalFlush, "RoyalFlush", ref pass, ref fail);

        var result2 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Hearts, Rank.Nine), C(Suit.Hearts, Rank.Eight) },
            new[] { C(Suit.Hearts, Rank.Seven), C(Suit.Hearts, Rank.Six), C(Suit.Hearts, Rank.Five), C(Suit.Spades, Rank.Ace), C(Suit.Diamonds, Rank.King) });
        Check(result2.Category == HandCategory.StraightFlush && result2.Kickers[0] == 9, "StraightFlush", ref pass, ref fail);

        var result3 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King) },
            new[] { C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        Check(result3.Category == HandCategory.FourOfAKind && result3.Kickers[0] == 13 && result3.Kickers[1] == 4, "FourOfAKind", ref pass, ref fail);

        var result4 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Queen), C(Suit.Hearts, Rank.Queen) },
            new[] { C(Suit.Diamonds, Rank.Queen), C(Suit.Clubs, Rank.Jack), C(Suit.Spades, Rank.Jack), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King) });
        Check(result4.Category == HandCategory.FullHouse && result4.Kickers[0] == 12 && result4.Kickers[1] == 11, "FullHouse", ref pass, ref fail);

        var result5 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.King) },
            new[] { C(Suit.Hearts, Rank.Seven), C(Suit.Hearts, Rank.Four), C(Suit.Hearts, Rank.Two), C(Suit.Spades, Rank.Three), C(Suit.Diamonds, Rank.Five) });
        Check(result5.Category == HandCategory.Flush && result5.Kickers.SequenceEqual(new[] { 14, 13, 7, 4, 2 }), "Flush", ref pass, ref fail);

        var result6 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Nine), C(Suit.Diamonds, Rank.Eight) },
            new[] { C(Suit.Hearts, Rank.Seven), C(Suit.Clubs, Rank.Six), C(Suit.Spades, Rank.Five), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King) });
        Check(result6.Category == HandCategory.Straight && result6.Kickers[0] == 9, "Straight", ref pass, ref fail);

        var result7 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten) },
            new[] { C(Suit.Diamonds, Rank.Ten), C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        Check(result7.Category == HandCategory.ThreeOfAKind && result7.Kickers[0] == 10 && result7.Kickers[1] == 13, "ThreeOfAKind", ref pass, ref fail);

        var result8 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace) },
            new[] { C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        Check(result8.Category == HandCategory.TwoPair && result8.Kickers[0] == 14 && result8.Kickers[1] == 13 && result8.Kickers[2] == 4, "TwoPair", ref pass, ref fail);

        var result9 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace) },
            new[] { C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Queen), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        Check(result9.Category == HandCategory.OnePair && result9.Kickers.SequenceEqual(new[] { 14, 13, 12, 4 }), "OnePair", ref pass, ref fail);

        var result10 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ace), C(Suit.Diamonds, Rank.King) },
            new[] { C(Suit.Hearts, Rank.Queen), C(Suit.Clubs, Rank.Seven), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        Check(result10.Category == HandCategory.HighCard && result10.Kickers.SequenceEqual(new[] { 14, 13, 12, 7, 4 }), "HighCard", ref pass, ref fail);

        var handA = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace) },
            new[] { C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Queen), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Diamonds, Rank.Four) });
        var handB = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.Ace) },
            new[] { C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Jack), C(Suit.Diamonds, Rank.Two), C(Suit.Clubs, Rank.Three), C(Suit.Spades, Rank.Four) });
        Check(handA.CompareTo(handB) > 0, "TieBreaker", ref pass, ref fail);

        var result12 = HandEvaluator.EvaluateHand(
            new[] { C(Suit.Hearts, Rank.Ace), C(Suit.Spades, Rank.Two) },
            new[] { C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Four), C(Suit.Hearts, Rank.Five), C(Suit.Spades, Rank.Seven), C(Suit.Diamonds, Rank.Eight) });
        Check(result12.Category == HandCategory.Straight && result12.Kickers[0] == 5, "StraightAceLow", ref pass, ref fail);
        Check(result4.ToDetailedDisplayName() == "QJ葫芦", "FullHouseDetailedName", ref pass, ref fail);
        Check(result5.ToDetailedDisplayName() == "A同花", "FlushDetailedName", ref pass, ref fail);
        Check(result6.ToDetailedDisplayName() == "顺子9", "StraightDetailedName", ref pass, ref fail);
        Check(result8.ToDetailedDisplayName() == "AK两对", "TwoPairDetailedName", ref pass, ref fail);
        Check(result9.ToDetailedDisplayName() == "A对子", "OnePairDetailedName", ref pass, ref fail);

        return (pass, fail);
    }

    private static Card C(Suit suit, Rank rank)
    {
        return new Card { Suit = suit, Rank = rank };
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
