using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HandHistoryPanel : Panel
{
    private const int HandsPerPage = 4;

    private VBoxContainer? _list;
    private ScrollContainer? _scroll;
    private Label? _pageLabel;
    private Button? _prevPageButton;
    private Button? _nextPageButton;

    private readonly List<HistoryEntry> _parsedEntries = new();
    private readonly List<(int Start, int End)> _pageRanges = new();
    private int _parsedMessageCount;
    private int _currentPageIndex = -1;

    private static readonly Color CheckColor = new(0.48f, 0.72f, 0.92f);
    private static readonly Color CallColor = new(0.94f, 0.78f, 0.34f);
    private static readonly Color RaiseColor = new(1.0f, 0.58f, 0.22f);
    private static readonly Color FoldColor = new(0.95f, 0.32f, 0.32f);
    private static readonly Color WinColor = new(0.25f, 0.86f, 0.52f);
    private static readonly Color DealColor = new(0.70f, 0.82f, 1.0f);

    public override void _Ready()
    {
        BuildUi();
        WireSignals();
        RefreshHistory();
    }

    public void JumpToLatestPage()
    {
        RefreshHistory(forceLatestPage: true);
    }

    private void BuildUi()
    {
        CustomMinimumSize = new Vector2(420, 320);
        AddThemeStyleboxOverride("panel", FlatUi.PanelStyle(FlatUi.SurfaceAlt));

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8;
        root.OffsetTop = 8;
        root.OffsetRight = -8;
        root.OffsetBottom = -8;
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);

        var title = FlatUi.Label("对局记录", 22);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        _prevPageButton = FlatUi.Button("上一页");
        _prevPageButton.CustomMinimumSize = new Vector2(82, 36);
        _prevPageButton.Pressed += () => ChangePage(-1);
        header.AddChild(_prevPageButton);

        _pageLabel = FlatUi.MutedLabel("第 1 / 1 页", 14);
        _pageLabel.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_pageLabel);

        _nextPageButton = FlatUi.Button("下一页");
        _nextPageButton.CustomMinimumSize = new Vector2(82, 36);
        _nextPageButton.Pressed += () => ChangePage(1);
        header.AddChild(_nextPageButton);

        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(390, 240),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(_scroll);

        _list = new VBoxContainer();
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _list.AddThemeConstantOverride("separation", 8);
        _scroll.AddChild(_list);
    }

    private void WireSignals()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.HandHistoryUpdated += RefreshHistory;
    }

    private void RefreshHistory()
    {
        RefreshHistory(false);
    }

    private void RefreshHistory(bool forceLatestPage)
    {
        if (_list == null)
        {
            return;
        }

        EnsureParsedHistory();
        RebuildPageRanges();

        if (_pageRanges.Count == 0)
        {
            _currentPageIndex = -1;
            RenderCurrentPage();
            return;
        }

        var latestIndex = _pageRanges.Count - 1;
        if (forceLatestPage || _currentPageIndex < 0 || _currentPageIndex > latestIndex)
        {
            _currentPageIndex = latestIndex;
        }

        RenderCurrentPage();
    }

    private void EnsureParsedHistory()
    {
        var history = GameManager.Instance?.HandHistory ?? System.Array.Empty<string>();
        if (history.Count < _parsedMessageCount)
        {
            _parsedEntries.Clear();
            _parsedMessageCount = 0;
        }

        for (var i = _parsedMessageCount; i < history.Count; i++)
        {
            _parsedEntries.Add(ParseEntry(history[i]));
        }

        _parsedMessageCount = history.Count;
    }

    private void RebuildPageRanges()
    {
        _pageRanges.Clear();
        if (_parsedEntries.Count == 0)
        {
            return;
        }

        var handStarts = _parsedEntries
            .Select((entry, index) => (entry, index))
            .Where(item => item.entry.Kind == EntryKind.Section)
            .Select(item => item.index)
            .ToList();

        if (handStarts.Count == 0)
        {
            _pageRanges.Add((0, _parsedEntries.Count - 1));
            return;
        }

        var handBlocks = new List<(int Start, int End)>();
        for (var i = 0; i < handStarts.Count; i++)
        {
            var start = handStarts[i];
            var end = i + 1 < handStarts.Count ? handStarts[i + 1] - 1 : _parsedEntries.Count - 1;
            handBlocks.Add((start, end));
        }

        for (var i = 0; i < handBlocks.Count; i += HandsPerPage)
        {
            var pageStart = handBlocks[i].Start;
            var pageEnd = handBlocks[Mathf.Min(i + HandsPerPage - 1, handBlocks.Count - 1)].End;
            _pageRanges.Add((pageStart, pageEnd));
        }
    }

    private void RenderCurrentPage()
    {
        if (_list == null)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        if (_currentPageIndex < 0 || _currentPageIndex >= _pageRanges.Count)
        {
            UpdatePager();
            return;
        }

        var range = _pageRanges[_currentPageIndex];
        for (var i = range.Start; i <= range.End; i++)
        {
            _list.AddChild(BuildEntry(_parsedEntries[i]));
        }

        UpdatePager();
        CallDeferred(nameof(ScrollToBottom));
    }

    private void ChangePage(int delta)
    {
        if (_pageRanges.Count == 0)
        {
            return;
        }

        _currentPageIndex = Mathf.Clamp(_currentPageIndex + delta, 0, _pageRanges.Count - 1);
        RenderCurrentPage();
    }

    private void UpdatePager()
    {
        var pageCount = Mathf.Max(1, _pageRanges.Count);
        var current = _currentPageIndex < 0 ? 1 : _currentPageIndex + 1;
        if (_pageLabel != null)
        {
            _pageLabel.Text = $"第 {current} / {pageCount} 页";
        }

        if (_prevPageButton != null)
        {
            _prevPageButton.Disabled = _currentPageIndex <= 0;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.Disabled = _currentPageIndex < 0 || _currentPageIndex >= pageCount - 1;
        }
    }

    private void ScrollToBottom()
    {
        if (_scroll == null)
        {
            return;
        }

        _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
    }

    private Control BuildEntry(HistoryEntry entry)
    {
        return entry.Kind switch
        {
            EntryKind.Section => BuildSectionEntry(entry),
            EntryKind.Street => BuildStreetEntry(entry),
            _ => BuildPlayerEntry(entry)
        };
    }

    private static Control BuildSectionEntry(HistoryEntry entry)
    {
        var section = FlatUi.Label(entry.Action, 16, HorizontalAlignment.Center);
        section.AddThemeColorOverride("font_color", DealColor);
        return section;
    }

    private Control BuildStreetEntry(HistoryEntry entry)
    {
        var panel = FlatUi.Panel("StreetHistoryEntry");
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0, 88);
        panel.AddThemeStyleboxOverride("panel", FlatUi.PanelStyle(new Color(0.085f, 0.105f, 0.11f, 0.96f), 8));

        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 12;
        root.OffsetTop = 8;
        root.OffsetRight = -12;
        root.OffsetBottom = -8;
        root.AddThemeConstantOverride("separation", 12);
        root.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(root);

        var stageLabel = FlatUi.Label(entry.Action, 22);
        stageLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stageLabel.AddThemeColorOverride("font_color", DealColor);
        root.AddChild(stageLabel);

        var cardRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        cardRow.AddThemeConstantOverride("separation", 5);
        foreach (var card in entry.Cards.Select((card, index) => (card, index)))
        {
            cardRow.AddChild(BuildMiniCard(card.card, card.index >= entry.HighlightCardStart, true));
        }

        root.AddChild(cardRow);
        return panel;
    }

    private Control BuildPlayerEntry(HistoryEntry entry)
    {
        var panel = FlatUi.Panel("HistoryEntry");
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0, entry.Cards.Count > 0 ? 88 : 76);
        panel.AddThemeStyleboxOverride("panel", FlatUi.PanelStyle(new Color(0.075f, 0.095f, 0.10f, 0.92f), 6));

        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8;
        root.OffsetTop = 6;
        root.OffsetRight = -8;
        root.OffsetBottom = -6;
        root.AddThemeConstantOverride("separation", 10);
        root.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(root);

        root.AddChild(BuildAvatar(entry.PlayerName));

        var textBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        textBox.AddThemeConstantOverride("separation", 0);
        root.AddChild(textBox);

        var name = FlatUi.Label(string.IsNullOrWhiteSpace(entry.PlayerName) ? "牌局" : entry.PlayerName, 17);
        name.AddThemeColorOverride("font_color", FlatUi.Text);
        textBox.AddChild(name);

        var action = FlatUi.Label(entry.Action, 17);
        action.AddThemeColorOverride("font_color", entry.ActionColor);
        textBox.AddChild(action);

        if (entry.Cards.Count > 0)
        {
            var cardRow = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.End,
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd
            };
            cardRow.AddThemeConstantOverride("separation", 4);
            foreach (var card in entry.Cards.Select((card, index) => (card, index)))
            {
                cardRow.AddChild(BuildMiniCard(card.card, card.index >= entry.HighlightCardStart, false));
            }

            root.AddChild(cardRow);
        }

        if (!string.IsNullOrWhiteSpace(entry.AmountText))
        {
            var amount = FlatUi.Label(entry.AmountText, 18, HorizontalAlignment.Right);
            amount.AddThemeColorOverride("font_color", entry.AmountPositive ? WinColor : FoldColor);
            amount.CustomMinimumSize = new Vector2(70, 0);
            root.AddChild(amount);
        }

        return panel;
    }

    private static Control BuildAvatar(string playerName)
    {
        var avatar = new Panel
        {
            CustomMinimumSize = new Vector2(46, 46)
        };
        var style = FlatUi.PanelStyle(new Color(0.18f, 0.22f, 0.22f), 23);
        style.BorderColor = new Color(1, 1, 1, 0.12f);
        avatar.AddThemeStyleboxOverride("panel", style);

        var label = FlatUi.Label(GetInitial(playerName), 17, HorizontalAlignment.Center);
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.VerticalAlignment = VerticalAlignment.Center;
        avatar.AddChild(label);
        return avatar;
    }

    private static string GetInitial(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return "牌";
        }

        return playerName.Trim()[0].ToString();
    }

    private static Control BuildMiniCard(Card? card, bool highlighted, bool large)
    {
        var display = new CardDisplay();
        display.SetDisplaySize(large ? new Vector2(50, 72) : new Vector2(42, 60));
        if (card == null)
        {
            display.SetBack();
        }
        else
        {
            display.SetCard(card);
        }

        if (!highlighted)
        {
            return display;
        }

        var frame = new Panel
        {
            CustomMinimumSize = large ? new Vector2(58, 80) : new Vector2(50, 68)
        };
        var style = FlatUi.PanelStyle(new Color(1.0f, 0.82f, 0.18f, 0.12f), 4);
        style.BorderColor = new Color(1.0f, 0.82f, 0.18f, 0.92f);
        style.BorderWidthLeft = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthBottom = 2;
        frame.AddThemeStyleboxOverride("panel", style);
        display.SetAnchorsPreset(LayoutPreset.Center);
        if (large)
        {
            display.OffsetLeft = -25;
            display.OffsetTop = -36;
            display.OffsetRight = 25;
            display.OffsetBottom = 36;
        }
        else
        {
            display.OffsetLeft = -21;
            display.OffsetTop = -30;
            display.OffsetRight = 21;
            display.OffsetBottom = 30;
        }

        frame.AddChild(display);
        return frame;
    }

    private static HistoryEntry ParseEntry(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new HistoryEntry("", "空记录", FlatUi.MutedText);
        }

        if (message.StartsWith("---"))
        {
            return new HistoryEntry("", message.Trim('-').Trim(), DealColor) { Kind = EntryKind.Section };
        }

        var deal = Regex.Match(message, @"^(翻牌|转牌|河牌):\s*(?<cards>.+)$");
        if (deal.Success)
        {
            var cardText = deal.Groups["cards"].Value;
            return new HistoryEntry("", deal.Groups[1].Value, DealColor)
            {
                Kind = EntryKind.Street,
                Cards = ParseCards(cardText),
                HighlightCardStart = GetHighlightCardStart(cardText)
            };
        }

        var reveal = Regex.Match(message, @"^(?<name>.+?)\s+亮牌:\s*(?<cards>.+)$");
        if (reveal.Success)
        {
            var cards = ParseCards(reveal.Groups["cards"].Value);
            if (cards.Count == 1)
            {
                cards.Add(null);
            }

            return new HistoryEntry(reveal.Groups["name"].Value, "亮牌", DealColor)
            {
                Cards = cards,
                HighlightCardStart = int.MaxValue
            };
        }

        var showdown = Regex.Match(message, @"^摊牌\s+(?<name>.+?):\s*(?<cards>.+?)\s*\((?<rank>.+)\)$");
        if (showdown.Success)
        {
            return new HistoryEntry(showdown.Groups["name"].Value, $"摊牌 · {showdown.Groups["rank"].Value}", DealColor)
            {
                Cards = ParseCards(showdown.Groups["cards"].Value),
                HighlightCardStart = int.MaxValue
            };
        }

        var win = Regex.Match(message, @"^(?<name>.+?)\s+收池(?:\s+\(\+(?<amount>\d+)\)|\s+(?<amount2>\d+))$");
        if (win.Success)
        {
            return new HistoryEntry(win.Groups["name"].Value, "收池", WinColor)
            {
                AmountText = $"+{GetAmount(win)}",
                AmountPositive = true
            };
        }

        var lastStanding = Regex.Match(message, @"^其他玩家弃牌，(?<name>.+?)\s+收下底池\s+(?<amount>\d+)$");
        if (lastStanding.Success)
        {
            return new HistoryEntry(lastStanding.Groups["name"].Value, "其他玩家弃牌，直接收池", WinColor)
            {
                AmountText = $"+{lastStanding.Groups["amount"].Value}",
                AmountPositive = true
            };
        }

        var blind = Regex.Match(message, @"^(?<name>.+?)\s+下(?<blind>小盲|大盲)\s+(?<amount>\d+)$");
        if (blind.Success)
        {
            return new HistoryEntry(blind.Groups["name"].Value, $"下{blind.Groups["blind"].Value}", CallColor)
            {
                AmountText = $"-{blind.Groups["amount"].Value}"
            };
        }

        var action = ParseAction(message);
        if (action != null)
        {
            return action;
        }

        return new HistoryEntry("", message, FlatUi.MutedText);
    }

    private static HistoryEntry? ParseAction(string message)
    {
        var timeout = Regex.Match(message, @"^(?<name>.+?)\s+超时，自动(?<action>过牌|弃牌)$");
        if (timeout.Success)
        {
            var actionText = timeout.Groups["action"].Value;
            return new HistoryEntry(timeout.Groups["name"].Value, $"超时 · {actionText}", actionText == "弃牌" ? FoldColor : CheckColor);
        }

        var fold = Regex.Match(message, @"^(?<name>.+?)\s+弃牌$");
        if (fold.Success)
        {
            return new HistoryEntry(fold.Groups["name"].Value, "弃牌", FoldColor);
        }

        var check = Regex.Match(message, @"^(?<name>.+?)\s+过牌$");
        if (check.Success)
        {
            return new HistoryEntry(check.Groups["name"].Value, "过牌", CheckColor);
        }

        var call = Regex.Match(message, @"^(?<name>.+?)\s+跟注(?:\s+\(-(?<amount>\d+)\)|\s+(?<amount2>\d+))$");
        if (call.Success)
        {
            return new HistoryEntry(call.Groups["name"].Value, "跟注", CallColor)
            {
                AmountText = $"-{GetAmount(call)}"
            };
        }

        var betRaise = Regex.Match(message, @"^(?<name>.+?)\s+(?<action>下注|加注|全下)(?:\s+\(-(?<amount>\d+)\))?(?:\s*到\s*(?<total>\d+))?$");
        if (betRaise.Success)
        {
            var actionText = betRaise.Groups["action"].Value;
            var total = betRaise.Groups["total"].Success ? $"到 {betRaise.Groups["total"].Value}" : "";
            return new HistoryEntry(betRaise.Groups["name"].Value, $"{actionText} {total}".Trim(), actionText == "全下" ? FoldColor : RaiseColor)
            {
                AmountText = betRaise.Groups["amount"].Success ? $"-{betRaise.Groups["amount"].Value}" : ""
            };
        }

        return null;
    }

    private static string GetAmount(Match match)
    {
        return match.Groups["amount"].Success ? match.Groups["amount"].Value : match.Groups["amount2"].Value;
    }

    private static List<Card?> ParseCards(string text)
    {
        var result = new List<Card?>();
        foreach (var token in Regex.Matches(text, @"BACK|\?\?|(10|[2-9JQKA])[HDCS]").Cast<Match>().Select(match => match.Value))
        {
            if (token is "BACK" or "??")
            {
                result.Add(null);
                continue;
            }

            var card = ParseCard(token);
            if (card != null)
            {
                result.Add(card);
            }
        }

        return result;
    }

    private static int GetHighlightCardStart(string text)
    {
        var separator = text.IndexOf('|');
        return separator < 0 ? 0 : ParseCards(text[..separator]).Count;
    }

    private static Card? ParseCard(string token)
    {
        if (token.Length < 2)
        {
            return null;
        }

        var suitChar = token[^1];
        var rankText = token[..^1];
        if (!TryParseSuit(suitChar, out var suit) || !TryParseRank(rankText, out var rank))
        {
            return null;
        }

        return new Card { Suit = suit, Rank = rank };
    }

    private static bool TryParseSuit(char suitChar, out Suit suit)
    {
        suit = suitChar switch
        {
            'H' => Suit.Hearts,
            'D' => Suit.Diamonds,
            'C' => Suit.Clubs,
            'S' => Suit.Spades,
            _ => Suit.Spades
        };
        return suitChar is 'H' or 'D' or 'C' or 'S';
    }

    private static bool TryParseRank(string rankText, out Rank rank)
    {
        rank = rankText switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            "10" => Rank.Ten,
            "9" => Rank.Nine,
            "8" => Rank.Eight,
            "7" => Rank.Seven,
            "6" => Rank.Six,
            "5" => Rank.Five,
            "4" => Rank.Four,
            "3" => Rank.Three,
            "2" => Rank.Two,
            _ => Rank.Two
        };
        return rankText is "A" or "K" or "Q" or "J" or "10" or "9" or "8" or "7" or "6" or "5" or "4" or "3" or "2";
    }

    private enum EntryKind
    {
        Player,
        Street,
        Section
    }

    private sealed class HistoryEntry
    {
        public HistoryEntry(string playerName, string action, Color actionColor)
        {
            PlayerName = playerName;
            Action = action;
            ActionColor = actionColor;
        }

        public string PlayerName { get; }
        public string Action { get; }
        public Color ActionColor { get; }
        public EntryKind Kind { get; init; } = EntryKind.Player;
        public string AmountText { get; init; } = "";
        public bool AmountPositive { get; init; }
        public int HighlightCardStart { get; init; } = int.MaxValue;
        public List<Card?> Cards { get; init; } = new();
    }
}
