using Godot;
using System.Collections.Generic;

public static class CardIconCache
{
    private const string SourceDir = "res://assets/textures/cards";
    private const string IconDir = "res://assets/textures/card_icons";
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static bool _generated;

    public static Texture2D? GetIcon(Card? card)
    {
        if (card == null)
        {
            return null;
        }

        EnsureGenerated();
        var key = card.ShortName;
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var path = GetIconPath(card);
        var loaded = ResourceLoader.Load<Texture2D>(path);
        if (loaded != null)
        {
            Cache[key] = loaded;
            return loaded;
        }

        var image = new Image();
        if (image.Load(path) != Error.Ok)
        {
            image = CropIconImage(card);
        }

        if (image == null)
        {
            return null;
        }

        var texture = ImageTexture.CreateFromImage(image);
        Cache[key] = texture;
        return texture;
    }

    public static Texture2D? GetCombinedIcon(Card? firstCard, Card? secondCard)
    {
        var first = GetIcon(firstCard);
        if (first == null || secondCard == null)
        {
            return first;
        }

        var second = GetIcon(secondCard);
        if (second == null)
        {
            return first;
        }

        var image = Image.CreateEmpty(86, 42, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));
        BlitScaled(first, image, new Rect2I(0, 0, 42, 42));
        BlitScaled(second, image, new Rect2I(44, 0, 42, 42));
        return ImageTexture.CreateFromImage(image);
    }

    private static void EnsureGenerated()
    {
        if (_generated)
        {
            return;
        }

        _generated = true;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(IconDir));
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                var card = new Card { Suit = suit, Rank = rank };
                var path = GetIconPath(card);
                if (Godot.FileAccess.FileExists(path))
                {
                    continue;
                }

                var image = CropIconImage(card);
                image?.SavePng(ProjectSettings.GlobalizePath(path));
            }
        }
    }

    private static Image? CropIconImage(Card card)
    {
        var sourcePath = $"{SourceDir}/card{SuitName(card.Suit)}{RankName(card.Rank)}.png";
        var source = new Image();
        if (source.Load(sourcePath) != Error.Ok)
        {
            return null;
        }

        var crop = GetCornerCrop(source.GetWidth(), source.GetHeight());
        var icon = Image.CreateEmpty(crop.Size.X, crop.Size.Y, false, Image.Format.Rgba8);
        icon.Fill(new Color(0, 0, 0, 0));
        icon.BlitRect(source, crop, Vector2I.Zero);
        icon.Resize(42, 42, Image.Interpolation.Lanczos);
        return icon;
    }

    private static Rect2I GetCornerCrop(int width, int height)
    {
        var cropWidth = Mathf.RoundToInt(width * 0.34f);
        var cropHeight = Mathf.RoundToInt(height * 0.34f);
        return new Rect2I(0, 0, cropWidth, cropHeight);
    }

    private static void BlitScaled(Texture2D texture, Image target, Rect2I rect)
    {
        var image = texture.GetImage();
        image.Resize(rect.Size.X, rect.Size.Y, Image.Interpolation.Bilinear);
        target.BlitRect(image, new Rect2I(Vector2I.Zero, rect.Size), rect.Position);
    }

    private static string GetIconPath(Card card)
    {
        return $"{IconDir}/card{SuitName(card.Suit)}{RankName(card.Rank)}_icon.png";
    }

    private static string SuitName(Suit suit)
    {
        return suit switch
        {
            Suit.Hearts => "Hearts",
            Suit.Diamonds => "Diamonds",
            Suit.Clubs => "Clubs",
            Suit.Spades => "Spades",
            _ => "Spades"
        };
    }

    private static string RankName(Rank rank)
    {
        return rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            Rank.Ten => "10",
            _ => ((int)rank).ToString()
        };
    }
}
