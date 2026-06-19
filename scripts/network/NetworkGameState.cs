using Godot;
using System.Collections.Generic;

public class CardDTO
{
    public int Suit { get; set; }
    public int Rank { get; set; }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary { ["suit"] = Suit, ["rank"] = Rank };
    }

    public static CardDTO FromCard(Card card)
    {
        return new CardDTO { Suit = (int)card.Suit, Rank = (int)card.Rank };
    }

    public Card ToCard()
    {
        return new Card { Suit = (Suit)Suit, Rank = (Rank)Rank };
    }

    public static CardDTO FromDictionary(Godot.Collections.Dictionary dict)
    {
        return new CardDTO
        {
            Suit = dict.GetValueOrDefault("suit", 0).AsInt32(),
            Rank = dict.GetValueOrDefault("rank", 2).AsInt32()
        };
    }
}

public class PlayerDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Chips { get; set; }
    public int CurrentBet { get; set; }
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public int Position { get; set; }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["id"] = Id,
            ["name"] = Name,
            ["chips"] = Chips,
            ["current_bet"] = CurrentBet,
            ["is_folded"] = IsFolded,
            ["is_all_in"] = IsAllIn,
            ["position"] = Position
        };
    }

    public static PlayerDTO FromPlayer(Player player)
    {
        return new PlayerDTO
        {
            Id = player.Id,
            Name = player.Name,
            Chips = player.Chips,
            CurrentBet = player.CurrentBet,
            IsFolded = player.IsFolded,
            IsAllIn = player.IsAllIn,
            Position = player.Position
        };
    }

    public static PlayerDTO FromDictionary(Godot.Collections.Dictionary dict)
    {
        return new PlayerDTO
        {
            Id = dict.GetValueOrDefault("id", 0).AsInt32(),
            Name = dict.GetValueOrDefault("name", "").AsString(),
            Chips = dict.GetValueOrDefault("chips", 0).AsInt32(),
            CurrentBet = dict.GetValueOrDefault("current_bet", 0).AsInt32(),
            IsFolded = dict.GetValueOrDefault("is_folded", false).AsBool(),
            IsAllIn = dict.GetValueOrDefault("is_all_in", false).AsBool(),
            Position = dict.GetValueOrDefault("position", 0).AsInt32()
        };
    }
}

public class SidePotDTO
{
    public int Amount { get; set; }
    public List<int> EligiblePlayers { get; set; } = new();

    public Godot.Collections.Dictionary ToDictionary()
    {
        var players = new Godot.Collections.Array<int>();
        foreach (var id in EligiblePlayers)
        {
            players.Add(id);
        }

        return new Godot.Collections.Dictionary { ["amount"] = Amount, ["eligible_players"] = players };
    }

    public static SidePotDTO FromDictionary(Godot.Collections.Dictionary dict)
    {
        var dto = new SidePotDTO
        {
            Amount = dict.GetValueOrDefault("amount", 0).AsInt32()
        };

        foreach (var item in dict.GetValueOrDefault("eligible_players", new Godot.Collections.Array()).AsGodotArray())
        {
            dto.EligiblePlayers.Add(item.AsInt32());
        }

        return dto;
    }
}

public class GameStateDTO
{
    public GameState CurrentState { get; set; }
    public List<PlayerDTO> Players { get; set; } = new();
    public List<CardDTO> CommunityCards { get; set; } = new();
    public int MainPot { get; set; }
    public List<SidePotDTO> SidePots { get; set; } = new();
    public int CurrentBet { get; set; }
    public int CurrentPlayerId { get; set; }
    public int DealerPosition { get; set; }
    public int TableSeatCount { get; set; } = 9;
    public int SmallBlindAmount { get; set; } = Constants.SmallBlind;
    public int BigBlindAmount { get; set; } = Constants.BigBlind;
    public int MinBuyIn { get; set; } = Constants.MinBuyIn;
    public int MaxBuyIn { get; set; } = Constants.MaxBuyIn;
    public int TableChipLimit { get; set; } = Constants.TableChipLimit;
    public int ThinkingTimeSeconds { get; set; } = Constants.ThinkingTimeSeconds;
    public int TurnTimeRemainingSeconds { get; set; }
    public int HandNumber { get; set; }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var players = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var player in Players)
        {
            players.Add(player.ToDictionary());
        }

        var community = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var card in CommunityCards)
        {
            community.Add(card.ToDictionary());
        }

        var sidePots = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var sidePot in SidePots)
        {
            sidePots.Add(sidePot.ToDictionary());
        }

        return new Godot.Collections.Dictionary
        {
            ["current_state"] = (int)CurrentState,
            ["players"] = players,
            ["community_cards"] = community,
            ["main_pot"] = MainPot,
            ["side_pots"] = sidePots,
            ["current_bet"] = CurrentBet,
            ["current_player_id"] = CurrentPlayerId,
            ["dealer_position"] = DealerPosition,
            ["table_seat_count"] = TableSeatCount,
            ["small_blind_amount"] = SmallBlindAmount,
            ["big_blind_amount"] = BigBlindAmount,
            ["min_buy_in"] = MinBuyIn,
            ["max_buy_in"] = MaxBuyIn,
            ["table_chip_limit"] = TableChipLimit,
            ["thinking_time_seconds"] = ThinkingTimeSeconds,
            ["turn_time_remaining_seconds"] = TurnTimeRemainingSeconds,
            ["hand_number"] = HandNumber
        };
    }

    public static GameStateDTO FromDictionary(Godot.Collections.Dictionary dict)
    {
        var dto = new GameStateDTO
        {
            CurrentState = (GameState)dict.GetValueOrDefault("current_state", 0).AsInt32(),
            MainPot = dict.GetValueOrDefault("main_pot", 0).AsInt32(),
            CurrentBet = dict.GetValueOrDefault("current_bet", 0).AsInt32(),
            CurrentPlayerId = dict.GetValueOrDefault("current_player_id", -1).AsInt32(),
            DealerPosition = dict.GetValueOrDefault("dealer_position", 0).AsInt32(),
            TableSeatCount = dict.GetValueOrDefault("table_seat_count", 9).AsInt32(),
            SmallBlindAmount = dict.GetValueOrDefault("small_blind_amount", Constants.SmallBlind).AsInt32(),
            BigBlindAmount = dict.GetValueOrDefault("big_blind_amount", Constants.BigBlind).AsInt32(),
            MinBuyIn = dict.GetValueOrDefault("min_buy_in", Constants.MinBuyIn).AsInt32(),
            MaxBuyIn = dict.GetValueOrDefault("max_buy_in", Constants.MaxBuyIn).AsInt32(),
            TableChipLimit = dict.GetValueOrDefault("table_chip_limit", Constants.TableChipLimit).AsInt32(),
            ThinkingTimeSeconds = dict.GetValueOrDefault("thinking_time_seconds", Constants.ThinkingTimeSeconds).AsInt32(),
            TurnTimeRemainingSeconds = dict.GetValueOrDefault("turn_time_remaining_seconds", 0).AsInt32(),
            HandNumber = dict.GetValueOrDefault("hand_number", 0).AsInt32()
        };

        foreach (var item in dict.GetValueOrDefault("players", new Godot.Collections.Array()).AsGodotArray())
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                dto.Players.Add(PlayerDTO.FromDictionary(item.AsGodotDictionary()));
            }
        }

        foreach (var item in dict.GetValueOrDefault("community_cards", new Godot.Collections.Array()).AsGodotArray())
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                dto.CommunityCards.Add(CardDTO.FromDictionary(item.AsGodotDictionary()));
            }
        }

        foreach (var item in dict.GetValueOrDefault("side_pots", new Godot.Collections.Array()).AsGodotArray())
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                dto.SidePots.Add(SidePotDTO.FromDictionary(item.AsGodotDictionary()));
            }
        }

        return dto;
    }
}
