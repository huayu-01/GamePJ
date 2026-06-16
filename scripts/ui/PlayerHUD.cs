using Godot;

public partial class PlayerHUD : Control
{
    private Label? _nameLabel;
    private Label? _chipsLabel;
    private Label? _accountLabel;
    private Label? _betLabel;
    private Label? _statusLabel;
    private Label? _handLabel;
    private Label? _blindLabel;
    private Panel? _panel;
    private Control? _cardsLayer;
    private HBoxContainer? _revealActions;
    private Button? _revealFirstButton;
    private Button? _revealSecondButton;
    private Button? _revealBothButton;
    private CardDisplay? _card1;
    private CardDisplay? _card2;

    private int _lastPlayerId = -1;
    private string _lastCard1Key = "";
    private string _lastCard2Key = "";
    private bool _lastRevealCards;
    private bool _lastCard1FaceUp;
    private bool _lastCard2FaceUp;
    private bool _lastTurnActive;
    private bool _lastWinner;
    private bool _lastFolded;
    private bool _isLocal;
    private bool _isEmptySeat;
    private Vector2 _cardSize = new(72, 103);

    public int PlayerId => _lastPlayerId;

    public override void _Ready()
    {
        BuildUi();
    }

    public void ConfigureForStage(float stageWidth, bool localPlayer)
    {
        _isLocal = localPlayer;
        var cardWidth = localPlayer
            ? Mathf.Clamp(stageWidth * 0.18f, 112f, 206f)
            : Mathf.Clamp(stageWidth * 0.096f, 58f, 104f);
        _cardSize = new Vector2(cardWidth, cardWidth * 1.43f);
        var cardGap = localPlayer ? Mathf.Clamp(stageWidth * 0.018f, 10f, 18f) : -cardWidth * 0.34f;
        var cardsWidth = localPlayer ? cardWidth * 2f + cardGap : cardWidth * 2f + cardGap;
        var topTextHeight = localPlayer ? Mathf.Clamp(stageWidth * 0.084f, 68f, 96f) : Mathf.Clamp(stageWidth * 0.066f, 45f, 68f);
        var bottomTextHeight = localPlayer ? Mathf.Clamp(stageWidth * 0.060f, 42f, 72f) : Mathf.Clamp(stageWidth * 0.048f, 34f, 54f);

        Size = new Vector2(
            Mathf.Ceil(Mathf.Max(cardsWidth + 16f, localPlayer ? stageWidth * 0.48f : stageWidth * 0.20f)),
            Mathf.Ceil(topTextHeight + _cardSize.Y + bottomTextHeight));
        CustomMinimumSize = Size;

        var labelSize = localPlayer
            ? Mathf.RoundToInt(Mathf.Clamp(stageWidth * 0.034f, 22f, 34f))
            : Mathf.RoundToInt(Mathf.Clamp(stageWidth * 0.024f, 14f, 22f));
        ApplyLabelSize(_nameLabel, labelSize + 1);
        ApplyLabelSize(_chipsLabel, labelSize);
        ApplyLabelSize(_accountLabel, Mathf.Max(11, labelSize - 2));
        ApplyLabelSize(_blindLabel, Mathf.Max(12, labelSize - 1));
        ApplyLabelSize(_betLabel, labelSize);
        ApplyLabelSize(_handLabel, labelSize);
        ApplyLabelSize(_statusLabel, labelSize);

        _card1?.SetDisplaySize(_cardSize);
        _card2?.SetDisplaySize(_cardSize);
        LayoutContents();
    }

    public void SetEmptySeat(int seatNumber)
    {
        if (_nameLabel == null)
        {
            BuildUi();
        }

        _isEmptySeat = true;
        Visible = true;
        _lastPlayerId = -1;
        _nameLabel!.Text = $"空座 {seatNumber}";
        _chipsLabel!.Text = "";
        _accountLabel!.Text = "";
        _betLabel!.Text = "";
        _statusLabel!.Text = "";
        _handLabel!.Text = "";
        _blindLabel!.Visible = false;
        _card1!.SetCard(null, false);
        _card2!.SetCard(null, false);
        if (_cardsLayer != null)
        {
            _cardsLayer.Visible = false;
        }
        Modulate = new Color(1, 1, 1, 0.34f);
        MouseFilter = MouseFilterEnum.Ignore;
        LayoutContents();
    }

    public void SetPlayer(Player player, bool revealCards, bool revealFirstCard = false, bool revealSecondCard = false)
    {
        if (_nameLabel == null)
        {
            BuildUi();
        }

        _isEmptySeat = false;
        Visible = true;
        Modulate = Colors.White;
        if (_cardsLayer != null)
        {
            _cardsLayer.Visible = true;
        }
        MouseFilter = MouseFilterEnum.Stop;
        _lastPlayerId = player.Id;
        _nameLabel!.Text = player.Name;
        _chipsLabel!.Text = $"筹码: {player.Chips}";
        _accountLabel!.Text = "";
        _betLabel!.Text = player.CurrentBet > 0 ? $"下注: {player.CurrentBet}" : "";
        _statusLabel!.Text = GetStatusText(player);
        var rank = GameManager.Instance?.GetVisibleHandRank(player.Id);
        _handLabel!.Text = rank.HasValue && revealCards ? rank.Value.Category.ToDisplayName() : "";

        var showFirstCard = revealCards || revealFirstCard;
        var showSecondCard = revealCards || revealSecondCard;
        var card1Key = player.HoleCards[0]?.ShortName ?? "";
        var card2Key = player.HoleCards[1]?.ShortName ?? "";
        if (player.IsSittingOut)
        {
            _card1!.SetCard(null, false);
            _card2!.SetCard(null, false);
            SetFoldedVisual(true);
        }
        else
        {
            SetCardVisibility(_card1!, player.HoleCards[0], showFirstCard, _lastCard1FaceUp, _lastCard1Key != card1Key);
            SetCardVisibility(_card2!, player.HoleCards[1], showSecondCard, _lastCard2FaceUp, _lastCard2Key != card2Key);
            SetFoldedVisual(player.IsFolded && !showFirstCard && !showSecondCard);
        }

        _lastCard1Key = card1Key;
        _lastCard2Key = card2Key;
        _lastRevealCards = showFirstCard && showSecondCard;
        _lastCard1FaceUp = showFirstCard;
        _lastCard2FaceUp = showSecondCard;
        _lastFolded = player.IsFolded || player.IsSittingOut;
        LayoutContents();
    }

    private static void SetCardVisibility(CardDisplay display, Card? card, bool faceUp, bool wasFaceUp, bool cardChanged)
    {
        if (!faceUp)
        {
            display.SetBack();
            return;
        }

        if (!wasFaceUp || cardChanged)
        {
            display.FlipTo(card);
        }
        else
        {
            display.SetCard(card);
        }
    }

    public void SetBlindRole(string role)
    {
        if (_blindLabel == null || _isEmptySeat)
        {
            return;
        }

        _blindLabel.Text = role;
        _blindLabel.Visible = !string.IsNullOrEmpty(role);
        _blindLabel.AddThemeColorOverride("font_color", role == "大盲" ? new Color(0.42f, 0.72f, 1.0f) : new Color(1.0f, 0.78f, 0.32f));
        LayoutContents();
    }

    public void SetTurnActive(bool active)
    {
        if (_panel == null || _isEmptySeat)
        {
            return;
        }

        if (active)
        {
            _statusLabel!.Text = "行动中";
        }

        _statusLabel!.AddThemeColorOverride("font_color", active ? new Color(1.0f, 0.82f, 0.22f) : FlatUi.Accent);
        if (active && !_lastTurnActive)
        {
            var tween = CreateTween();
            tween.SetLoops(2);
            tween.TweenProperty(this, "scale", new Vector2(1.04f, 1.04f), 0.22);
            tween.TweenProperty(this, "scale", Vector2.One, 0.22);
        }

        _lastTurnActive = active;
    }

    public void SetWinnerState(bool winner, int amount)
    {
        if (_panel == null || _isEmptySeat)
        {
            return;
        }

        if (winner)
        {
            _statusLabel!.Text = amount > 0 ? $"+{amount}" : "胜利";
            if (!_lastWinner)
            {
                var tween = CreateTween();
                tween.TweenProperty(this, "position:y", Position.Y - 12, 0.18).SetTrans(Tween.TransitionType.Back);
                tween.TweenProperty(this, "position:y", Position.Y, 0.22).SetTrans(Tween.TransitionType.Bounce);
            }
        }

        _lastWinner = winner;
    }

    public void PlayPotReceived(int amount)
    {
        if (_statusLabel == null || _isEmptySeat)
        {
            return;
        }

        _statusLabel.Text = amount > 0 ? $"+{amount}" : "收池";
        _statusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.22f));

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector2(1.08f, 1.08f), 0.16).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(this, "scale", Vector2.One, 0.22).SetTrans(Tween.TransitionType.Bounce);
    }

    private void BuildUi()
    {
        CustomMinimumSize = new Vector2(220, 190);
        _panel = FlatUi.Panel("HudPanel");
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_panel);

        _blindLabel = CreateCenteredLabel("", 12);
        _nameLabel = CreateCenteredLabel("玩家", 15);
        _chipsLabel = CreateCenteredLabel("筹码: 1000", 13, true);
        _accountLabel = CreateCenteredLabel("", 12, true);
        _betLabel = CreateCenteredLabel("", 13, true);
        _statusLabel = CreateCenteredLabel("", 13);
        _statusLabel.AddThemeColorOverride("font_color", FlatUi.Accent);
        _handLabel = CreateCenteredLabel("", 13);
        _handLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.32f));

        _cardsLayer = new Control { Name = "CardsLayer" };
        AddChild(_cardsLayer);
        _card1 = new CardDisplay { CustomMinimumSize = new Vector2(58, 83) };
        _card2 = new CardDisplay { CustomMinimumSize = new Vector2(58, 83) };
        _cardsLayer.AddChild(_card1);
        _cardsLayer.AddChild(_card2);

        _revealActions = new HBoxContainer
        {
            Name = "RevealActions",
            Alignment = BoxContainer.AlignmentMode.Center,
            Visible = false
        };
        _revealActions.AddThemeConstantOverride("separation", 6);
        AddChild(_revealActions);
        _revealFirstButton = CreateRevealButton(() => RevealCard(0));
        _revealSecondButton = CreateRevealButton(() => RevealCard(1));
        _revealBothButton = CreateRevealButton(RevealBothCards);
        _revealActions.AddChild(_revealFirstButton);
        _revealActions.AddChild(_revealSecondButton);
        _revealActions.AddChild(_revealBothButton);
        LayoutContents();
    }

    public void SetRevealActions(bool canRevealFirst, bool canRevealSecond, Card? firstCard, Card? secondCard)
    {
        if (_revealActions == null)
        {
            BuildUi();
        }

        var canRevealBoth = canRevealFirst && canRevealSecond;
        ConfigureRevealButton(_revealFirstButton, canRevealFirst, firstCard, null);
        ConfigureRevealButton(_revealSecondButton, canRevealSecond, secondCard, null);
        ConfigureRevealButton(_revealBothButton, canRevealBoth, firstCard, secondCard);
        _revealActions!.Visible = canRevealFirst || canRevealSecond || canRevealBoth;
        LayoutContents();
    }

    private static Button CreateRevealButton(System.Action action)
    {
        var button = FlatUi.Button("亮", FlatUi.SurfaceAlt);
        button.CustomMinimumSize = new Vector2(128, 46);
        button.Pressed += action;
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Right;
        return button;
    }

    private static void ConfigureRevealButton(Button? button, bool visible, Card? firstCard, Card? secondCard)
    {
        if (button == null)
        {
            return;
        }

        button.Visible = visible;
        button.Disabled = !visible;
        button.Text = "亮";
        button.Icon = visible ? CardIconCache.GetCombinedIcon(firstCard, secondCard) : null;
        button.TooltipText = visible
            ? $"亮牌 {(firstCard != null ? firstCard.ShortName : "")}{(secondCard != null ? " " + secondCard.ShortName : "")}".Trim()
            : "";
    }

    private void RevealCard(int cardIndex)
    {
        if (!_isLocal || _lastPlayerId <= 0)
        {
            return;
        }

        GameManager.Instance?.RevealHoleCard(_lastPlayerId, cardIndex);
    }

    private void RevealBothCards()
    {
        RevealCard(0);
        RevealCard(1);
    }

    private Label CreateCenteredLabel(string text, int fontSize, bool muted = false)
    {
        var label = muted ? FlatUi.MutedLabel(text, fontSize) : FlatUi.Label(text, fontSize, HorizontalAlignment.Center);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        AddChild(label);
        return label;
    }

    private void LayoutContents()
    {
        if (_nameLabel == null || _cardsLayer == null)
        {
            return;
        }

        var width = Size.X > 0 ? Size.X : CustomMinimumSize.X;
        var blindHeight = _blindLabel?.Visible == true ? Mathf.Clamp(_cardSize.X * 0.24f, 16f, 28f) : 0f;
        var lineHeight = Mathf.Clamp(_cardSize.X * (_isLocal ? 0.25f : 0.22f), _isLocal ? 24f : 16f, _isLocal ? 36f : 24f);
        var accountHeight = string.IsNullOrEmpty(_accountLabel?.Text) ? 0f : lineHeight * 0.82f;
        var cardsY = blindHeight + lineHeight * 2.12f + accountHeight + (_isLocal ? 8f : 7f);
        var cardGap = _isLocal ? Mathf.Clamp(_cardSize.X * 0.08f, 10f, 18f) : -_cardSize.X * 0.34f;
        var cardsWidth = _cardSize.X * 2f + cardGap;
        var cardsX = (width - cardsWidth) / 2f;

        PositionLabel(_blindLabel, 0, 0, width, blindHeight);
        PositionLabel(_nameLabel, 0, blindHeight, width, lineHeight);
        PositionLabel(_chipsLabel, 0, blindHeight + lineHeight * 0.95f, width, lineHeight);
        PositionLabel(_accountLabel, 0, blindHeight + lineHeight * 1.85f, width, accountHeight);

        _cardsLayer.Position = new Vector2(cardsX, cardsY);
        _cardsLayer.Size = new Vector2(cardsWidth, _cardSize.Y);
        if (_card1 != null)
        {
            _card1.Position = Vector2.Zero;
        }
        if (_card2 != null)
        {
            _card2.Position = new Vector2(_cardSize.X + cardGap, 0);
        }

        var bottomY = cardsY + _cardSize.Y + (_isLocal ? 4f : 2f);
        var revealHeight = _revealActions?.Visible == true ? Mathf.Clamp(_cardSize.X * 0.30f, 46f, 62f) : 0f;
        if (_revealActions != null)
        {
            _revealActions.Position = new Vector2(0, bottomY);
            _revealActions.Size = new Vector2(width, revealHeight);
            foreach (var child in _revealActions.GetChildren())
            {
                if (child is Button button)
                {
                    var buttonWidth = Mathf.Clamp(_cardSize.X * 0.62f, 104f, 148f);
                    button.CustomMinimumSize = new Vector2(buttonWidth, Mathf.Max(40f, revealHeight - 2f));
                    button.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(Mathf.Clamp(_cardSize.X * 0.15f, 16f, 23f)));
                }
            }
        }

        var textY = bottomY + revealHeight;
        PositionLabel(_betLabel, 0, textY, width, lineHeight);
        PositionLabel(_handLabel, 0, textY + lineHeight * 0.86f, width, lineHeight);
        PositionLabel(_statusLabel, 0, textY + lineHeight * 1.72f, width, lineHeight);
    }

    private static void PositionLabel(Label? label, float x, float y, float width, float height)
    {
        if (label == null)
        {
            return;
        }

        label.Visible = height > 0.5f;
        label.Position = new Vector2(x, y);
        label.Size = new Vector2(width, height);
    }

    private static void ApplyLabelSize(Label? label, int size)
    {
        label?.AddThemeFontSizeOverride("font_size", size);
    }

    private static string GetStatusText(Player player)
    {
        if (player.Chips <= 0)
        {
            return "待补码";
        }

        if (player.IsSittingOut)
        {
            return player.WantsSitOutNextHand ? "暂离" : "等下局";
        }

        if (player.IsFolded)
        {
            return "弃牌";
        }

        if (player.IsAllIn)
        {
            return "全下";
        }

        return GameManager.Instance?.IsAiPlayer(player.Id) == true ? "AI" : "";
    }

    private void SetFoldedVisual(bool folded)
    {
        if (_card1 == null || _card2 == null)
        {
            return;
        }

        if (folded && !_lastFolded)
        {
            var tween = CreateTween();
            tween.TweenProperty(_card1, "modulate:a", 0.42f, 0.16);
            tween.Parallel().TweenProperty(_card2, "modulate:a", 0.42f, 0.16);
            tween.Parallel().TweenProperty(_card1, "rotation_degrees", -8.0f, 0.16);
            tween.Parallel().TweenProperty(_card2, "rotation_degrees", 8.0f, 0.16);
        }
        else if (!folded)
        {
            _card1.Modulate = Colors.White;
            _card2.Modulate = Colors.White;
            _card1.RotationDegrees = 0;
            _card2.RotationDegrees = 0;
        }
    }
}
