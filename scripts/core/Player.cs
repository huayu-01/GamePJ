using Godot;

public partial class Player : RefCounted
{
    public int Id { get; set; }
    public string Name { get; set; } = "Player";
    public int Chips { get; set; } = 1000;
    public Card[] HoleCards { get; set; } = new Card[2];
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public bool IsSittingOut { get; set; }
    public bool WantsSitOutNextHand { get; set; }
    public int CurrentBet { get; set; }
    public int Position { get; set; }
    public int AccountBalance { get; set; }

    public void ResetForHand()
    {
        HoleCards = new Card[2];
        IsFolded = false;
        IsAllIn = false;
        CurrentBet = 0;
    }
}
