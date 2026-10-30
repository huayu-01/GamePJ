using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>可随时收起的基地练习指引，只观察真实操作，不限制玩家出击。</summary>
public partial class BaseGuide : CanvasLayer
{
	private PanelContainer _panel = null!;
	private Label _text = null!;
	private Vector3? _origin;
	private bool _moved, _hit, _browsed, _collapsed;
	private float _refresh;
	public bool MovementObserved => _moved;
	public bool HitObserved => _hit;
	public string GuideText => _text.Text;
	public override void _Ready()
	{
		Layer = 3; ProcessMode = ProcessModeEnum.Always;
		_panel = new PanelContainer { Position = new Vector2(24, 90), Size = new Vector2(510, 0), MouseFilter = Control.MouseFilterEnum.Ignore,
			Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
		AddChild(_panel);
		_text = new Label { CustomMinimumSize = new Vector2(480, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
		_text.AddThemeFontSizeOverride("font_size", 17); _panel.AddChild(_text);
		GameplayEventBus.Raised += OnGameplayEvent;
	}
	public override void _ExitTree() => GameplayEventBus.Raised -= OnGameplayEvent;
	private void OnGameplayEvent(GameplayEvent fact)
	{
		// 碰甲也属于成功练习接触；不能只用扣血值，否则木桩有甲时永远无法完成。
		if (fact.Kind == GameplayEventKind.Hit && fact.Source == PlayerLocator.Player && fact.Target != null && fact.Target != fact.Source) _hit = true;
	}
	public override void _Process(double delta)
	{
		_refresh -= (float)delta; if (_refresh > 0) return; _refresh = 0.15f;
		if (PlayerLocator.Player is { } player)
		{
			_origin ??= player.GlobalPosition; _moved |= player.GlobalPosition.DistanceTo(_origin.Value) > 2f;
		}
		_browsed |= UiService.Instance?.Screen<StartScreen>()?.IsOpen == true;
		_panel.Visible = UiRouter.Instance?.AnyOpen != true;
		string move = ControlsSettingsSection.CurrentKeys("move_forward");
		string attack = ControlsSettingsSection.CurrentKeys("weapon_skill_1");
		if (attack.Contains("没有")) attack = "鼠标左键";
		_text.Text = _collapsed ? "F1 展开基地指引" :
			$"基地指引 · F1 收起（可直接出击）\n{Mark(_moved)} {move} 等方向键移动，鼠标转动视角\n" +
			$"{Mark(_hit)} {attack} 攻击前方木桩，观察命中与冷却\n" +
			$"{Mark(_browsed)} 靠近角色展台互动，查看该角色的武器\n" +
			"准备好后前往出击门。符文台和能力卡可稍后了解。";
	}
	private static string Mark(bool done) => done ? "✓" : "○";
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 } && UiRouter.Instance?.AnyOpen != true)
		{ _collapsed = !_collapsed; _refresh = 0; GetViewport().SetInputAsHandled(); }
	}
}
