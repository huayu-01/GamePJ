using ActRoguelike.Core;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Run;

public partial class SurvivalHud : CanvasLayer
{
	public SurvivalDirector Director { get; set; } = null!;
	private Label _clock = null!, _objectives = null!, _notice = null!;
	private ProgressBar _experience = null!;
	private HBoxContainer _sceneRuneRow = null!;
	private TextureRect _sceneRuneIcon = null!;
	private Label _sceneRuneLabel = null!;
	public string SceneRuneText => _sceneRuneLabel.Text;
	public bool SceneRuneIconVisible => _sceneRuneRow.Visible && _sceneRuneIcon.Texture != null;
	public override void _Ready()
	{
		Layer = 2;
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
		AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, AnchorLeft = .26f, AnchorRight = .74f, OffsetTop = 22, OffsetBottom = 225 };
		root.AddChild(panel);
		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; panel.AddChild(box);
		_clock = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = "UiSubtitle" }; box.AddChild(_clock);
		_experience = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12), MouseFilter = Control.MouseFilterEnum.Ignore }; box.AddChild(_experience);
		_objectives = new Label { HorizontalAlignment = HorizontalAlignment.Center }; box.AddChild(_objectives);
		_notice = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart }; box.AddChild(_notice);
		_sceneRuneRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; box.AddChild(_sceneRuneRow);
		_sceneRuneIcon = new TextureRect { CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }; _sceneRuneRow.AddChild(_sceneRuneIcon);
		_sceneRuneLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart }; _sceneRuneRow.AddChild(_sceneRuneLabel);
		Director.SceneRuneChanged += RefreshSceneRune; RefreshSceneRune();
	}
	private void RefreshSceneRune()
	{
		var rune = Director.SceneRune?.Definition;
		_sceneRuneRow.Visible = rune != null; _sceneRuneIcon.Texture = rune?.Icon;
		_sceneRuneLabel.Text = rune == null ? "" : $"场景符文 · {rune.LocalizedName}\n{rune.LocalizedDescription}";
	}
	public override void _ExitTree() { if (IsInstanceValid(Director)) Director.SceneRuneChanged -= RefreshSceneRune; }
	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && _sceneRuneRow != null) RefreshSceneRune();
	}
	public override void _Process(double delta)
	{
		if (Director.Definition == null) return;
		int remaining = Mathf.Max(0, Mathf.CeilToInt(Director.Definition.DurationSeconds - Director.ElapsedSeconds));
		string clock = Director.State == SurvivalDirector.PhaseState.FinalBoss ? "击败终局首领" : $"{remaining / 60:00}:{remaining % 60:00}";
		_clock.Text = $"{Director.PhaseTitle}  ·  {clock}  ·  等级 {Director.Level}";
		_experience.MaxValue = Director.NextLevelExperience; _experience.Value = Director.Experience;
		_objectives.Text = $"信标 {Director.CompletedBeacons}/3  ·  场上敌人 {Director.AliveCount}  ·  击杀 {Director.TotalKills}  ·  " +
			string.Format(ContentText.Get("ui.survival.shards", "碎片 {0}"), Director.CollectedLootShards);
		_notice.Text = Director.Notice;
	}
}
