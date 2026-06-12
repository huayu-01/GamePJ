public enum Suit
{
    Hearts = 0,
    Diamonds = 1,
    Clubs = 2,
    Spades = 3
}

public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public enum GameState
{
    Menu,
    Lobby,
    PreFlop,
    Flop,
    Turn,
    River,
    Showdown,
    GameOver
}

public enum PlayerAction
{
    Fold,
    Check,
    Call,
    Bet,
    Raise,
    AllIn
}

public enum HandCategory
{
    RoyalFlush,
    StraightFlush,
    FourOfAKind,
    FullHouse,
    Flush,
    Straight,
    ThreeOfAKind,
    TwoPair,
    OnePair,
    HighCard
}
