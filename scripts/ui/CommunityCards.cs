using Godot;
using System.Collections.Generic;

public partial class CommunityCards : HBoxContainer
{
    private readonly List<CardDisplay> _cards = new();
    private readonly List<string> _visibleKeys = new();
    private Vector2 _cardSize = new(72, 104);

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < 5; i++)
        {
            var display = new CardDisplay();
            display.SetCard(null);
            display.Modulate = new Color(1, 1, 1, 0.3f);
            _cards.Add(display);
            _visibleKeys.Add("");
            AddChild(display);
        }
    }

    public void SetCardSize(Vector2 cardSize)
    {
        _cardSize = cardSize;
        foreach (var card in _cards)
        {
            card.SetDisplaySize(_cardSize);
        }
    }

    public void SetCards(IReadOnlyList<Card> cards)
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            if (i < cards.Count)
            {
                var key = cards[i].ShortName;
                if (_visibleKeys[i] != key)
                {
                    AnimateCardIn(i, cards[i]);
                }
                else
                {
                    _cards[i].SetCard(cards[i]);
                    _cards[i].Modulate = Colors.White;
                }

                _visibleKeys[i] = key;
            }
            else
            {
                _cards[i].SetCard(null);
                _cards[i].Modulate = new Color(1, 1, 1, 0.3f);
                _visibleKeys[i] = "";
            }
        }
    }

    private void AnimateCardIn(int index, Card card)
    {
        var display = _cards[index];
        display.SetCard(card, false);
        display.Modulate = new Color(1, 1, 1, 0);
        display.Scale = new Vector2(0.82f, 0.82f);
        display.RotationDegrees = index % 2 == 0 ? -4 : 4;

        var tween = display.CreateTween();
        tween.TweenInterval(index * 0.05);
        tween.TweenProperty(display, "modulate:a", 1.0f, 0.12);
        tween.Parallel().TweenProperty(display, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back);
        tween.Parallel().TweenProperty(display, "rotation_degrees", 0.0f, 0.2);
        tween.TweenCallback(Callable.From(() => display.FlipTo(card)));
    }
}
