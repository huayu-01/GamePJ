using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 一条血条的节点句柄：构造时按名字抓「填充条 / 延迟条 / 数值文字 / 护甲条」，
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

	/// <summary>护甲遮罩的节点名（用户 A6：有甲才显示、罩在血条上）。
	/// 预制体里没有这两条 = 这个预制体不画护甲（<c>BarBoss</c> 就没有，首领 0 甲）—— 静默跳过。</summary>
	public const string ArmorFillNode = "ArmorFill";
	public const string ArmorBgNode = "ArmorBg";

	private readonly Control? _bg;
	private readonly Control _fill;
	private readonly Control? _delay;
	private readonly Label? _label;
	private readonly Node? _animation;
	private readonly Control? _armorFill;
	private readonly Control? _armorBg;

	private HealthBarWidget(Control? bg, Control fill, Control? delay, Label? label, Node? animation, Control? armorFill, Control? armorBg)
	{
		_bg = bg;
		_fill = fill;
		_delay = delay;
		_label = label;
		_animation = animation;
		_armorFill = armorFill;
		_armorBg = armorBg;
		FillWidth = fill.Size.X;
		FillHeight = fill.Size.Y;
		BgWidth = bg?.Size.X ?? (fill.Size.X + (2f * fill.Position.X));

		// 高度以填充条为准：整条高度 = 配置的边框高度（旧代码 height+2 的框是围绕 height 的 padding）。
		BgHeight = fill.Size.Y;

		// 护甲条满宽取自预制体自身（与血条不同宽也没关系）；原色用于"碎甲闪白"之后还原。
		ArmorWidth = armorFill?.Size.X ?? 0f;
		// 护甲遮罩的"原色"用于碎甲闪白之后还原。⚠️ 读 **Modulate** 而不是 Color：
		// 新样式里 Color 自己就带染色与透明度（`Color(0.72,0.86,1,0.45)` 那层"塑料"），
		// modulate 才是额外乘上去的那一层 —— 还原成 Color 会把染色乘两遍，ColorRect 型的护甲条会越闪越暗。
		ArmorModulate = armorFill?.Modulate ?? Colors.White;
	}

	/// <summary>满宽（比例 1.0）时填充条的像素宽度/高度，取自预制体。</summary>
	public float FillWidth { get; private set; }

	public float FillHeight { get; private set; }

	/// <summary>整条（含边框）在预制体里的像素尺寸，用于把 Balance.tres 的尺寸换算成缩放。</summary>
	public float BgWidth { get; }

	/// <summary>整条高度，等于填充高度（上下边框是 padding，不额外加高）。</summary>
	public float BgHeight { get; }

	/// <summary>护甲条满宽时的像素宽度（0 = 这个预制体没有护甲条）。</summary>
	public float ArmorWidth { get; }

	/// <summary>护甲遮罩的原始 modulate（碎甲闪白之后还原到它）。</summary>
	private Color ArmorModulate { get; }

	/// <summary>这个预制体有没有护甲条。</summary>
	public bool HasArmorBar => _armorFill != null;

	/// <summary>护甲条现在画的比例（验证用只读值）。</summary>
	public float ArmorRatio { get; private set; }

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

	/// <summary>
	/// 护甲进度（用户 A6：「**要有护甲才会被显示出来**，期望上是**有一圈半透明遮罩遮住血条**，
	/// 就像**血条包裹了一层塑料**，且**比血条大一点点**」）。
	///
	/// <para>预制体里那两个节点就是那层塑料：<c>ArmorBg</c> = 罩住整条血条、比血条大 2px 的半透明壳
	/// （带一圈高亮边框），<c>ArmorFill</c> = 壳里那层磨砂的"甲"（比例 = 剩余甲，从右往左退）。</para>
	///
	/// <para><b>可见性</b>：<paramref name="visible"/> 由调用方按 <c>MaxArmor &gt; 0</c> 决定 ——
	/// **无甲的单位（首领 / 白板怪 / 0 甲角色）任何护甲视觉都不显示**；
	/// 有甲上限但甲被打光时仍然显示（只剩那层空壳）—— 玩家要能看出"甲被打光了"，
	/// 而不是"护甲条忽然消失"。</para>
	/// </summary>
	public void SetArmor(float ratio, bool visible)
	{
		if (_armorFill == null)
		{
			return;
		}

		ArmorRatio = Mathf.Clamp(ratio, 0f, 1f);
		_armorFill.Visible = visible;
		_armorBg?.SetVisible(visible);
		if (!visible)
		{
			return;
		}

		_armorFill.Size = new Vector2(Mathf.Max(0f, ArmorWidth * ArmorRatio), _armorFill.Size.Y);
	}

	/// <summary>
	/// 碎甲闪一下（读 <see cref="Unit.ArmorBroke"/>，只在甲从 &gt;0 掉到 0 时触发）：
	/// 那层"塑料"瞬间刷白再回到原色 —— 与模型上那圈 <see cref="ArmorBreakFx"/> 同时发生。
	/// </summary>
	public void FlashArmorBreak()
	{
		if (_armorFill == null || !GodotObject.IsInstanceValid(_armorFill))
		{
			return;
		}

		_armorFill.Modulate = Colors.White;
		Tween tween = _armorFill.CreateTween();
		tween.TweenProperty(_armorFill, "modulate", ArmorModulate, 0.45f)
			.SetTrans(Tween.TransitionType.Sine);
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
			root.GetNodeOrNull<Node>(AnimationNode),
			Find(root, ArmorFillNode),
			Find(root, ArmorBgNode));
}

/// <summary>
/// 把世界信息投影到屏幕：伤害跳字（按类别分色，见 <see cref="DamageNumbers"/>）+ 敌人血条（含护甲条）。
/// 只读 Gameplay 事件与单位状态，不参与结算；不往敌人预制体里加节点，所以新敌人自动带上。
/// 血条是**事件驱动**的：订阅 GameplayEventBus，受伤时 O(1) 登记该单位，死亡/满血时移除；
/// <see cref="_Process"/> 只更新已登记的那几个血条，不再每帧遍历 enemy_units / boss_units 组。
///
/// <p><b>为什么还要订阅每个单位的 <see cref="Unit.Damaged"/></b>（用户需求 §五 #9）：
/// <c>GameplayEventBus</c> 的 <c>Damaged</c> 事件里 <c>Value</c> 只有 <c>HealthLost</c> ——
/// 「甲 / 盾 / 格挡 / 暴击 / 这次伤害是不是燃烧」这些都在 <see cref="DamageResult"/> 上，
/// 想按类别显示跳字就必须读它。订阅在**第一次命中**（<c>GameplayEventKind.Hit</c>，它先于结算抛出）时建立，
/// 所以连第一次命中的甲数字也不会漏；死亡 / 退树时成对退订。</p>
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

	/// <summary>被订阅了 <see cref="Unit.Damaged"/> 等 C# 事件的单位（退订要拿同一批委托，所以显式存起来）。</summary>
	private sealed class WatchedUnit
	{
		public required Unit Unit { get; init; }
		public required Action<DamageResult, HitInstance> Damaged { get; init; }
		public required Action ArmorBroke { get; init; }
		public required Action<HitInstance> Died { get; init; }
		public double InvulnerableShownAt { get; set; } = -10d;
	}

	private readonly Dictionary<ulong, EventBar> _bars = new();
	private readonly Dictionary<ulong, WatchedUnit> _watches = new();
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

	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnEvent;

		// 退订所有单位事件（成对退订是项目纪律；场景切换时这里一定要走）。
		foreach (WatchedUnit watch in _watches.Values)
		{
			Unsubscribe(watch);
		}

		_watches.Clear();
	}

	private void OnEvent(GameplayEvent gameplayEvent)
	{
		// 血条由事件登记/移除（用户需求：扣血后通知，不遍历组），与伤害跳字各自独立判断。
		if (gameplayEvent.Kind == GameplayEventKind.Died)
		{
			if (gameplayEvent.Target is { } dead)
			{
				RemoveBar(dead);
				Unwatch(dead);
			}

			return;
		}

		if (gameplayEvent.Kind == GameplayEventKind.Hit)
		{
			// 命中事件在**无敌判定之前**抛出（Unit.ReceiveHit 第一段）：
			// ① 顺手订阅这个单位的 C# 事件（这样连第一次命中的甲/格挡数字都不会漏）；
			// ② 这一刻 IsInvulnerable 还是 true —— 无敌时不会走 Damaged，所以「无敌」只能在这里读。
			if (gameplayEvent.Target is { } hitTarget && IsInstanceValid(hitTarget))
			{
				Watch(hitTarget);
				ShowInvulnerable(hitTarget);
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
	}

	/// <summary>
	/// 一次结算完成后的跳字（用户需求 §五 #9）：按 <see cref="DamageResult"/> 的字段分类，
	/// 同一次命中可以同时出现「甲 12」和「5」，靠 <see cref="DamageNumbers.SlotOffsetOf"/> 上下错开。
	/// </summary>
	private void OnUnitDamaged(Unit unit, DamageResult result, HitInstance hit)
	{
		if (!Balance.Settings.ShowDamageNumbers || !IsInstanceValid(unit))
		{
			return;
		}

		if (ToScreen(unit.GlobalPosition + (Vector3.Up * 1.5f)) is not { } screen)
		{
			return;
		}

		BalanceConfig config = Balance.Settings;
		bool byPlayer = hit.Attacker is { Faction: Faction.Player };

		// 血条登记：**护甲被扣也要建条**（用户需求 §五 #8 要能看出"护甲进度"）——
		// `GameplayEventBus` 的 Damaged 事件里只有 HealthLost，甲被吸收时那条事件是 0，
		// 所以"只掉甲、没掉血"的单位必须在**这里**（读得到 DamageResult）才登记得上。
		if (IsBarCandidate(unit) && (unit.Health < unit.MaxHealth || unit.Armor < unit.MaxArmor || unit.Shield < unit.MaxShield))
		{
			RegisterBar(unit);
		}

		// 「其他伤害（电 / 燃烧）」：本次命中**既没有攻击实例、也没有攻击者** —— 状态效果的每秒伤害
		// 与环境直伤都是这样造的 HitInstance（Unit._PhysicsProcess 的状态 tick），普通攻击一定带 AttackInstance。
		bool other = hit.Attack == null && hit.Attacker == null;

		float total = result.ArmorLost + result.ShieldLost + result.HealthLost;

		if (result.ArmorLost > 0.01f)
		{
			DamageNumbers.Spawn(_numberLayer, screen, DamageNumberKind.Armor, result.ArmorLost, unit, byPlayer,
				config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
		}

		if (result.ShieldLost > 0.01f)
		{
			DamageNumbers.Spawn(_numberLayer, screen, DamageNumberKind.Shield, result.ShieldLost, unit, byPlayer,
				config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
		}

		if (result.HealthLost > 0.01f)
		{
			DamageNumberKind kind = other
				? DamageNumberKind.OtherDamage
				: result.Critical ? DamageNumberKind.Critical : DamageNumberKind.Damage;
			DamageNumbers.Spawn(_numberLayer, screen, kind, result.HealthLost, unit, byPlayer,
				config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
		}

		if (result.Blocked)
		{
			DamageNumbers.Spawn(_numberLayer, screen, DamageNumberKind.Block, total, unit, byPlayer,
				config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
		}
	}

	/// <summary>无敌跳字（节流，见 <see cref="DamageNumbers.InvulnerableThrottleSeconds"/>：冲刺无敌帧会被连续命中）。</summary>
	private void ShowInvulnerable(Unit unit)
	{
		if (!Balance.Settings.ShowDamageNumbers || !unit.IsInvulnerable)
		{
			return;
		}

		ulong id = unit.GetInstanceId();
		if (!_watches.TryGetValue(id, out WatchedUnit? watch))
		{
			return;
		}

		double now = Time.GetTicksMsec() / 1000d;
		if (now - watch.InvulnerableShownAt < DamageNumbers.InvulnerableThrottleSeconds)
		{
			return;
		}

		watch.InvulnerableShownAt = now;

		if (ToScreen(unit.GlobalPosition + (Vector3.Up * 1.5f)) is not { } screen)
		{
			return;
		}

		BalanceConfig config = Balance.Settings;
		DamageNumbers.Spawn(_numberLayer, screen, DamageNumberKind.Invulnerable, 0f, unit, false,
			config.DamageNumberFontSize, config.DamageNumberRise, config.DamageNumberLifetime);
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
		// ⚠️ 有甲的单位多一条判据（用户需求 §五 #8）：**血满了但甲被打掉**时也要留着血条 ——
		// 否则"甲被打光"这件事在画面上完全看不到（甲吸收伤害时血量一点没掉）。
		// 没有甲的单位（MaxArmor <= 0）行为与改动前逐位一致：只看血量。
		float ratio = unit.MaxHealth > 0f ? Mathf.Clamp(unit.Health / unit.MaxHealth, 0f, 1f) : 0f;
		bool fullArmor = unit.MaxArmor <= 0f || unit.Armor >= unit.MaxArmor;
		bool hidden = ratio >= 1f && fullArmor;

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

		// 护甲进度（用户需求 §五 #8）：有甲上限的单位显示甲条，甲被打光也留着空条。
		bar.Widget.SetArmor(
			unit.MaxArmor > 0f ? Mathf.Clamp(unit.Armor / unit.MaxArmor, 0f, 1f) : 0f,
			unit.MaxArmor > 0f);

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

	// ───────────────────────── 单位事件订阅（#9 分类跳字 + #8 碎甲表现）─────────────────────────

	/// <summary>第一次看到这个单位时订阅它的 C# 事件（幂等）。</summary>
	private void Watch(Unit unit)
	{
		ulong id = unit.GetInstanceId();
		if (_watches.ContainsKey(id))
		{
			return;
		}

		Action<DamageResult, HitInstance> damaged = (result, hit) => OnUnitDamaged(unit, result, hit);
		Action armorBroke = () => OnUnitArmorBroke(unit);
		Action<HitInstance> died = _ => Unwatch(unit);

		unit.Damaged += damaged;
		unit.ArmorBroke += armorBroke;
		unit.Died += died;
		_watches[id] = new WatchedUnit { Unit = unit, Damaged = damaged, ArmorBroke = armorBroke, Died = died };
	}

	private void Unwatch(Unit unit) => Unwatch(unit.GetInstanceId());

	private void Unwatch(ulong id)
	{
		if (_watches.Remove(id, out WatchedUnit? watch))
		{
			Unsubscribe(watch);
		}
	}

	private static void Unsubscribe(WatchedUnit watch)
	{
		if (GodotObject.IsInstanceValid(watch.Unit))
		{
			watch.Unit.Damaged -= watch.Damaged;
			watch.Unit.ArmorBroke -= watch.ArmorBroke;
			watch.Unit.Died -= watch.Died;
		}
	}

	/// <summary>碎甲（<see cref="Unit.ArmorBroke"/> 只在甲从 &gt;0 掉到 0 时触发）：甲条闪白 + 模型上炸一圈。</summary>
	private void OnUnitArmorBroke(Unit unit)
	{
		ulong id = unit.GetInstanceId();
		if (_bars.TryGetValue(id, out EventBar? bar))
		{
			bar.Widget.FlashArmorBreak();
		}

		if (IsInstanceValid(unit))
		{
			ArmorBreakFx.Spawn(this, unit.GlobalPosition);
		}
	}

	/// <summary>扣血通知到达时的 O(1) 登记：没有血条就实例化一份预制体。</summary>
	private void RegisterBar(Unit unit)
	{
		ulong id = unit.GetInstanceId();
		if (_bars.ContainsKey(id))
		{
			return;
		}

		BalanceConfig config = Balance.Settings;
		_bars[id] = CreateBar(config.EnemyHealthBarWidth, config.EnemyHealthBarHeight, unit.Health / Mathf.Max(1f, unit.MaxHealth), unit);
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
	/// 实例化 BarEnemy 预制体。尺寸由 Balance.tres 决定，外观（颜色/边框/文字/护甲条）由预制体决定。
	/// ⚠️ 预制体的约定：可缩放的整条内容都放在 <c>Content</c> 下，且以 Root 原点为中心
	/// （当前 BarEnemy.tscn 是 Content 摆在 (-1,-1)、宽高 +2）—— 这样 Root.Position 直接就是血条中心。
	/// </summary>
	private EventBar CreateBar(float width, float height, float ratio, Unit unit)
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

		// 护甲遮罩：建出来的这一帧就要有正确比例，否则会先闪一下满甲；
		// 无甲上限的单位（MaxArmor = 0）连壳都不给（`visible: false`）—— 首领血条因此天然没有护甲视觉。
		widget.SetArmor(
			unit.MaxArmor > 0f ? Mathf.Clamp(unit.Armor / unit.MaxArmor, 0f, 1f) : 0f,
			unit.MaxArmor > 0f);

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
