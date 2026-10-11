using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 一条血条的节点句柄：构造时按名字抓「填充条 / 延迟条 / 数值文字」，
/// 之后只改比例与位置。控件类型随美术换（ColorRect / TextureRect / NinePatchRect 都行），
/// 因此这里统一按 <see cref="Control"/> 使用 —— 不做类型强转。
/// </summary>
internal sealed class HealthBarWidget
{
		private static Control? Find(Control root, string name)
			=> root.FindChild(name, recursive: true, owned: false) as Control;

		/// <summary>按名字**递归**查找：预制体层级由美术决定，不能假设 Fill 是 Root 的直接子节点。</summary>
	public const string FillNode = "Fill";
	public const string BgNode = "Bg";
	public const string DelayNode = "DelayBar";
	public const string LabelNode = "Label";
	public const string AnimationNode = "Animation";

	private readonly Control? _bg;
	private readonly Control _fill;
	private readonly Control? _delay;
	private readonly Label? _label;
	private readonly Node? _animation;

	private HealthBarWidget(Control? bg, Control fill, Control? delay, Label? label, Node? animation)
	{
		_bg = bg;
		_fill = fill;
		_delay = delay;
		_label = label;
		_animation = animation;
		FillWidth = fill.Size.X;
		FillHeight = fill.Size.Y;
		BgWidth = bg?.Size.X ?? (fill.Size.X + (2f * fill.Position.X));

		// 高度以填充条为准：整条高度 = 配置的边框高度（旧代码 height+2 的框是围绕 height 的 padding）。
		BgHeight = fill.Size.Y;
	}

	/// <summary>满宽（比例 1.0）时填充条的像素宽度/高度，取自预制体。</summary>
	public float FillWidth { get; private set; }

	public float FillHeight { get; private set; }

	/// <summary>整条（含边框）在预制体里的像素尺寸，用于把 Balance.tres 的尺寸换算成缩放。</summary>
	public float BgWidth { get; }

	/// <summary>整条高度，等于填充高度（上下边框是 padding，不额外加高）。</summary>
	public float BgHeight { get; }

	public string Text
	{
		get => _label?.Text ?? string.Empty;
		set
		{
			if (_label != null)
			{
				_label.Text = value;
			}
		}
	}

	/// <summary>填充比例（0–1）与配色。传 <paramref name="hit"/> 时把受伤前的旧满宽交给延迟条（残影）。</summary>
	public void SetRatio(float ratio, Color color, bool hit)
	{
		if (_fill is ColorRect rect)
		{
			rect.Color = color;
		}
		else
		{
			// 贴图条没有 Color 属性：整体染色，效果等价。
			_fill.Modulate = color;
		}

		_fill.Size = new Vector2(Mathf.Max(0f, FillWidth * Mathf.Clamp(ratio, 0f, 1f)), FillHeight);

		if (hit && _delay != null)
		{
			_delay.Size = new Vector2(FillWidth, FillHeight);
			_delay.Visible = true;
		}
	}

	/// <summary>表现层：延迟条向真实比例缓慢跟随（受伤残影）。数值更新与表现分开，方便以后塞动画。</summary>
	public void AdvanceDelay(float ratio, float speed, float delta)
	{
		if (_delay == null || !_delay.Visible)
		{
			return;
		}

		float target = FillWidth * Mathf.Clamp(ratio, 0f, 1f);
		float width = Mathf.MoveToward(_delay.Size.X, target, speed * delta);
		_delay.Size = new Vector2(width, FillHeight);
		if (Mathf.IsEqualApprox(width, target))
		{
			_delay.Visible = false;
		}
	}

	/// <summary>表现层入口占位：预制体上的 AnimationPlayer 现在没有动画，以后加动画只改这里。</summary>
	public void PlayAnimation(StringName name) => _animation?.Call("play", name);

	/// <summary>从预制体实例里按节点名抓句柄。少一个填充条就是预制体坏了，直接报出来。</summary>
	public static HealthBarWidget Bind(Control root)
		=> new(
			Find(root, BgNode),
			Find(root, FillNode) ?? throw new System.InvalidOperationException($"血条预制体缺少 {FillNode} 节点"),
			Find(root, DelayNode),
			root.GetNodeOrNull<Label>(LabelNode),
			root.GetNodeOrNull<Node>(AnimationNode));
}

/// <summary>
/// 把世界信息投影到屏幕：伤害跳字 + 敌人血条。
/// 只读 Gameplay 事件与单位状态，不参与结算；不往敌人预制体里加节点，所以新敌人自动带上。
/// 血条是**事件驱动**的：订阅 GameplayEventBus，受伤时 O(1) 登记该单位，死亡/满血时移除；
/// <see cref="_Process"/> 只更新已登记的那几个血条，不再每帧遍历 enemy_units / boss_units 组。
/// </summary>
public partial class WorldOverlay : CanvasLayer
{
	/// <summary>敌人血条预制体（外观改这里，代码不碰）。</summary>
	private static readonly PackedScene EnemyBarScene = GD.Load<PackedScene>("res://Scenes/UI/Bars/BarEnemy.tscn");

	/// <summary>延迟条跟随速度（比例/秒）：受伤后残影条慢慢缩到真实血量。</summary>
	private const float DelayFollowSpeed = 0.6f;

	private sealed class EventBar
	{
		public required Control Root { get; init; }
		public required HealthBarWidget Widget { get; init; }

		/// <summary>延迟条当前跟随到的比例（残影条用）。</summary>
		public float Ratio { get; set; }
	}

	private readonly Dictionary<ulong, EventBar> _bars = new();
	private readonly List<ulong> _stale = new();
	private Control _barLayer = null!;
	private Control _numberLayer = null!;

	public override void _Ready()
	{
		Layer = 5;
		_barLayer = GetNode<Control>("Bars");
		_numberLayer = GetNode<Control>("Numbers");
		GameplayEventBus.Raised += OnEvent;
	}

	public override void _ExitTree() => GameplayEventBus.Raised -= OnEvent;

	private void OnEvent(GameplayEvent gameplayEvent)
	{
		// 血条由事件登记/移除（用户需求：扣血后通知，不遍历组），与伤害跳字各自独立判断。
		if (gameplayEvent.Kind == GameplayEventKind.Died)
		{
			if (gameplayEvent.Target is { } dead)
			{
				RemoveBar(dead);
			}

			return;
		}

		if (gameplayEvent.Kind != GameplayEventKind.Damaged)
		{
			return;
		}

		Unit? target = gameplayEvent.Target;
		if (target == null || !IsInstanceValid(target))
		{
			return;
		}

		if (IsBarCandidate(target) && target.Health < target.MaxHealth)
		{
			RegisterBar(target);
		}

		BalanceConfig config = Balance.Settings;
		if (!config.ShowDamageNumbers || gameplayEvent.Value <= 0.01f)
		{
			return;
		}

		if (ToScreen(target.GlobalPosition + (Vector3.Up * 1.5f)) is not { } screen)
		{
			return;
		}

		bool byPlayer = gameplayEvent.Source is { Faction: Faction.Player };
		var label = new Label
		{
			Text = $"{gameplayEvent.Value:0}",
			Position = screen,
			Modulate = byPlayer ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.35f),
		};
		label.AddThemeFontSizeOverride("font_size", config.DamageNumberFontSize);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		label.AddThemeConstantOverride("outline_size", 5);
		_numberLayer.AddChild(label);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position", screen + new Vector2(0f, -config.DamageNumberRise), config.DamageNumberLifetime);
		tween.TweenProperty(label, "modulate:a", 0f, config.DamageNumberLifetime);
		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	public override void _Process(double delta)
	{
		if (!Balance.Settings.ShowEnemyHealthBars)
		{
			ClearBars();
			return;
		}

		if (_bars.Count == 0)
		{
			return;
		}

		// 只更新已登记的血条（通常 0–5 个）；单位被回收时顺手清理。
		_stale.Clear();
		foreach (ulong id in _bars.Keys)
		{
			GodotObject? unit = GodotObject.InstanceFromId(id);
			if (unit is not Unit alive || !IsInstanceValid(alive) || alive.IsDead)
			{
				_stale.Add(id);
			}
			else
			{
				UpdateBar(id, alive, (float)delta);
			}
		}

		foreach (ulong id in _stale)
		{
			RemoveBar(id);
		}
	}

	private void UpdateBar(ulong id, Unit unit, float delta)
	{
		BalanceConfig config = Balance.Settings;
		EventBar bar = _bars[id];

		// 满血不显示（保留原设计）：受伤后显示，回满后隐藏。
		float ratio = unit.MaxHealth > 0f ? Mathf.Clamp(unit.Health / unit.MaxHealth, 0f, 1f) : 0f;
		bool hidden = ratio >= 1f;

		// 血条放在脚下（用户需求）：沿 -Up 偏移。
		Vector2? screen = hidden ? null : ToScreen(unit.GlobalPosition - (Vector3.Up * config.EnemyHealthBarTopOffset));
		bar.Root.Visible = screen != null;
		if (screen is { } position)
		{
			// 预制体把内容摆成「以 Root 原点为中心」，所以屏幕投影点直接就是 Root 位置。
			bar.Root.Position = position;
		}

		// 数值更新：填充比例 + 配色；同时把「受伤前」的比例交给延迟条。
		bool hit = ratio < bar.Ratio;
		bar.Ratio = ratio;
		bar.Widget.SetRatio(ratio, HealthColor(ratio), hit);

		// 表现更新：残影条跟随（独立方法，以后换成动画只改这里）。
		bar.Widget.AdvanceDelay(ratio, DelayFollowSpeed, delta);
	}

	/// <summary>血量配色：绿 → 黄 → 红。美术要换配色改这里（或改成按预制体的主题色）。</summary>
	private static Color HealthColor(float ratio)
		=> ratio > 0.5f ? new Color(0.4f, 0.9f, 0.4f) : ratio > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);

	/// <summary>这个单位该由 WorldOverlay 画血条吗。Boss 交给 PlayerHud 的屏幕上方主血条，这里跳过。</summary>
	private static bool IsBarCandidate(Unit unit)
		=> Balance.Settings.ShowEnemyHealthBars
			&& unit.Faction != Faction.Player
			&& !unit.HasTag("Boss")
			&& !unit.IsDead
			&& unit.MaxHealth > 0f;

	/// <summary>扣血通知到达时的 O(1) 登记：没有血条就实例化一份预制体。</summary>
	private void RegisterBar(Unit unit)
	{
		ulong id = unit.GetInstanceId();
		if (_bars.ContainsKey(id))
		{
			return;
		}

		BalanceConfig config = Balance.Settings;
		_bars[id] = CreateBar(config.EnemyHealthBarWidth, config.EnemyHealthBarHeight, unit.Health / Mathf.Max(1f, unit.MaxHealth));
	}

	private void RemoveBar(Unit unit) => RemoveBar(unit.GetInstanceId());

	private void RemoveBar(ulong id)
	{
		if (_bars.Remove(id, out EventBar? bar))
		{
			bar.Root.QueueFree();
		}
	}

	/// <summary>
	/// 实例化 BarEnemy 预制体。尺寸由 Balance.tres 决定，外观（颜色/边框/文字）由预制体决定。
	/// ⚠️ 预制体的约定：可缩放的整条内容都放在 <c>Content</c> 下，且以 Root 原点为中心
	/// （当前 BarEnemy.tscn 是 Content 摆在 (-1,-1)、宽高 +2）—— 这样 Root.Position 直接就是血条中心。
	/// </summary>
	private EventBar CreateBar(float width, float height, float ratio)
	{
		Control root = EnemyBarScene.Instantiate<Control>();
		HealthBarWidget widget = HealthBarWidget.Bind(root);

		// Balance 改过尺寸就整体缩放 Content（缩放放 Content 上，Root 的 Position 才不受影响）。
		float scaleX = widget.BgWidth > 0f ? width / widget.BgWidth : 1f;
		float scaleY = widget.BgHeight > 0f ? height / widget.BgHeight : 1f;
		if (root.GetNodeOrNull<Control>("Content") is { } content)
		{
			content.Scale = new Vector2(scaleX, scaleY);
			if (!content.Position.IsEqualApprox(-content.Size * 0.5f))
			{
				GD.PushWarning($"BarEnemy 预制体的 Content 没有以 Root 原点为中心（{content.Position}），血条位置会偏。");
			}
		}

		widget.SetRatio(Mathf.Clamp(ratio, 0f, 1f), HealthColor(ratio), false);
		_barLayer.AddChild(root);
		return new EventBar { Root = root, Widget = widget, Ratio = ratio };
	}

	private void ClearBars()
	{
		foreach (EventBar bar in _bars.Values)
		{
			bar.Root.QueueFree();
		}

		_bars.Clear();
	}

	private Vector2? ToScreen(Vector3 world)
	{
		Camera3D? camera = GetViewport().GetCamera3D();
		if (camera == null || camera.IsPositionBehind(world))
		{
			return null;
		}

		return camera.UnprojectPosition(world);
	}
}