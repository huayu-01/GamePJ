using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **准星处的蓄力指示器**（P6b 表现层；设计文档 `角色设计.txt` 第 44 行，用户描述最具体的那一条）：
///
/// <blockquote>
/// 「为玩家的准星增加一圈指示（类似一个三角形或四边形，然后是边或角被拆开了，又或者是一个更大的三角形
/// 然后抹去三个角剩下中间的线），当玩家按住右键时这个指示会往准星处缩，直到成为闭合形状，
/// **闭合时准星会有个变色**，短暂过后又会往原来的样子恢复……」
/// </blockquote>
///
/// <para><b>画法（P7 补全的那串时序，用户逐字给过）</b>：一个正方形的四条边，每个角的
/// **两端各留一个缺口**；缺口随 <see cref="Ability.ChargeProgress"/> 从 <see cref="CornerGapMax"/>
/// 线性收到 0 —— 收满那一刻四条边首尾相接，就是文档说的"闭合形状"。之后是按用户原话的时序：</para>
///
/// <list type="number">
/// <item><b>缩</b>：缺口随进度收（<see cref="ChargeColor"/>）。</item>
/// <item><b>闭合</b>：`ChargeBaseComplete` 由 false 变 true，缺口 = 0。</item>
/// <item><b>变色</b>：那一帧起换成 <see cref="PerfectColor"/> + 罩一层光晕 + 准星本体也染色。</item>
/// <item><b>0.3s 后</b>（<see cref="PerfectColorSeconds"/>，导出参数）→ **变回原来的颜色**
///   （<see cref="ChargeColor"/>、光晕收起、准星复原），**但形状依旧是闭合的**（没松手就不散开）。</item>
/// <item><b>松手</b>：`IsCharging` 变 false → 本组件立刻不可见 / 复位。</item>
/// </list>
///
/// <para>⚠️ <see cref="PerfectColorSeconds"/>（0.3s）是**表现层**的量，与战斗层的
/// <see cref="Ability.PerfectWindowSeconds"/>（能量掌武技2 = 0.2s，表值）**不是同一个东西**，
/// 见那个导出属性的注释。</para>
///
/// <para><b>它只读运行期状态，不参与判定</b>：<c>Ability.ChargeProgress</c> /
/// <c>IsPerfectWindow</c> / <c>ChargeBaseComplete</c> / <c>IsCharging</c> 都是 P2 就做好的只读量
/// （见 `docs/abilities.md` §1.1），所以这里一行战斗逻辑都没有 —— 表现改样式不影响手感。</para>
///
/// <para><b>它是能量掌武技2 独有的</b>（P7 用户裁定）：数据源按**技能行 Id** 认（<see cref="AbilityId"/>，
/// 默认 <c>skl_a1_w1_2</c>），不是"任何武器绑在右键上的技能"。见 <see cref="AbilityId"/> 的注释。</para>
///
/// <para>⚠️ **`mouse_filter` 必须是 <c>Ignore</c>**（场景里已设）：它是屏幕正中心的一个 Control，
/// 默认的 <c>Stop</c> 会吃掉鼠标事件，而鼠标捕获时事件坐标恒为中心 → **镜头完全不能转**
/// （`architecture.md` 第 7 条，踩过一次）。</para>
/// </summary>
public partial class ChargeIndicator : Control
{
	/// <summary>
	/// 哪一条技能行是"方形蓄力圈"的技能（**表里的技能行 Id**，默认 = 能量掌 武技2「憋个大的」）。
	///
	/// <para><b>P7 用户裁定</b>：「方形蓄力圈就是**能量掌武技2独有的**」—— 所以别的武器武技2
	/// （弓「刹那之眼」/ 飞刀「千刃齐发」/ 长枪）蓄力时**不该**出现这个圈。
	/// 填了 <see cref="AbilityId"/> 就**只认它**：按行 Id 找不到（这一局没装那把武器）就当"没有这个技能"，
	/// <b>不回退</b> —— 回退会让"别的武器武技2 又冒出方形圈"这个 bug 原样回来。
	/// 与 <c>ScopeOverlay.AbilityId</c> / <c>AfterimageTrail.AbilityId</c> 同一条纪律
	/// （**加武器不改代码**：换技能行只改这一个字段）。</para>
	/// </summary>
	[Export] public string AbilityId { get; set; } = "skl_a1_w1_2";

	/// <summary>
	/// **回退路径**：看哪个输入动作上的技能（默认右键 = 武技 2；能量掌的「憋个大的」就在那儿）。
	/// ⚠️ 只在 <see cref="AbilityId"/> **留空**时生效（想临时回到 P6b 那种"任何武器的武技2 都画圈"的
	/// 行为就清空它；正常游玩不要动）。
	/// </summary>
	[Export] public string AbilityInputAction { get; set; } = "weapon_skill_2";

	/// <summary>正方形半边长（像素）。</summary>
	[Export] public float Radius { get; set; } = 22f;

	/// <summary>线宽（像素）。</summary>
	[Export] public float LineWidth { get; set; } = 3f;

	/// <summary>未蓄力时每个角张开的缺口总长（像素）—— 收满即闭合。</summary>
	[Export] public float CornerGapMax { get; set; } = 18f;

	/// <summary>没在蓄力时（理论上不可见）的线色。</summary>
	[Export] public Color IdleColor { get; set; } = new Color(1f, 1f, 1f, 0.35f);

	/// <summary>蓄力过程中的线色。</summary>
	[Export] public Color ChargeColor { get; set; } = new Color(0.45f, 0.85f, 1f, 0.95f);

	/// <summary>闭合后**变色那 0.3s** 里的线色（"闭合时准星会有个变色"）；0.3s 之后回到 <see cref="ChargeColor"/>。</summary>
	[Export] public Color PerfectColor { get; set; } = new Color(1f, 0.9f, 0.32f, 1f);

	/// <summary>变色那 0.3s 里额外罩的那一圈光晕相对正方形的放大倍数（过了就收起）。</summary>
	[Export] public float PerfectHaloScale { get; set; } = 1.35f;

	[ExportGroup("闭合变色的时序（表现层自己的一段，与战斗层的完美窗口无关）")]
	/// <summary>
	/// **闭合那一下的"变色"持续多久**（秒）。用户给的原话就是 0.3s：
	/// 「完美时机的准星变化：**缩 → 闭合 → 变色 → 0.3s 后 → 变回原来的颜色，但依旧是闭合形状，
	/// 还没松手的情况下**」。
	///
	/// <para>⚠️ 它 **不是** <see cref="Ability.PerfectWindowSeconds"/>（能量掌武技2 = **0.2s** = 那个
	/// "能打出完美一击"的战斗层时间窗，是表值、有平衡含义）。这两个量**不同**：
	/// 0.2s 决定"松手算不算完美"，0.3s 只决定"准星的颜色亮多久"。
	/// 本类**只读** <c>IsPerfectWindow</c>（写进探针），一个表值都没改。</para>
	///
	/// <para>0 = 关掉变色那一段（闭合后直接就是蓄力色）。</para>
	/// </summary>
	[Export] public float PerfectColorSeconds { get; set; } = 0.3f;

	/// <summary>准星本体的变色：变色那 0.3s 里把兄弟节点 `Crosshair` 也染成 <see cref="PerfectColor"/>。</summary>
	[Export] public bool TintCrosshair { get; set; } = true;

	private Unit? _unit;
	private Ability? _ability;
	private Control? _crosshair;
	private Color _crosshairBaseColor = Colors.White;

	// 拍摄台 / 探针用的强制状态（不进战斗逻辑，只为截图与取证）。
	private bool _previewActive;
	private float _previewProgress;
	private bool _previewPerfect;

	// 闭合变色的时序（**纯表现层的计时器**，不写回战斗层任何东西）：
	// `ChargeBaseComplete` 由 false 变 true 的那一帧起 `_colorHoldLeft = PerfectColorSeconds`，之后每帧递减。
	// 减到 0 时**形状照样是闭合的**（缺口由 ChargeProgress 决定，进度到 1 就不动了），只是颜色回到蓄力色。
	private float _colorHoldLeft;
	private bool _wasBaseComplete;

	/// <summary>现在画出来了吗（探针用）。</summary>
	public bool IndicatorVisible { get; private set; }

	/// <summary>现在画的蓄力比例（探针用）。</summary>
	public float ShownProgress { get; private set; }

	/// <summary>现在是不是"闭合形状"（缺口收到 0；探针用）。</summary>
	public bool ShownClosed { get; private set; }

	/// <summary>现在是不是"闭合变色"那一段（探针用；与战斗层的 `IsPerfectWindow` 不是同一个量）。</summary>
	public bool ShownPerfect { get; private set; }

	/// <summary>变色还剩几秒（探针用；0 = 已经变回原来的颜色）。</summary>
	public float PerfectColorRemaining => _colorHoldLeft;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;   // 双保险：场景里也写了，见类注释
		_crosshair = GetNodeOrNull<Control>("../Crosshair");
		if (_crosshair != null)
		{
			_crosshairBaseColor = _crosshair.Modulate;
		}

		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (_previewActive)
		{
			ApplyPreview();
			return;
		}

		// 玩家与技能都是惰性找的：HUD 的 `_Ready` 早于玩家进树（`architecture.md` 第 1 条）。
		if (_unit == null || !IsInstanceValid(_unit))
		{
			_unit = PlayerLocator.Player;
			_ability = null;
		}

		if (_unit == null)
		{
			SetShown(false, 0f, false);
			return;
		}

		if (_ability == null || !IsInstanceValid(_ability))
		{
			_ability = ResolveAbility();
		}

		// 只有"按住蓄力"的技能才有这一段（`IsCharging` 只在 `HoldToCharge` 为真时被输入层置起）。
		bool charging = _ability is { IsCharging: true };
		if (!charging)
		{
			_wasBaseComplete = false;
			_colorHoldLeft = 0f;
			SetShown(false, 0f, false);
			return;
		}

		// ⭐ 用户给的那串时序：缩 → 闭合 → 变色 → 0.3s 后变回原色（**形状保持闭合**）。
		//    上升沿 = `ChargeBaseComplete` 由 false 变 true（只读，不写回）。
		bool closed = _ability!.ChargeBaseComplete;
		if (closed && !_wasBaseComplete)
		{
			_colorHoldLeft = Mathf.Max(0f, PerfectColorSeconds);
		}
		else if (!closed)
		{
			_colorHoldLeft = 0f;
		}

		_wasBaseComplete = closed;

		if (_colorHoldLeft > 0f)
		{
			_colorHoldLeft = Mathf.Max(0f, _colorHoldLeft - (float)delta);
		}

		SetShown(true, _ability.ChargeProgress, _colorHoldLeft > 0f);
	}

	/// <summary>
	/// 找"这个圈该看哪个技能"：**按技能行 Id**（<see cref="AbilityId"/>，默认能量掌武技2）。
	///
	/// <para>⚠️ 填了 <see cref="AbilityId"/> 就**只认它**：按行 Id 找不到就返回 null（= 不画圈），
	/// **不**回退到 <see cref="AbilityInputAction"/>。理由见 <see cref="AbilityId"/> 的注释 ——
	/// 回退 = "弓/飞刀/长枪的武技2 蓄力时又冒出方形圈"（用户明确裁定过那是不对的）。</para>
	///
	/// <para>行 Id 是**表里的标识**，所以"哪一条技能行用这个圈 / 换一把武器"都是填表，不是改代码
	/// （`docs/vfx.md` §2 第 5 条）。</para>
	/// </summary>
	private Ability? ResolveAbility()
	{
		if (_unit == null)
		{
			return null;
		}

		if (!string.IsNullOrEmpty(AbilityId))
		{
			return Ability.FindByAbilityId(_unit, AbilityId);
		}

		return PlayerAbilityInput.FindByInputAction(_unit, AbilityInputAction);
	}

	/// <summary>
	/// 把"这一次应该画成什么样"落到节点上（可见性 + 变色 + 重绘）。
	/// <paramref name="tinted"/> = **闭合变色那 0.3s 之内**（不是"整个完美窗口"）。
	/// </summary>
	private void SetShown(bool show, float progress, bool tinted)
	{
		ShownProgress = Mathf.Clamp(progress, 0f, 1f);
		ShownClosed = ShownProgress >= 1f;
		ShownPerfect = tinted;
		IndicatorVisible = show;

		if (Visible != show)
		{
			Visible = show;
		}

		if (show)
		{
			QueueRedraw();
		}

		if (TintCrosshair && _crosshair != null && IsInstanceValid(_crosshair))
		{
			_crosshair.Modulate = show && tinted ? PerfectColor : _crosshairBaseColor;
		}
	}

	/// <summary>
	/// 画四条边：每条边的两端各让出 <c>gap</c>，`gap = CornerGapMax × (1 − 进度)`。
	/// 进度 1 → gap 0 → 首尾相接成闭合正方形（文档里的"闭合形状"）。
	///
	/// <para>⚠️ **闭合与变色是两个独立的量**（P7 用户时序）：进度到 1 之后就不再变（一直闭合），
	/// 而颜色只亮 <see cref="PerfectColorSeconds"/> 那 0.3s —— 0.3s 之后走
	/// <see cref="ChargeColor"/> 这条分支，形状一个字都不动。</para>
	/// </summary>
	public override void _Draw()
	{
		if (!IndicatorVisible)
		{
			return;
		}

		Color color = ShownPerfect ? PerfectColor : ShownProgress > 0f ? ChargeColor : IdleColor;
		Vector2 center = Size * 0.5f;
		float half = Radius;
		float gap = CornerGapMax * (1f - ShownProgress);

		// 四个角（左上 → 右上 → 右下 → 左下），顺时针连成正方形。
		Vector2[] corners =
		{
			center + new Vector2(-half, -half),
			center + new Vector2(half, -half),
			center + new Vector2(half, half),
			center + new Vector2(-half, half),
		};

		for (int i = 0; i < corners.Length; i++)
		{
			Vector2 from = corners[i];
			Vector2 to = corners[(i + 1) % corners.Length];
			Vector2 along = (to - from).Normalized();
			float length = from.DistanceTo(to);
			float inset = Mathf.Min(gap * 0.5f, length * 0.5f);
			DrawLine(from + (along * inset), to - (along * inset), color, LineWidth, true);
		}

		// 完美时机那 0.3s：外面再罩一圈更亮的闭合方框（"闭合时准星会有个变色"的那一下最显眼）。
		// ⚠️ 0.3s 之后 `ShownPerfect` 为 false，光晕跟着收起来 —— 但上面那四条边仍然是闭合的。
		if (ShownPerfect)
		{
			Color halo = PerfectColor;
			halo.A = 0.45f;
			float r = half * PerfectHaloScale;
			Vector2[] outer =
			{
				center + new Vector2(-r, -r),
				center + new Vector2(r, -r),
				center + new Vector2(r, r),
				center + new Vector2(-r, r),
			};
			for (int i = 0; i < outer.Length; i++)
			{
				DrawLine(outer[i], outer[(i + 1) % outer.Length], halo, LineWidth * 0.6f, true);
			}
		}
	}

	// ───────────────── 拍摄台 / 探针（只摆视觉，不进战斗逻辑） ─────────────────

	/// <summary>
	/// 拍摄台用：强制摆成"蓄力到 <paramref name="progress"/>、<paramref name="tinted"/> = 是否处在
	/// 闭合变色那一段"的样子（不依赖真的按住右键）。后三个参数给 0 = 保持场景里的值；给非 0 就把几何量放大
	/// —— 拍摄台靠它把屏幕中心那个 ~40px 的小东西拍清楚（**判据与画法一个字都没变，只是尺寸**）。
	///
	/// <para>P7 的时序靠同一对入参就能摆全：`(1, true)` = 刚闭合（变色中）；
	/// `(1, false)` = **0.3s 之后那一帧**（变回原色但保持闭合）；`(0.5, false)` = 收缩中。</para>
	/// </summary>
	public void DebugPreviewCharge(float progress, bool tinted, float radius = 0f, float lineWidth = 0f, float cornerGapMax = 0f)
	{
		if (radius > 0f)
		{
			Radius = radius;
		}

		if (lineWidth > 0f)
		{
			LineWidth = lineWidth;
		}

		if (cornerGapMax > 0f)
		{
			CornerGapMax = cornerGapMax;
		}

		_previewActive = true;
		_previewProgress = Mathf.Clamp(progress, 0f, 1f);
		_previewPerfect = tinted;
		ApplyPreview();
	}

	/// <summary>拍摄台用：撤掉强制状态，回到"跟着真实按键走"。</summary>
	public void DebugClearPreview()
	{
		_previewActive = false;
		SetShown(false, 0f, false);
	}

	private void ApplyPreview() => SetShown(true, _previewProgress, _previewPerfect);

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"蓄力指示器：可见={IndicatorVisible} 进度={ShownProgress:0.##} 闭合={ShownClosed} 变色中={ShownPerfect}（还剩 {_colorHoldLeft:0.##}s）"
			+ $"（半径 {Radius:0.#} 线宽 {LineWidth:0.#} 最大缺口 {CornerGapMax:0.#} 变色时长 {PerfectColorSeconds:0.##}s）"
			+ $"｜当前缺口 {CornerGapMax * (1f - ShownProgress):0.#}px｜准星染色={(TintCrosshair && _crosshair != null ? (ShownPerfect ? "完美色" : "原色") : "未接管")}"
			+ $"｜只读源 技能行={AbilityId}（回退按键「{AbilityInputAction}」）={_ability?.AbilityId ?? "（没有按住蓄力型技能）"}"
			+ $" 蓄力中={_ability?.IsCharging ?? false} 基础蓄满={_ability?.ChargeBaseComplete ?? false}"
			+ $" 完美窗口={_ability?.IsPerfectWindow ?? false}（战斗层的 {_ability?.PerfectWindowSeconds ?? 0f:0.##}s，与上面的变色时长是**两个量**）"
			+ $"｜页面鼠标穿透 = {(MouseFilter == MouseFilterEnum.Ignore)}（必须 True，见 architecture.md 第 7 条）";
}
