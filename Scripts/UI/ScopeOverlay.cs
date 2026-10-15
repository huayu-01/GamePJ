using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **开镜特效**（P6c 表现层；设计文档 `角色设计.txt` 第 87 行，弓 武技2「刹那之眼」）：
///
/// <blockquote>
/// 「武技2[刹那之眼]{cd:2}:基础蓄力{0.3}秒后**放大视角**，每{0.5}秒的对准星附近的某个目标施加"标记"……」
/// </blockquote>
///
/// <para>机制侧 P3 已经做完了（<see cref="HawkEyeAbility.StepZoom"/> 改 <c>Camera3D.Fov</c>，
/// 实测 78 → 32.4）。本类只补**视觉反馈** —— 一个"镜筒"式的图像层：</para>
/// <list type="bullet">
/// <item><b>暗角</b>：从镜筒半径 <see cref="ScopeRadiusRatio"/> 往外，用一圈套一圈的
///   <c>DrawArc</c> 画出一层由浅到深的环形压暗（半径越大越黑），到屏幕四角最深
///   （<see cref="VignetteMaxAlpha"/>）。</item>
/// <item><b>圆形镜筒遮罩</b>：镜筒半径处一条细亮环（<see cref="RingColor"/> /
///   <see cref="RingWidth"/>）+ 往内偏移一点的第二道细描边（<see cref="InnerRingGap"/>）。</item>
/// <item><b>刻度</b>：环上 <see cref="TickCount"/> 条朝内的短刻度（<see cref="TickLength"/>），
///   **只占环外侧的一小段**，够不到中心。</item>
/// </list>
///
/// <para><b>「不遮挡瞄准」怎么保证</b>：镜筒半径以内**一个像素都不画**（连刻度都只从环上朝内
/// 走 <see cref="TickLength"/> 像素）—— 屏幕正中心那一片永远是完全透明的，
/// 准星（兄弟节点 <c>Crosshair</c>）与蓄力指示器照常可见、照常可读。</para>
///
/// <para><b>它只读运行期状态，不参与判定</b>：唯一的数据来源是
/// <see cref="HawkEyeAbility.ZoomProgress"/>（P6c 为表现层新增的**只读**属性，0 = 没放大 / 1 = 放大到位）
/// 与 <see cref="HawkEyeAbility.IsAiming"/>。强度直接跟缩放进度走 → **开镜时淡入、还原视角时淡出**，
/// 与镜头完全同步；战斗代码（FOV 的写入、平滑、收招时机）一行没动。</para>
///
/// <para>⚠️ <b><c>mouse_filter</c> 必须是 <c>Ignore</c></b>（场景里已设、<c>_Ready</c> 里再兜一次）：
/// 这是一个铺满全屏的 <c>Control</c>，默认的 <c>Stop</c> 会吃掉鼠标事件，
/// 而鼠标捕获时事件坐标恒为中心 → **镜头完全不能转**（`architecture.md` 第 7 条，踩过一次）。</para>
/// </summary>
public partial class ScopeOverlay : Control
{
	[ExportGroup("数据来源（只读运行期状态）")]
	/// <summary>哪一条技能行是"开镜"技能（表里的技能行 Id，默认 = 弓 武技2「刹那之眼」）。留空 = 按类型找。</summary>
	[Export] public string AbilityId { get; set; } = "skl_a3_w1_2";

	/// <summary>强度的平滑速度（1/秒；≤0 = 直接跟 <c>ZoomProgress</c> 走，不额外平滑）。</summary>
	[Export] public float FadeSharpness { get; set; } = 12f;

	[ExportGroup("几何（可在编辑器里调）")]
	/// <summary>镜筒半径 = 屏幕较短边 × 这个比例（0.34 ≈ 1080p 下 367px）。</summary>
	[Export(PropertyHint.Range, "0.05,0.6,0.01")] public float ScopeRadiusRatio { get; set; } = 0.34f;

	/// <summary>暗角渐变的档数（每档一个色标；越大过渡越细腻，开销几乎不变）。</summary>
	[Export(PropertyHint.Range, "2,64,1")] public int VignetteSteps { get; set; } = 24;

	/// <summary>暗角贴图的分辨率（正方形边长，像素；只影响径向渐变的采样细腻度）。</summary>
	[Export(PropertyHint.Range, "32,1024,32")] public int VignetteResolution { get; set; } = 256;

	/// <summary>暗角最深处（屏幕四角）的不透明度。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float VignetteMaxAlpha { get; set; } = 0.8f;

	/// <summary>暗角从镜筒往外的加深曲线：<c>1</c> = 线性；<c>&gt;1</c> = 贴着镜筒几乎不压暗、越靠边越浓。</summary>
	[Export(PropertyHint.Range, "0.4,4,0.05")] public float VignetteFalloff { get; set; } = 1.3f;

	/// <summary>镜筒环的线宽（像素）。</summary>
	[Export] public float RingWidth { get; set; } = 2.5f;

	/// <summary>第二道细描边相对镜筒环**往内**偏移多少像素。</summary>
	[Export] public float InnerRingGap { get; set; } = 7f;

	/// <summary>刻度条数（沿环均分）。</summary>
	[Export(PropertyHint.Range, "0,48,1")] public int TickCount { get; set; } = 12;

	/// <summary>刻度长度（像素，从环朝**内**）。</summary>
	[Export] public float TickLength { get; set; } = 16f;

	/// <summary>刻度线宽（像素）。</summary>
	[Export] public float TickWidth { get; set; } = 2f;

	[ExportGroup("配色")]
	/// <summary>暗角色（越靠边越浓）。</summary>
	[Export] public Color VignetteColor { get; set; } = new(0.02f, 0.03f, 0.05f, 1f);

	/// <summary>镜筒环 / 刻度的颜色。</summary>
	[Export] public Color RingColor { get; set; } = new(0.55f, 0.88f, 1f, 1f);

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在实际画出来的强度（0 = 完全没画）。</summary>
	public float ShownIntensity { get; private set; }

	/// <summary>现在镜筒半径（像素）—— 探针用。</summary>
	public float ShownRadius { get; private set; }

	/// <summary>本帧读到的缩放进度（探针用：证明它真的在消费 <c>ZoomProgress</c>）。</summary>
	public float SourceZoomProgress { get; private set; }

	private Unit? _unit;
	private HawkEyeAbility? _scope;
	private float _intensity;
	private GradientTexture2D? _vignette;
	private int _vignetteSteps = -1;
	private float _vignetteRatio = -1f;
	private float _vignetteFalloff = -1f;
	private float _vignetteInner = -1f;

	// 拍摄台 / 探针用的强制状态（只为截图与取证，不依赖真的按住右键）。
	private bool _previewActive;
	private float _previewIntensity;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;   // 双保险：场景里也写了，见类注释
		Visible = false;
	}

	public override void _Process(double delta)
	{
		float wanted;
		if (_previewActive)
		{
			wanted = Mathf.Clamp(_previewIntensity, 0f, 1f);
			SourceZoomProgress = wanted;
		}
		else
		{
			// 玩家与技能都是惰性找的：HUD 的 `_Ready` 早于玩家进树（`architecture.md` 第 1 条）。
			if (_unit == null || !IsInstanceValid(_unit))
			{
				_unit = PlayerLocator.Player;
				_scope = null;
			}

			if (_unit == null)
			{
				wanted = 0f;
				_scope = null;
				SourceZoomProgress = 0f;
			}
			else
			{
				if (_scope == null || !IsInstanceValid(_scope))
				{
					_scope = ResolveScope();
				}

				// ⭐ 唯一的输入：战斗层给的只读量。没有它就画不出任何东西。
				SourceZoomProgress = _scope?.ZoomProgress ?? 0f;
				wanted = SourceZoomProgress;
			}
		}

		if (_previewActive || FadeSharpness <= 0f)
		{
			_intensity = wanted;
		}
		else
		{
			float weight = 1f - Mathf.Exp(-FadeSharpness * (float)delta);
			_intensity = Mathf.Lerp(_intensity, wanted, weight);
		}

		ShownIntensity = _intensity;
		bool show = _intensity > 0.002f;
		if (Visible != show)
		{
			Visible = show;
		}

		if (show)
		{
			QueueRedraw();
		}
	}

	/// <summary>按表里的行 Id 找开镜技能；填了 <see cref="AbilityId"/> 就只认它，否则按类型找第一个。</summary>
	private HawkEyeAbility? ResolveScope()
	{
		if (_unit == null)
		{
			return null;
		}

		if (!string.IsNullOrEmpty(AbilityId)
			&& Ability.FindByAbilityId(_unit, AbilityId) is HawkEyeAbility byId)
		{
			return byId;
		}

		foreach (Node node in _unit.FindChildren("*", string.Empty, true, false))
		{
			if (node is HawkEyeAbility found)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>
	/// 画三样：外圈暗角（一张径向渐变贴图，越远越浓）+ 镜筒细亮环 + 环上朝内的刻度。
	/// **镜筒半径以内不画任何东西**（见类注释）。
	///
	/// <para>暗角为什么用 <see cref="GradientTexture2D"/> 而不是"一圈套一圈的 <c>DrawArc</c>"：
	/// 后者每一圈的抗锯齿边缘会叠在前一圈的实心上，屏幕上会出现一圈圈**可见的同心细线**
	/// （实测截图里非常明显）。一张径向渐变贴图是一次绘制、零接缝，而且 `VignetteSteps`
	/// 变成"渐变的档数"——照样是导出属性。</para>
	/// </summary>
	public override void _Draw()
	{
		if (ShownIntensity <= 0f || Size.X < 2f || Size.Y < 2f)
		{
			return;
		}

		float intensity = Mathf.Clamp(ShownIntensity, 0f, 1f);
		Vector2 center = Size * 0.5f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f * Mathf.Clamp(ScopeRadiusRatio, 0.02f, 0.95f);
		ShownRadius = radius;

		// ① 暗角：一张铺满"中心 → 屏幕角"的正方形径向渐变（正方形的内切圆就是屏幕中心的圆，
		//    所以镜筒半径在不同窗口比例下恒为圆）。贴图里 0 = 中心、1 = 正方形边缘（屏幕边中点）。
		float outer = Mathf.Max(radius + 1f, center.Length());
		if (EnsureVignette(radius, outer) is { } vignette && VignetteMaxAlpha > 0f)
		{
			vignette.Gradient.SetColor(VignetteStepCount, new Color(
				VignetteColor.R, VignetteColor.G, VignetteColor.B, VignetteMaxAlpha * intensity));
			DrawTextureRect(
				vignette,
				new Rect2(center - new Vector2(outer, outer), new Vector2(outer * 2f, outer * 2f)),
				false);
		}

		// ② 镜筒环：主线 + 往内偏移一道更细的描边（"细描边框"）。
		Color ring = RingColor;
		ring.A = intensity;
		DrawArc(center, radius, 0f, Mathf.Tau, 128, ring, RingWidth, true);

		Color inner = ring;
		inner.A *= 0.55f;
		DrawArc(center, Mathf.Max(1f, radius - InnerRingGap), 0f, Mathf.Tau, 128, inner, RingWidth * 0.5f, true);

		// ③ 刻度：从环朝内，均分 TickCount 条（够不到中心 → 不遮挡瞄准）。
		if (TickCount > 0 && TickLength > 0f)
		{
			Color tick = ring;
			tick.A *= 0.8f;
			for (int i = 0; i < TickCount; i++)
			{
				float angle = Mathf.Tau * i / TickCount;
				Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
				DrawLine(
					center + (dir * radius),
					center + (dir * Mathf.Max(1f, radius - TickLength)),
					tick,
					TickWidth,
					true);
			}
		}
	}

	/// <summary>渐变里一共几个台阶（<see cref="VignetteStepCount"/> = 步数）。</summary>
	private int VignetteStepCount => Mathf.Clamp(VignetteSteps, 2, 64);

	/// <summary>
	/// 备好那张径向渐变贴图：从"镜筒半径 <paramref name="radius"/>"到"正方形半边长
	/// <paramref name="outer"/>"按 <c>t²</c> 把 alpha 拉到 <see cref="VignetteMaxAlpha"/>
	/// （每帧只改最后一个色标）。镜筒半径比例 / 档数 / 窗口尺寸变了就重建。
	/// </summary>
	private GradientTexture2D EnsureVignette(float radius, float outer)
	{
		int steps = VignetteStepCount;
		float ratio = Mathf.Clamp(ScopeRadiusRatio, 0.02f, 0.95f);
		float inner = outer > 0.001f ? Mathf.Clamp(radius / outer, 0f, 0.98f) : 0.2f;
		if (_vignette != null && _vignetteSteps == steps
			&& Mathf.Abs(_vignetteRatio - ratio) < 0.0001f
			&& Mathf.Abs(_vignetteFalloff - VignetteFalloff) < 0.0001f
			&& Mathf.Abs(_vignetteInner - inner) < 0.001f)
		{
			return _vignette;
		}

		var gradient = new Gradient();
		var offsets = new float[steps + 1];
		var colors = new Color[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			float t = i / (float)steps;                             // 0 = 镜筒边缘，1 = 正方形边缘（屏幕边中点）
			offsets[i] = Mathf.Lerp(inner, 1f, t);
			float ramp = i == 0 ? 0f : Mathf.Pow(t, Mathf.Max(0.4f, VignetteFalloff));
			colors[i] = new Color(VignetteColor.R, VignetteColor.G, VignetteColor.B, VignetteMaxAlpha * ramp);
		}

		gradient.Offsets = offsets;
		gradient.Colors = colors;

		_vignette = new GradientTexture2D
		{
			Gradient = gradient,
			Width = Mathf.Max(32, VignetteResolution),
			Height = Mathf.Max(32, VignetteResolution),
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(0.5f, 0f),
		};

		_vignetteSteps = steps;
		_vignetteRatio = ratio;
		_vignetteFalloff = VignetteFalloff;
		_vignetteInner = inner;
		return _vignette;
	}

	// ───────────────── 拍摄台 / 探针（只摆视觉，不进战斗逻辑） ─────────────────

	/// <summary>
	/// 拍摄台用：强制摆成"开镜到 <paramref name="intensity"/>"的样子（不依赖真的按住右键、也不依赖相机）。
	/// 传 0 = 撤掉强制状态。
	/// </summary>
	public void DebugPreviewScope(float intensity)
	{
		_previewActive = intensity > 0f;
		_previewIntensity = Mathf.Clamp(intensity, 0f, 1f);
		if (_previewActive)
		{
			_intensity = _previewIntensity;
			ShownIntensity = _intensity;
			SourceZoomProgress = _intensity;
			Visible = true;
			QueueRedraw();
		}
	}

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"开镜特效：强度={ShownIntensity:0.###} 镜筒半径={ShownRadius:0.#}px"
			+ $"（比例 {ScopeRadiusRatio:0.##} 环宽 {RingWidth:0.#} 刻度 {TickCount}×{TickLength:0.#}px 最暗 {VignetteMaxAlpha:0.##}×{VignetteFalloff:0.##} 渐变 {VignetteStepCount} 档/{VignetteResolution}px）"
			+ $"｜只读源 技能行={AbilityId} 缩放进度={SourceZoomProgress:0.###}"
			+ $" 开镜中={_scope?.IsAiming ?? false} 视角已放大={_scope?.ZoomApplied ?? false}"
			+ $"｜中心净空 {(ShownRadius > 0f ? ShownRadius - TickLength : 0f):0.#}px（>0 = 准星完全不被遮挡；未开镜时不算）"
			+ $"｜页面鼠标穿透 = {(MouseFilter == MouseFilterEnum.Ignore)}（必须 True，见 architecture.md 第 7 条）";
}
