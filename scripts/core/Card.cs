using Godot;

public partial class Card : Resource
{
    [Export] public Suit Suit { get; set; }
    [Export] public Rank Rank { get; set; }

    public int Value => (int)Rank;

    public string ShortName => $"{RankToShortName(Rank)}{SuitToSymbol(Suit)}";

    public static string RankToShortName(Rank rank)
    {
        return rank switch
        {
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)rank).ToString()
        };
    }

    public static string SuitToSymbol(Suit suit)
    {
        return suit switch
        {
            Suit.Hearts => "H",
            Suit.Diamonds => "D",
            Suit.Clubs => "C",
            Suit.Spades => "S",
            _ => "?"
        };
    }

    public override string ToString()
    {
        return ShortName;
    }
}
