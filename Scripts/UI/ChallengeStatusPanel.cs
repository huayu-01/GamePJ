using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>只读的挑战状态；右上角说明真实生成预算、清场进度与完成结果。</summary>
public partial class ChallengeStatusPanel : PanelContainer
{
	public bool Active => BoundStage?.ChallengerActive == true && PlayerLocator.Player is not { IsDead: true };
	public int CompletionCount { get; private set; }
	public string StatusText => _title.Text + "\n" + _detail.Text;
	public StageController? BoundStage { get; private set; }
	private Label _title = null!, _detail = null!;
	private StyleBoxFlat _frame = null!;
	private float _completedSeconds;
	public override void _Ready()
	{
		Name = "ChallengeStatusPanel"; MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.TopRight);
		OffsetLeft = -364; OffsetRight = -24; OffsetTop = 82; OffsetBottom = 278;
		_frame = new StyleBoxFlat
		{
			BgColor = new Color(.055f, .085f, .13f, .92f), BorderColor = new Color(1, .7f, .25f),
			BorderWidthLeft = 3, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14,
		};
		AddThemeStyleboxOverride("panel", _frame);
		var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		rows.AddThemeConstantOverride("separation", 8); AddChild(rows);
		_title = new Label { MouseFilter = MouseFilterEnum.Ignore };
		_title.AddThemeFontSizeOverride("font_size", 22); rows.AddChild(_title);
		_detail = new Label { MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
		_detail.AddThemeFontSizeOverride("font_size", 17); rows.AddChild(_detail);
		Visible = false;
	}
	public override void _Process(double delta)
	{
		StageController? stage = StageController.Instance;
		if (BoundStage != stage)
		{
			if (BoundStage != null && IsInstanceValid(BoundStage)) BoundStage.ChallengeFinished -= OnFinished;
			BoundStage = stage; _completedSeconds = 0;
			if (BoundStage != null) BoundStage.ChallengeFinished += OnFinished;
		}
		_completedSeconds = Mathf.Max(0, _completedSeconds - (float)delta);
		if (PlayerLocator.Player is { IsDead: true }) { Visible = false; _completedSeconds = 0; return; }
		if (Active && BoundStage?.ActiveChallengeEncounter is { } encounter)
		{
			_completedSeconds = 0; Visible = true;
			_title.Text = encounter.IsBossNode ? "挑战者 · 首领战" : "挑战者 · 挑战进行中";
			_title.Modulate = _frame.BorderColor = new Color(1, .76f, .3f);
			if (encounter.IsBossNode)
			{
				string boss = encounter.Boss is { IsDead: false } ? "首领存活" : "等待首领生成";
				_detail.Text = $"{boss}\n战场敌人 {GetTree().GetNodesInGroup("enemy_units").Count}（含游离与增援）\n生成预算：首领战不适用\n完成挑战前，其他交互暂不可用";
			}
			else
			{
				string wave = encounter.WaveMode ? $"波次 {encounter.WaveIndex}/{(encounter.WaveCount > 0 ? encounter.WaveCount.ToString() : "不限")} · 待生成 {encounter.WaveRemaining}" : "连续生成";
				_detail.Text = $"{wave}\n生成预算 {encounter.RemainingBudget:0.#} / {encounter.TotalBudget:0.#}\n即时额度 {encounter.SpendableBudget:0.#} / {encounter.BudgetCap:0.#}\n本场存活 {encounter.AliveCount} · 清空后解除挑战\n其他交互暂不可用";
			}
		}
		else if (_completedSeconds > 0)
		{
			Visible = true; _title.Text = "挑战完成";
			_title.Modulate = _frame.BorderColor = new Color(.3f, 1, .7f);
			_detail.Text = "本场敌人已清空\n挑战者状态已解除，可以继续交互";
		}
		else Visible = false;
	}
	private void OnFinished(bool completed)
	{
		_completedSeconds = completed && PlayerLocator.Player is not { IsDead: true } ? 2.2f : 0;
		if (_completedSeconds > 0) CompletionCount++;
		else Visible = false;
	}
	public override void _ExitTree()
	{
		if (BoundStage != null && IsInstanceValid(BoundStage)) BoundStage.ChallengeFinished -= OnFinished;
	}
}
