using Godot;

public partial class CardDisplay : TextureRect
{
    private Label? _label;
    private Card? _card;

    [Export] public bool FaceUp { get; set; } = true;
    [Export] public string CardText { get; set; } = "";

    public override void _Ready()
    {
        if (CustomMinimumSize == Vector2.Zero)
        {
            CustomMinimumSize = new Vector2(72, 104);
        }

        PivotOffset = CustomMinimumSize / 2f;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;

        _label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = FaceUp ? CardText : "背面"
        };
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_label);
        Refresh();
    }

    public void SetCard(Card? card, bool faceUp = true)
    {
        _card = card;
        FaceUp = faceUp;
        CardText = card?.ShortName ?? "";
        Refresh();
    }

    public void SetBack()
    {
        FaceUp = false;
        CardText = "";
        Refresh();
    }

    public void SetDisplaySize(Vector2 size)
    {
        CustomMinimumSize = size;
        Size = size;
        PivotOffset = size / 2f;
        if (_label != null)
        {
            _label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(Mathf.Clamp(size.X * 0.28f, 14f, 32f)));
        }
    }

    public Tween FlipTo(Card? card)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "scale:x", 0.0, 0.15);
        tween.TweenCallback(Callable.From(() => SetCard(card, true)));
        tween.TweenProperty(this, "scale:x", 1.0, 0.15);
        return tween;
    }

    private void Refresh()
    {
        if (_label == null)
        {
            return;
        }

        _label.Text = "";
        Texture = FaceUp && _card != null
            ? LoadCardTexture(_card) ?? CreateCardTexture(new Color(0.95f, 0.94f, 0.88f), new Color(0.12f, 0.13f, 0.15f))
            : LoadTexture("res://assets/textures/cards/cardBack_blue2.png") ??
              CreateCardTexture(new Color(0.12f, 0.22f, 0.42f), new Color(0.78f, 0.86f, 1f));

        if (FaceUp && _card == null)
        {
            _label.Text = "--";
        }
    }

    private static Texture2D? LoadCardTexture(Card card)
    {
        return LoadTexture($"res://assets/textures/cards/card{SuitName(card.Suit)}{RankName(card.Rank)}.png");
    }

    private static Texture2D? LoadTexture(string path)
    {
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
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

    private static ImageTexture CreateCardTexture(Color fill, Color border)
    {
        const int width = 96;
        const int height = 136;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(fill);
        for (var x = 0; x < width; x++)
        {
            image.SetPixel(x, 0, border);
            image.SetPixel(x, height - 1, border);
        }
        for (var y = 0; y < height; y++)
        {
            image.SetPixel(0, y, border);
            image.SetPixel(width - 1, y, border);
        }

        return ImageTexture.CreateFromImage(image);
    }
}
