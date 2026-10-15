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
/// <para><b>画法</b>：一个正方形的四条边，每个角的**两端各留一个缺口**；缺口随
/// <see cref="Ability.ChargeProgress"/> 从 <see cref="CornerGapMax"/> 线性收到 0 ——
/// 收满那一刻四条边首尾相接，就是文档说的"闭合形状"。之后进入
/// <see cref="Ability.IsPerfectWindow"/>（完美时机窗口），整体换成 <see cref="PerfectColor"/>
/// 并罩一层光晕；窗口过后把缺口放回去（"短暂过后又会往原来的样子恢复"）。</para>
///
/// <para><b>它只读运行期状态，不参与判定</b>：<c>Ability.ChargeProgress</c> /
/// <c>IsPerfectWindow</c> / <c>ChargeBaseComplete</c> / <c>IsCharging</c> 都是 P2 就做好的只读量
/// （见 `docs/abilities.md` §7），所以这里一行战斗逻辑都没有 —— 表现改样式不影响手感。</para>
///
/// <para>⚠️ **`mouse_filter` 必须是 <c>Ignore</c>**（场景里已设）：它是屏幕正中心的一个 Control，
/// 默认的 <c>Stop</c> 会吃掉鼠标事件，而鼠标捕获时事件坐标恒为中心 → **镜头完全不能转**
/// （`architecture.md` 第 7 条，踩过一次）。</para>
/// </summary>
public partial class ChargeIndicator : Control
{
	/// <summary>看哪个输入动作上的技能（默认右键 = 武技 2；能量掌的「憋个大的」就在那儿）。</summary>
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

	/// <summary>闭合 / 完美时机窗口里的线色（"闭合时准星会有个变色"）。</summary>
	[Export] public Color PerfectColor { get; set; } = new Color(1f, 0.9f, 0.32f, 1f);

	/// <summary>完美时机窗口里额外罩的那一圈光晕相对正方形的放大倍数。</summary>
	[Export] public float PerfectHaloScale { get; set; } = 1.35f;

	/// <summary>准星本体的变色：闭合时把兄弟节点 `Crosshair` 也染成 <see cref="PerfectColor"/>。</summary>
	[Export] public bool TintCrosshair { get; set; } = true;

	private Unit? _unit;
	private Ability? _ability;
	private Control? _crosshair;
	private Color _crosshairBaseColor = Colors.White;

	// 拍摄台 / 探针用的强制状态（不进战斗逻辑，只为截图与取证）。
	private bool _previewActive;
	private float _previewProgress;
	private bool _previewPerfect;

	/// <summary>现在画出来了吗（探针用）。</summary>
	public bool IndicatorVisible { get; private set; }

	/// <summary>现在画的蓄力比例（探针用）。</summary>
	public float ShownProgress { get; private set; }

	/// <summary>现在是不是"闭合 / 完美时机"配色（探针用）。</summary>
	public bool ShownPerfect { get; private set; }

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
			_ability = PlayerAbilityInput.FindByInputAction(_unit, AbilityInputAction);
		}

		// 只有"按住蓄力"的技能才有这一段（`IsCharging` 只在 `HoldToCharge` 为真时被输入层置起）。
		bool charging = _ability is { IsCharging: true };
		if (!charging)
		{
			SetShown(false, 0f, false);
			return;
		}

		SetShown(true, _ability!.ChargeProgress, _ability.ChargeBaseComplete || _ability.IsPerfectWindow);
	}

	/// <summary>把"这一次应该画成什么样"落到节点上（可见性 + 变色 + 重绘）。</summary>
	private void SetShown(bool show, float progress, bool perfect)
	{
		ShownProgress = Mathf.Clamp(progress, 0f, 1f);
		ShownPerfect = perfect;
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
			_crosshair.Modulate = show && perfect ? PerfectColor : _crosshairBaseColor;
		}
	}

	/// <summary>
	/// 画四条边：每条边的两端各让出 <c>gap</c>，`gap = CornerGapMax × (1 − 进度)`。
	/// 进度 1 → gap 0 → 首尾相接成闭合正方形（文档里的"闭合形状"）。
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

		// 完美时机窗口：外面再罩一圈更亮的闭合方框（"闭合时准星会有个变色"的那一下最显眼）。
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
	/// 拍摄台用：强制摆成"蓄力到 <paramref name="progress"/>、是否处在闭合/完美时机"的样子
	/// （不依赖真的按住右键）。后三个参数给 0 = 保持场景里的值；给非 0 就把几何量放大
	/// —— 拍摄台靠它把屏幕中心那个 ~40px 的小东西拍清楚（**判据与画法一个字都没变，只是尺寸**）。
	/// </summary>
	public void DebugPreviewCharge(float progress, bool perfect, float radius = 0f, float lineWidth = 0f, float cornerGapMax = 0f)
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
		_previewPerfect = perfect;
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
		=> $"蓄力指示器：可见={IndicatorVisible} 进度={ShownProgress:0.##} 闭合/完美={ShownPerfect}"
			+ $"（半径 {Radius:0.#} 线宽 {LineWidth:0.#} 最大缺口 {CornerGapMax:0.#}）"
			+ $"｜当前缺口 {CornerGapMax * (1f - ShownProgress):0.#}px｜准星染色={(TintCrosshair && _crosshair != null ? (ShownPerfect ? "完美色" : "原色") : "未接管")}"
			+ $"｜技能={_ability?.AbilityId ?? "（没有按住蓄力型技能）"} 蓄力中={_ability?.IsCharging ?? false}"
			+ $" 基础蓄满={_ability?.ChargeBaseComplete ?? false} 完美窗口={_ability?.IsPerfectWindow ?? false}"
			+ $"｜页面鼠标穿透 = {(MouseFilter == MouseFilterEnum.Ignore)}（必须 True，见 architecture.md 第 7 条）";
}
