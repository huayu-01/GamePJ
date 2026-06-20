using Godot;
using System.Collections.Generic;

public partial class BettingPanel : Panel
{
    [Signal] public delegate void ActionSubmittedEventHandler(int action, int amount);

    private readonly List<Button> _buttons = new();
    private readonly List<Button> _quickButtons = new();
    private Button? _foldOrCheckButton;
    private Button? _callButton;
    private Button? _raiseButton;
    private Button? _quarterButton;
    private Button? _halfButton;
    private Button? _threeQuarterButton;
    private Button? _allInButton;
    private VBoxContainer? _raiseContainer;
    private HSlider? _raiseSlider;
    private SpinBox? _raiseSpin;
    private Label? _hintLabel;

    private int _currentBet;
    private int _playerBet;
    private int _chips;
    private int _minRaise;
    private int _wagerUnit = 1;
    private int _totalPot;
    private int _callAmount;
    private bool _canCheck;
    private bool _interactive;
    private bool _canBetOrRaiseByRule = true;
    private bool _callButtonActsAsRaise;

    public override void _Ready()
    {
        BuildUi();
    }

    public void ConfigureForStage(float stageWidth)
    {
        ConfigureForStage(stageWidth, Size);
    }

    public void ConfigureForStage(float stageWidth, Vector2 panelSize)
    {
        var width = panelSize.X > 0 ? panelSize.X : Mathf.Clamp(stageWidth * 0.94f, 360f, 920f);
        var height = panelSize.Y > 0 ? panelSize.Y : Mathf.Clamp(stageWidth * 0.48f, 240f, 520f);
        var gap = Mathf.Clamp(stageWidth * 0.016f, 6f, 16f);
        var smallWidth = Mathf.Clamp((width - gap * 6f) / 5f, 64f, 142f);
        var smallHeight = Mathf.Clamp(stageWidth * 0.064f, 42f, 72f);
        var largeWidth = Mathf.Clamp(Mathf.Min(stageWidth * 0.25f, (width - gap * 3f) / 2f), 120f, 248f);
        var largeHeight = Mathf.Clamp(stageWidth * 0.14f, 86f, 164f);
        var fontSize = Mathf.RoundToInt(Mathf.Clamp(stageWidth * 0.034f, 18f, 32f));
        var centerX = width / 2f;
        // 快捷加注按钮下移到本地玩家原名称区域，给下注文本留出独立空间。
        var arcTop = Mathf.Clamp(height * 0.29f, 92f, 160f);
        var arcDrop = Mathf.Clamp(stageWidth * 0.040f, 20f, 44f);
        var buttonStep = (width - gap * 2f - smallWidth) / 4f;
        var quickY = new[] { arcTop + arcDrop, arcTop + arcDrop * 0.45f, arcTop, arcTop + arcDrop * 0.45f, arcTop + arcDrop };
        var quickButtons = new[] { _halfButton, _quarterButton, _raiseButton, _threeQuarterButton, _allInButton };
        for (var index = 0; index < quickButtons.Length; index++)
        {
            PositionButton(quickButtons[index], gap + buttonStep * index, quickY[index], smallWidth, smallHeight, fontSize);
        }

        var quickBottom = arcTop + arcDrop + smallHeight;
        var bottomY = Mathf.Max(quickBottom + gap, height - largeHeight - gap);
        PositionButton(_foldOrCheckButton, gap, bottomY, largeWidth, largeHeight, fontSize + 1);
        PositionButton(_callButton, width - largeWidth - gap, bottomY, largeWidth, largeHeight, fontSize + 1);

        if (_hintLabel != null)
        {
            _hintLabel.Position = new Vector2((width - 180f) / 2f, Mathf.Max(0, arcTop - 34f));
            _hintLabel.Size = new Vector2(180f, 24f);
            _hintLabel.AddThemeFontSizeOverride("font_size", Mathf.Max(13, fontSize - 8));
        }

        if (_raiseContainer != null)
        {
            var raiseWidth = Mathf.Clamp(stageWidth * 0.28f, 140f, 260f);
            _raiseContainer.Position = new Vector2((width - raiseWidth) / 2f, quickBottom + gap);
            _raiseContainer.Size = new Vector2(raiseWidth, Mathf.Clamp(stageWidth * 0.16f, 86f, 150f));
        }
    }

    public void UpdateState(int currentBet, int playerBet, int chips, int minRaise, int totalPot = 0, bool interactive = true, int wagerUnit = 1, bool canBetOrRaiseByRule = true)
    {
        _currentBet = currentBet;
        _playerBet = playerBet;
        _chips = chips;
        _minRaise = minRaise;
        _wagerUnit = Mathf.Max(1, wagerUnit);
        _totalPot = totalPot;
        _interactive = interactive;
        _canBetOrRaiseByRule = canBetOrRaiseByRule;
        _callAmount = Mathf.Max(0, currentBet - playerBet);
        _canCheck = _callAmount == 0;

        Visible = interactive;
        if (!interactive)
        {
            HideRaiseControls();
            return;
        }

        var canCall = _callAmount > 0 && chips >= _callAmount;
        var canRaise = CanBetOrRaise();

        if (_foldOrCheckButton != null)
        {
            _foldOrCheckButton.Text = _canCheck ? "过牌" : "弃牌";
            FlatUi.StyleButton(_foldOrCheckButton, _canCheck ? FlatUi.AccentMuted : FlatUi.Danger);
            _foldOrCheckButton.Disabled = false;
        }

        if (_callButton != null)
        {
            _callButtonActsAsRaise = !canCall && canRaise;
            _callButton.Text = canCall
                ? $"跟注 {_callAmount}"
                : _callButtonActsAsRaise
                    ? $"{(_currentBet == 0 ? "下注" : "加注")} {GetMinimumBetOrRaiseTotal()}"
                    : "跟注";
            _callButton.Disabled = !canCall && !_callButtonActsAsRaise;
        }

        if (_raiseButton != null)
        {
            _raiseButton.Text = currentBet == 0 ? "下注" : "加注";
            _raiseButton.Disabled = !canRaise;
        }

        foreach (var button in _quickButtons)
        {
            button.Disabled = !canRaise;
        }

        if (_allInButton != null)
        {
            _allInButton.Disabled = chips <= 0;
        }

        UpdateRaiseLimits();
        UpdateQuickButtonTooltips();
        if (!canRaise)
        {
            HideRaiseControls();
        }

        if (_hintLabel != null)
        {
            _hintLabel.Text = "轮到你行动";
        }
    }

    private void BuildUi()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        MouseFilter = MouseFilterEnum.Pass;

        _hintLabel = FlatUi.MutedLabel("轮到你行动", 13);
        _hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_hintLabel);

        _halfButton = AddButton("1/2", () => SubmitPotFraction(0.5f));
        _quarterButton = AddButton("1/4", () => SubmitPotFraction(0.25f));
        _raiseButton = AddButton("加注", ToggleRaiseControls, FlatUi.AccentMuted);
        _threeQuarterButton = AddButton("3/4", () => SubmitPotFraction(0.75f));
        _allInButton = AddButton("全下", () => Submit(PlayerAction.AllIn, _chips), FlatUi.Danger);
        _foldOrCheckButton = AddButton("过牌", SubmitFoldOrCheck);
        _callButton = AddButton("跟注", SubmitCallOrRaise);

        _quickButtons.Add(_halfButton);
        _quickButtons.Add(_quarterButton);
        _quickButtons.Add(_threeQuarterButton);

        _raiseContainer = new VBoxContainer { Name = "RaiseContainer", Visible = false };
        _raiseContainer.AddThemeConstantOverride("separation", 6);
        AddChild(_raiseContainer);

        _raiseSlider = new HSlider { Name = "RaiseSlider", MinValue = 20, MaxValue = 1000, Value = 40 };
        _raiseSpin = new SpinBox { Name = "RaiseSpinBox", MinValue = 20, MaxValue = 1000, Value = 40 };
        _raiseSlider.ValueChanged += value => _raiseSpin.Value = value;
        _raiseSpin.ValueChanged += value => _raiseSlider.Value = value;
        _raiseContainer.AddChild(_raiseSlider);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(_raiseSpin);
        var confirm = FlatUi.Button("确认", FlatUi.AccentMuted);
        confirm.Pressed += ConfirmBetOrRaise;
        row.AddChild(confirm);
        _raiseContainer.AddChild(row);
    }

    private Button AddButton(string text, System.Action action, Color? color = null)
    {
        var button = FlatUi.Button(text, color);
        button.Pressed += action;
        AddChild(button);
        _buttons.Add(button);
        return button;
    }

    private static void PositionButton(Button? button, float x, float y, float width, float height, int fontSize)
    {
        if (button == null)
        {
            return;
        }

        button.Position = new Vector2(x, y);
        button.Size = new Vector2(width, height);
        button.CustomMinimumSize = button.Size;
        button.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private void SubmitFoldOrCheck()
    {
        Submit(_canCheck ? PlayerAction.Check : PlayerAction.Fold, 0);
    }

    private void SubmitCallOrRaise()
    {
        if (_callButtonActsAsRaise)
        {
            Submit(_currentBet == 0 ? PlayerAction.Bet : PlayerAction.Raise, GetMinimumBetOrRaiseTotal());
            return;
        }

        Submit(PlayerAction.Call, _callAmount);
    }

    private void ToggleRaiseControls()
    {
        if (!_interactive || _raiseContainer == null)
        {
            return;
        }

        _raiseContainer.Visible = !_raiseContainer.Visible;
    }

    private void OpenRaiseControls(float fraction)
    {
        if (!_interactive)
        {
            return;
        }

        SetRaiseAmountFromPot(fraction);
        if (_raiseContainer != null)
        {
            _raiseContainer.Visible = true;
        }
    }

    private void HideRaiseControls()
    {
        if (_raiseContainer != null)
        {
            _raiseContainer.Visible = false;
        }
    }

    private void SetRaiseAmountFromPot(float fraction)
    {
        var clamped = GetPotFractionTotal(fraction);
        if (_raiseSpin != null)
        {
            _raiseSpin.Value = clamped;
        }
    }

    private void UpdateRaiseLimits()
    {
        if (_raiseSlider == null || _raiseSpin == null)
        {
            return;
        }

        var min = GetMinimumBetOrRaiseTotal();
        min = Mathf.Clamp(min, _playerBet, _playerBet + _chips);
        _raiseSlider.MinValue = min;
        _raiseSlider.MaxValue = Mathf.Max(min, _playerBet + _chips);
        _raiseSlider.Step = _wagerUnit;
        if (_raiseSpin.Value < _raiseSlider.MinValue || _raiseSpin.Value > _raiseSlider.MaxValue)
        {
            _raiseSlider.Value = _raiseSlider.MinValue;
        }
        _raiseSpin.MinValue = _raiseSlider.MinValue;
        _raiseSpin.MaxValue = _raiseSlider.MaxValue;
        _raiseSpin.Step = _wagerUnit;
        _raiseSpin.Value = _raiseSlider.Value;
    }

    private void ConfirmBetOrRaise()
    {
        var action = _currentBet == 0 ? PlayerAction.Bet : PlayerAction.Raise;
        Submit(action, (int)(_raiseSpin?.Value ?? _minRaise));
    }

    private void SubmitPotFraction(float fraction)
    {
        if (!_interactive || !CanBetOrRaise())
        {
            return;
        }

        var action = _currentBet == 0 ? PlayerAction.Bet : PlayerAction.Raise;
        Submit(action, GetPotFractionTotal(fraction));
    }

    private void UpdateQuickButtonTooltips()
    {
        SetQuickTooltip(_quarterButton, 0.25f);
        SetQuickTooltip(_halfButton, 0.5f);
        SetQuickTooltip(_threeQuarterButton, 0.75f);
    }

    private void SetQuickTooltip(Button? button, float fraction)
    {
        if (button == null)
        {
            return;
        }

        if (!_interactive || !CanBetOrRaise())
        {
            button.TooltipText = "当前不能加注";
            return;
        }

        var amount = GetPotFractionTotal(fraction);
        button.TooltipText = _currentBet == 0 ? $"下注 {amount}" : $"加注到 {amount}";
    }

    private int GetPotFractionTotal(float fraction)
    {
        var min = GetMinimumBetOrRaiseTotal();
        var target = _currentBet + Mathf.RoundToInt(Mathf.Max(_totalPot, _minRaise) * fraction);
        return Mathf.Clamp(AlignToWagerUnit(target), min, _playerBet + _chips);
    }

    private bool CanBetOrRaise()
    {
        return _canBetOrRaiseByRule && _playerBet + _chips >= GetMinimumBetOrRaiseTotal();
    }

    private int GetMinimumBetOrRaiseTotal()
    {
        return AlignToWagerUnit(_currentBet == 0 ? _minRaise : _currentBet + _minRaise);
    }

    private int AlignToWagerUnit(int amount)
    {
        if (_wagerUnit <= 1)
        {
            return amount;
        }

        return Mathf.CeilToInt(amount / (float)_wagerUnit) * _wagerUnit;
    }

    private void Submit(PlayerAction action, int amount)
    {
        if (!_interactive)
        {
            return;
        }

        if (action == PlayerAction.Call)
        {
            amount = _callAmount;
        }
        else if (action == PlayerAction.AllIn)
        {
            amount = _chips;
        }

        EmitSignal(SignalName.ActionSubmitted, (int)action, amount);
        HideRaiseControls();
    }
}
