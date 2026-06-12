using Godot;
using System.Collections.Generic;

public partial class Deck : RefCounted
{
    private readonly List<Card> _cards = new();
    private readonly RandomNumberGenerator _rng = new();

    public int Count => _cards.Count;

    public Deck()
    {
        Initialize();
    }

    public void Initialize()
    {
        _cards.Clear();
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                _cards.Add(new Card { Suit = suit, Rank = rank });
            }
        }
    }

    public void Shuffle()
    {
        _rng.Randomize();
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = _rng.RandiRange(0, i);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card? Deal()
    {
        if (_cards.Count == 0)
        {
            return null;
        }

        var card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public void Reset()
    {
        Initialize();
        Shuffle();
    }
}
