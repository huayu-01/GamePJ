using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 敌人头顶跳字的**类别**（用户需求 §五 #9：「伤害 / 甲 / 恢复 / 其他伤害（电/燃烧）/ 格挡 / 无敌」）。
///
/// <para><b>每一类的来源（都是既有事实，没有新造一套）</b>：</para>
/// <list type="bullet">
/// <item><b>伤害 / 甲 / 盾 / 格挡</b> ← <see cref="Unit.Damaged"/> 事件带的 <see cref="DamageResult"/>
///   （<c>HealthLost</c> / <c>ArmorLost</c> / <c>ShieldLost</c> / <c>Blocked</c>）。
///   注意 <c>GameplayEventBus</c> 的 <c>Damaged</c> 事件里 <c>Value</c> **只有 HealthLost**，
///   甲的消耗根本没进事件 —— 所以"甲"这类数字必须读 <see cref="DamageResult"/>。</item>
/// <item><b>其他伤害（电/燃烧）</b> ← 同一条 <see cref="DamageResult"/>，靠"这次命中没有
///   <see cref="HitInstance.Attack"/>"判定：状态效果（燃烧 DoT）与环境直伤都是这样造的
///   <see cref="HitInstance"/>（见 <c>Unit._PhysicsProcess</c> 的状态 tick），普通攻击则一定带 AttackInstance。</item>
/// <item><b>无敌</b> ← <c>GameplayEventKind.Hit</c>：命中事件在**无敌判定之前**抛出（<c>Unit.ReceiveHit</c> 第一段），
///   而这时 <see cref="Unit.IsInvulnerable"/> 还是 true；无敌时不会走 <see cref="Unit.Damaged"/>（管线直接返回）。</item>
/// <item><b>恢复</b> ← <see cref="Unit.Heal"/> **不发任何事件**（Combat 本轮只读，不能加钩子），
///   所以由已经在轮询血量的 UI（<see cref="PlayerHud"/> 20Hz）发现"血量上升"时调用 <see cref="ShowHeal"/>。
///   全项目只有玩家会回血（恢复节点 / 吸血词条），敌人没有回血途径。</item>
/// </list>
/// </summary>
internal enum DamageNumberKind
{
	/// <summary>普通伤害（血量损失）。</summary>
	Damage,

	/// <summary>暴击伤害（同一类，显示更亮更大）。</summary>
	Critical,

	/// <summary>护甲吸收。</summary>
	Armor,

	/// <summary>护盾吸收。</summary>
	Shield,

	/// <summary>被格挡。</summary>
	Block,

	/// <summary>无敌（伤害被免疫）。</summary>
	Invulnerable,

	/// <summary>其他伤害：电 / 燃烧 / 环境直伤（没有攻击实例的伤害）。</summary>
	OtherDamage,

	/// <summary>恢复。</summary>
	Heal,
}

/// <summary>
/// 跳字的外观与排版（**纯表现**，不参与任何结算）。
/// 配色 / 字号 / 前缀都集中在这里一处，改颜色只改这张 switch。
/// </summary>
internal static class DamageNumbers
{
	/// <summary>无敌跳字的节流（秒）：冲刺的无敌帧会被连续命中，不节流会刷一屏（踩过这种刷屏）。</summary>
	public const float InvulnerableThrottleSeconds = 0.35f;

	/// <summary>同类跳字的配色。取色原则：**伤害暖、防御冷、特殊伤害洋红、恢复绿**。</summary>
	public static Color ColorOf(DamageNumberKind kind) => kind switch
	{
		DamageNumberKind.Damage => new Color(1f, 0.85f, 0.35f),
		DamageNumberKind.Critical => new Color(1f, 0.55f, 0.2f),
		DamageNumberKind.Armor => new Color(0.72f, 0.86f, 1f),
		DamageNumberKind.Shield => new Color(0.55f, 0.72f, 1f),
		DamageNumberKind.Block => new Color(0.85f, 0.9f, 1f),
		DamageNumberKind.Invulnerable => new Color(0.78f, 0.82f, 0.92f),
		DamageNumberKind.OtherDamage => new Color(1f, 0.5f, 0.85f),
		_ => new Color(0.45f, 1f, 0.55f),
	};

	/// <summary>字号倍率（相对 ConfigIndex 的 <c>DamageNumberFontSize</c>）。</summary>
	public static float FontScaleOf(DamageNumberKind kind) => kind switch
	{
		DamageNumberKind.Critical => 1.35f,
		DamageNumberKind.Invulnerable or DamageNumberKind.Block => 0.8f,
		_ => 1f,
	};

	/// <summary>
	/// 跳字的**起始垂直错位**（像素，屏幕坐标向下为正；同一次命中最多同时出 4 个数字）。
	/// 甲/盾/无敌往上、伤害居中、格挡往下 —— 这样"甲 12 / 5 / 格挡 17"不会叠在同一个像素上
	/// （踩过一次：格挡与甲都给了 -20，两个数字正好重合）。
	/// </summary>
	public static float SlotOffsetOf(DamageNumberKind kind) => kind switch
	{
		DamageNumberKind.Armor => -20f,
		DamageNumberKind.Shield => -36f,
		DamageNumberKind.Block => 20f,
		DamageNumberKind.Invulnerable => -20f,
		_ => 0f,
	};

	/// <summary>文字：伤害给数字，防御类给「甲/盾/格挡/无敌」，恢复给「＋N」。</summary>
	public static string TextOf(DamageNumberKind kind, float value, Unit? target)
	{
		switch (kind)
		{
			case DamageNumberKind.Armor:
				return $"甲 {value:0}";
			case DamageNumberKind.Shield:
				return $"盾 {value:0}";
			case DamageNumberKind.Block:
				return value > 0.5f ? $"格挡 {value:0}" : "格挡";
			case DamageNumberKind.Invulnerable:
				return "无敌";
			case DamageNumberKind.Heal:
				return $"＋{value:0}";
			case DamageNumberKind.OtherDamage:
				// 「电 / 燃烧」这些没有攻击实例的伤害：认得出燃烧就写「燃」，认不出就只用配色区分。
				return HasStatus(target, "Burn") ? $"燃 {value:0}" : $"{value:0}";
			default:
				return $"{value:0}";
		}
	}

	private static bool HasStatus(Unit? unit, string id)
	{
		if (unit == null || !GodotObject.IsInstanceValid(unit))
		{
			return false;
		}

		foreach (string status in unit.GetStatusIds())
		{
			if (status == id)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 造一个跳字并让它升起淡出（原来这段逻辑内联在 <see cref="WorldOverlay"/>，现在按类别取样式）。
	/// <paramref name="byPlayer"/> 只用来区分"我方打出的伤害"与"敌人打出的伤害"（描边色不同）。
	/// </summary>
	public static void Spawn(
		Control layer,
		Vector2 screen,
		DamageNumberKind kind,
		float value,
		Unit? target,
		bool byPlayer,
		int baseFontSize,
		float rise,
		float lifetime)
	{
		if (layer == null || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}

		Color color = ColorOf(kind);
		Vector2 start = screen + new Vector2(0f, SlotOffsetOf(kind));

		var label = new Label
		{
			Text = TextOf(kind, value, target),
			Position = start,
			Modulate = color,
		};

		label.AddThemeFontSizeOverride("font_size", Mathf.Max(10, (int)(baseFontSize * FontScaleOf(kind))));

		// 描边：我方打出的用深色描边、敌方打出的用偏红描边（原实现把这两者的区别放在字体颜色上，
		// 现在字体颜色已经被"类别"占用了，所以区别挪到描边上）。
		label.AddThemeColorOverride("font_outline_color", byPlayer ? new Color(0f, 0f, 0f, 0.85f) : new Color(0.25f, 0f, 0f, 0.85f));
		label.AddThemeConstantOverride("outline_size", kind == DamageNumberKind.Critical ? 7 : 5);
		layer.AddChild(label);

		Tween tween = label.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position", start + new Vector2(0f, -rise), lifetime);
		tween.TweenProperty(label, "modulate:a", 0f, lifetime);
		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	/// <summary>
	/// 恢复跳字（唯一没有事件的来源，由 UI 侧发现血量上升时调用）。
	/// 直接挂在目标头顶，和伤害跳字走同一套外观。
	/// </summary>
	public static void ShowHeal(Control layer, Vector2 screen, float amount, int baseFontSize, float rise, float lifetime)
		=> Spawn(layer, screen, DamageNumberKind.Heal, amount, null, true, baseFontSize, rise, lifetime);
}
