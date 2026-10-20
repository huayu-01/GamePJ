using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **技能指示器**（通用表现层组件；**玩家与敌人共用同一个组件**，靠样式 + 配色区分）。
///
/// <para>它回答两件事，两种阵营各用一半：</para>
/// <list type="bullet">
/// <item><b>敌人技能的示警</b>：跳跃落地类技能在**落点**画一个**红圈**（②）、冲锋类技能在身前画
///   一条**红色矩形**（④）。</item>
/// <item><b>玩家长按技能时的命中区预览**：投掷/抛射类技能画**半透明有宽度的曲线**（①）、
///   准星指哪就在哪画**青蓝圈**（②）、曲线 + 末端圈（③）。</item>
/// </list>
///
/// <para><b>四种样式</b>（<see cref="IndicatorStyle"/>，<see cref="Style"/> 是实例上的默认样式，
/// 运行期实际画哪种由调用哪个 <c>ShowXxx</c> 决定，<see cref="CurrentStyle"/> 是实际画法的只读回读）：</para>
///
/// <list type="number">
/// <item><b>① <see cref="IndicatorStyle.CurvePath"/></b>：把弹道抛物线画成一条**半透明、有宽度**的带
///   （<see cref="Width"/>）。曲线**采样复用 <see cref="DashPathIndicator.SampleParabola"/>** ——
///   与 <see cref="DashPathIndicator"/> 是同一个抛物面公式，本类不重写采样。</item>
/// <item><b>② <see cref="IndicatorStyle.AreaPoint"/></b>：贴地**圆圈**（环 + 很薄的填充盘）。
///   敌人 = 落点红圈（调用方把落点算好传进来）；玩家 = 准星指哪就是哪
///   （<see cref="ShowAreaPointAtCrosshair"/> 内部走 <see cref="AimUtil.GetCrosshairPoint"/>）。</item>
/// <item><b>③ <see cref="IndicatorStyle.CurvePathPlusArea"/></b>：① + 末端 ② （玩家限定）。</item>
/// <item><b>④ <see cref="IndicatorStyle.LineArea"/></b>：地面上的**矩形**（有 <see cref="Length"/> 与
///   <see cref="Width"/>）。方向先**贴地投影**（<c>direction</c> 的 Y 分量被丢掉），矩形躺在地面上，
///   不是悬空的斜盒子。</item>
/// </list>
///
/// <para>⚠️ <b>三条纪律</b>（`docs/vfx.md` §1 / §3）：</para>
/// <list type="bullet">
/// <item><b>不进物理世界</b>：本组件与它的子树里**没有** <c>CollisionObject3D</c>、**没有** <c>Unit</c>、
///   **不进任何分组** —— 所以不会被 <c>ClearField()</c> 清场、不会被单位计数、不会吃伤害。
///   <see cref="DebugState"/> 每次都会把这三项现算一遍（都是 0 / 无）。</item>
/// <item><b>只读、只画</b>：不写任何一个战斗状态（伤害 / 冷却 / 判定 / 单位位置全都不碰）；
///   入参（起点、方向、初速、半径、长度）由调用方给，本类不自己推战斗数值。</item>
/// <item><b>不硬编码</b>：颜色 / 透明度 / 宽度 / 长度 / 半径 / 贴地偏移 / 呼吸周期全是 <c>[Export]</c>，
///   几何量都从 <c>Export</c> 来（调用方显式传的数值优先，传 0 才回落到 <c>Export</c>）。</item>
/// </list>
///
/// <para><b>为什么不自己 <c>new</c> 节点</b>：用户明确要求**预制体化** —— 网格 / 材质都在
/// <c>Scenes/Combat/SkillIndicator.tscn</c> 里（<c>ImmediateMesh</c> / <c>TorusMesh</c> /
/// <c>CylinderMesh</c> / <c>BoxMesh</c> + 四份 <c>StandardMaterial3D</c>），本类只负责
/// <b>摆位置 + 填顶点 + 上色</b>。纯代码生成见 <see cref="DashPathIndicator"/>（那是它的写法）。</para>
///
/// <para><b>世界坐标约定</b>：预制体里的 <c>DrawRoot</c> 在 <c>_Ready</c> 里被设成
/// <see cref="Node3D.TopLevel"/> 并把变换清成单位阵 —— 于是它的子节点坐标**就是世界坐标**，
/// 组件挂在哪（<c>Main</c> 下 / 玩家身上 / 敌人身上）都画在同一个世界位置上，也不会被
/// 「生成方在 <c>AddChild</c> 之后才写 <c>GlobalPosition</c>」那条坑影响
/// （`docs/HANDOFF.md` §6 第 7 条：本类**不**在 <c>_Ready</c> 里改自己的位置）。</para>
/// </summary>
public partial class SkillIndicator : Node3D
{
	/// <summary>四种样式（见类注释）。</summary>
	public enum IndicatorStyle
	{
		/// <summary>① 曲线路径：半透明 + 有宽度的抛物线带。</summary>
		CurvePath,

		/// <summary>② 区域指向：贴地圆圈（敌人红圈在落点 / 玩家准星指哪就是哪）。</summary>
		AreaPoint,

		/// <summary>③ 曲线 + 末端圈（玩家限定）。</summary>
		CurvePathPlusArea,

		/// <summary>④ 直线矩形区域（冲撞 / 大范围直线伤害）。</summary>
		LineArea,
	}

	// ───────────────────────── 配置（全部 [Export]，编辑期可调） ─────────────────────────

	[ExportGroup("样式 / 阵营")]
	/// <summary>
	/// 这个实例的**默认样式**（编辑器里看得出它是干什么用的）。
	/// 运行期画什么由调用哪个 <c>ShowXxx</c> 决定，画完会把它同步成实际画法（见 <see cref="CurrentStyle"/>）。
	/// </summary>
	[Export] public IndicatorStyle Style { get; set; } = IndicatorStyle.CurvePath;

	/// <summary>
	/// <c>true</c> = **敌人**（红圈 / 红色矩形 = 示警）；<c>false</c> = **玩家**（青蓝 = 命中区预览）。
	/// 只决定默认色（见 <see cref="PlayerColor"/> / <see cref="HostileColor"/>），不影响任何判定。
	/// </summary>
	[Export] public bool IsHostile { get; set; }

	/// <summary>玩家那一套默认色（青蓝）。</summary>
	[Export] public Color PlayerColor { get; set; } = new(0.35f, 0.88f, 1f, 1f);

	/// <summary>敌人那一套默认色（红）。</summary>
	[Export] public Color HostileColor { get; set; } = new(1f, 0.26f, 0.2f, 1f);

	/// <summary>
	/// **最后一道色调覆盖**（可选的导出参数）：给它一个 A &gt; 0 的颜色就整圈换色；
	/// 留默认的**全透明** <c>(0,0,0,0)</c> = 按 <see cref="IsHostile"/> 用上面那两套默认色。
	/// </summary>
	[Export] public Color StyleColor { get; set; } = new(0f, 0f, 0f, 0f);

	[ExportGroup("几何 / 透明度（米）")]
	/// <summary>整条指示器的**半透明**（0~1）。曲线的带、圈、矩形填充都乘它。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float Alpha { get; set; } = 0.45f;

	/// <summary>**宽度**（米）：① 曲线带的宽度、④ 矩形的宽度。</summary>
	[Export] public float Width { get; set; } = 0.35f;

	/// <summary>**长度**（米）：④ 矩形的长度；也是 ① 没传 <c>maxDistance</c> 时的兜底射程。</summary>
	[Export] public float Length { get; set; } = 8f;

	/// <summary>**半径**（米）：② / ③ 的圈（调用方传 0 时用它）。</summary>
	[Export] public float Radius { get; set; } = 2.5f;

	/// <summary>**贴地偏移**（米）：圈 / 矩形离地面的高度（避免 z-fighting 与埋进地面）。</summary>
	[Export] public float GroundOffset { get; set; } = 0.05f;

	/// <summary>④ 矩形描边的粗细（米）。</summary>
	[Export] public float EdgeWidth { get; set; } = 0.14f;

	/// <summary>① 曲线的采样段数（传给 <see cref="DashPathIndicator.SampleParabola"/>，越大越圆滑）。</summary>
	[Export(PropertyHint.Range, "4,128,1")] public int CurveSteps { get; set; } = 32;

	[ExportGroup("外观细节")]
	/// <summary>圈的环**纵向压扁**比例（1 = 正圆管，0.3 = 贴地薄环；薄环从第三人称的浅角度看得更清）。</summary>
	[Export(PropertyHint.Range, "0.05,1,0.01")] public float RingFlatten { get; set; } = 0.3f;

	/// <summary>圈内填充盘相对 <see cref="Alpha"/> 的透明度比例（0 = 只要环，不要填充）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float FillAlphaScale { get; set; } = 0.35f;

	/// <summary>自发光强度（颜色只由导出色决定，不看场景光照）。</summary>
	[Export] public float EmissionEnergy { get; set; } = 1.8f;

	[ExportGroup("示警呼吸（Pulse）")]
	/// <summary>**呼吸 / 闪烁**开关（示警用：敌人红圈一直亮着太扎眼，呼吸一下更像"危险提示"）。</summary>
	[Export] public bool Pulse { get; set; } = true;

	/// <summary>呼吸周期（秒）。</summary>
	[Export] public float PulsePeriod { get; set; } = 1.2f;

	/// <summary>呼吸的最暗处相对最亮处的差额（0 = 不呼吸，1 = 完全灭到透明）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PulseDepth { get; set; } = 0.35f;

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在显示着吗（<see cref="HideAll"/> 之后为 <c>false</c>）。</summary>
	public bool IsShowing { get; private set; }

	/// <summary>最近一次**实际画出来的**样式（可能与 <see cref="Style"/> 不同：以调用为准）。</summary>
	public IndicatorStyle CurrentStyle { get; private set; }

	/// <summary>最近一次曲线采样出几个点（复用采样器的取证量）。</summary>
	public int LastSamplePoints { get; private set; }

	/// <summary>最近一次圈 / 曲线的落点半径（米；① 不带圈时为 0）。</summary>
	public float LastRadius { get; private set; }
	/// <summary>实际贴地后的圆心或矩形起点，以及本次矩形几何；供接入方核对预警。</summary>
	public Vector3 LastWorldPoint { get; private set; }
	public Vector3 LastDirection { get; private set; }
	public float LastLength { get; private set; }
	public float LastWidth { get; private set; }

	/// <summary>最近一次"是谁让指示器出现的"（日志与探针用，一行说明）。</summary>
	public string LastSource { get; private set; } = "（还没画过）";

	// ───────────────────────── 预制体节点（Scenes/Combat/SkillIndicator.tscn） ─────────────────────────

	private Node3D? _drawRoot;
	private MeshInstance3D? _curve;
	private ImmediateMesh? _curveMesh;
	private MeshInstance3D? _edge;
	private ImmediateMesh? _edgeMesh;
	private Node3D? _area;
	private MeshInstance3D? _ring;
	private MeshInstance3D? _fill;
	private MeshInstance3D? _lineFill;
	private StandardMaterial3D? _bandMaterial;
	private StandardMaterial3D? _ringMaterial;
	private StandardMaterial3D? _fillMaterial;
	private StandardMaterial3D? _lineMaterial;

	private float _pulseTime;
	private IndicatorStyle? _loggedStyle;

	/// <summary>解析出来的实际颜色：<see cref="StyleColor"/> 有 alpha 就用它，否则按阵营取默认两套色。</summary>
	public Color ResolvedColor => StyleColor.A > 0.001f
		? StyleColor
		: IsHostile ? HostileColor : PlayerColor;

	public override void _Ready()
	{
		_drawRoot = GetNodeOrNull<Node3D>("DrawRoot");
		if (_drawRoot != null)
		{
			// **世界坐标绘制**：DrawRoot 不继承父节点变换 → 子节点坐标 == 世界坐标。
			// 组件挂到哪（CurrentScene / 玩家 / 敌人）都画在同一个世界位置上；
			// 也就绕开了 HANDOFF §6 第 7 条（_Ready 里改自己 GlobalPosition 会被生成方覆盖）。
			_drawRoot.TopLevel = true;
			_drawRoot.Transform = Transform3D.Identity;
		}

		_curve = _drawRoot?.GetNodeOrNull<MeshInstance3D>("Curve");
		_curveMesh = _curve?.Mesh as ImmediateMesh;
		_edge = _drawRoot?.GetNodeOrNull<MeshInstance3D>("Edge");
		_edgeMesh = _edge?.Mesh as ImmediateMesh;
		_area = _drawRoot?.GetNodeOrNull<Node3D>("Area");
		_ring = _area?.GetNodeOrNull<MeshInstance3D>("Ring");
		_fill = _area?.GetNodeOrNull<MeshInstance3D>("Fill");
		_lineFill = _drawRoot?.GetNodeOrNull<MeshInstance3D>("LineFill");

		// 预制体里的材质是**模板**：同一份场景的多个实例共享同一个 Resource，
		// 所以每个实例自己复制一份 —— 敌人红圈与玩家青圈同时在场时互不染色。
		_bandMaterial = OwnMaterial(_curve);
		_ringMaterial = OwnMaterial(_ring);
		_fillMaterial = OwnMaterial(_fill);
		_lineMaterial = OwnMaterial(_lineFill);
		if (_edge != null && _bandMaterial != null)
		{
			_edge.MaterialOverride = _bandMaterial;   // 描边与曲线带同色同一份材质
		}

		HideAll();
	}

	// ───────────────────────── 运行期接口（只读 / 只画，不碰战斗逻辑） ─────────────────────────

	/// <summary>
	/// **① 曲线路径**：把一条抛物线（子弹 / 投掷物）画成半透明、有 <see cref="Width"/> 宽的带。
	///
	/// <para>时间轴由"**落回发射高度**"与"**水平飞满 <paramref name="maxDistance"/>**"两者中先到的那个决定：
	/// 垂直方向 <c>t = 2·vY/g</c>（初速朝下时退化为按距离截断），水平方向 <c>t = maxDistance / 水平速度</c>。</para>
	///
	/// <para>⚠️ 采样**复用** <see cref="DashPathIndicator.SampleParabola"/>（同一个抛物面公式），
	/// 本类不重写采样。</para>
	/// </summary>
	/// <param name="origin">起点（世界坐标；通常 = 角色胸口 + 朝向一点）。</param>
	/// <param name="startVelocity">初速矢量（米/秒，Y 朝上为正）。</param>
	/// <param name="gravity">重力**大小**（米/秒²，正数；传负数按绝对值处理）。</param>
	/// <param name="maxDistance">最远水平距离（米）；≤0 用 <see cref="Length"/>。</param>
	public void ShowCurvePath(Vector3 origin, Vector3 startVelocity, float gravity, float maxDistance)
		=> ShowCurve(origin, startVelocity, gravity, maxDistance, 0f);

	/// <summary>
	/// **② 区域指向**：在 <paramref name="worldPos"/> 的地面上画一个半径 <paramref name="radius"/> 的圆圈。
	///
	/// <para>敌人用法：把**落点**算好传进来（跳跃落地类技能的着陆点）；玩家用法见
	/// <see cref="ShowAreaPointAtCrosshair"/>。这里会先把点**压到地面**（只撞 World 层的一条短射线），
	/// 所以传"空中的落点"也不会画出悬空的圈。</para>
	/// </summary>
	/// <param name="worldPos">圆心（世界坐标）。</param>
	/// <param name="radius">半径（米）；≤0 用 <see cref="Radius"/>。</param>
	public void ShowAreaPoint(Vector3 worldPos, float radius)
	{
		Begin(IndicatorStyle.AreaPoint);
		PlaceArea(worldPos, radius > 0f ? radius : Radius);
		Complete($"区域指向：圆心 {worldPos} 半径 {LastRadius:0.##}m（贴地 {GroundOffset:0.##}m）");
	}

	/// <summary>
	/// **② 的"准星指哪就是哪"**（玩家用法）：从**相机屏幕中心**打一条射线求世界点，再在那里画圈。
	///
	/// <para>求点直接用现成工具 <see cref="AimUtil.GetCrosshairPoint"/>（P3 做的，
	/// 与 <c>PlayerWeaponInput</c> / <c>EnergyWaveAbility</c> / <c>HawkEyeAbility</c> **同一个函数**），
	/// 本类不自己拼 <c>ProjectRayOrigin</c>；射线打空时它退回"最大距离处"，圈照样会出来。</para>
	/// </summary>
	/// <param name="owner">射线要排除的自己人节点（通常传玩家 <c>Unit</c>）；能给 null。</param>
	/// <param name="maxDistance">射线最长距离（米）；≤0 用 60。</param>
	/// <param name="radius">半径（米）；≤0 用 <see cref="Radius"/>。</param>
	public void ShowAreaPointAtCrosshair(Node3D? owner, float maxDistance, float radius)
	{
		float reach = maxDistance > 0f ? maxDistance : 60f;
		Node3D rayOwner = owner ?? this;
		Vector3 point = AimUtil.GetCrosshairPoint(GetViewport(), rayOwner, reach, CombatLayers.World | CombatLayers.UnitMask);
		ShowAreaPoint(point, radius);
		LastSource += $"｜准星=AimUtil.GetCrosshairPoint（{reach:0.##}m，层 World|Unit）";
	}

	/// <summary>**③ 曲线 + 末端圈**（玩家限定）：① 的曲线，末端再画一个 ② 的圈。</summary>
	/// <param name="origin">起点（世界坐标）。</param>
	/// <param name="startVelocity">初速矢量（米/秒）。</param>
	/// <param name="gravity">重力大小（米/秒²，正数）。</param>
	/// <param name="maxDistance">最远水平距离（米）；≤0 用 <see cref="Length"/>。</param>
	/// <param name="radius">末端圈半径（米）；≤0 用 <see cref="Radius"/>。</param>
	public void ShowCurvePathPlusArea(Vector3 origin, Vector3 startVelocity, float gravity, float maxDistance, float radius)
		=> ShowCurve(origin, startVelocity, gravity, maxDistance, radius > 0f ? radius : Radius);

	/// <summary>
	/// **④ 直线区域**：从 <paramref name="origin"/> 沿 <paramref name="direction"/> 铺一个
	/// <paramref name="length"/> × <paramref name="width"/> 的**地面矩形**（敌人冲撞 / 大范围直线伤害）。
	///
	/// <para>⚠️ 方向会先**贴地投影**：<c>direction</c> 的 Y 分量被丢掉再归一化 ——
	/// 所以哪怕上游给的是"准星射线方向"（带俯角），画出来的也是**地面上的一条矩形**，不是悬空的斜盒子。
	/// 方向完全竖直（水平投影为 0）时退回 <c>-Z</c>。</para>
	/// </summary>
	/// <param name="origin">起点（世界坐标；该点的地面上）。</param>
	/// <param name="direction">冲出方向（无需归一化；Y 分量会被丢掉）。</param>
	/// <param name="length">长度（米）；≤0 用 <see cref="Length"/>。</param>
	/// <param name="width">宽度（米）；≤0 用 <see cref="Width"/>。</param>
	public void ShowLineArea(Vector3 origin, Vector3 direction, float length, float width)
	{
		Begin(IndicatorStyle.LineArea);
		float len = length > 0f ? length : Length;
		float wide = width > 0f ? width : Width;

		Vector3 flat = new(direction.X, 0f, direction.Z);
		if (flat.LengthSquared() < 0.0001f)
		{
			flat = Vector3.Forward;   // 水平投影为 0（方向竖直）→ 没有"地面前方"，退回 -Z
		}

		flat = flat.Normalized();
		Vector3 start = SnapToGround(origin) + (Vector3.Up * GroundOffset);
		LastWorldPoint = start; LastDirection = flat; LastLength = len; LastWidth = wide;
		Vector3 forward = flat * len;
		Vector3 half = new Vector3(-flat.Z, 0f, flat.X) * (wide * 0.5f);   // 地面上的右手垂直

		if (_lineFill != null)
		{
			_lineFill.Visible = true;
			_lineFill.Position = start + (forward * 0.5f);
			_lineFill.Rotation = new Vector3(0f, Mathf.Atan2(flat.X, flat.Z), 0f);
			_lineFill.Scale = new Vector3(wide, 1f, len);
		}

		// 四条边：用"贴地朝向"的带宽描边（与曲线共用同一个 FillBand），抬高 2cm 避免与填充面 z-fighting。
		Vector3 lift = Vector3.Up * 0.02f;
		var outline = new List<Vector3>
		{
			start - half + lift,
			start + half + lift,
			start + half + forward + lift,
			start - half + forward + lift,
			start - half + lift,
		};

		FillBand(_edgeMesh, _edge, outline, EdgeWidth, Vector3.Up, _bandMaterial);
		Complete($"直线区域：起点 {start} 方向 {flat} 长 {len:0.##}m 宽 {wide:0.##}m（贴地投影）");
	}

	/// <summary>隐藏全部（技能收招 / 示警结束时调用）。不显示时每帧开销为零。</summary>
	public void HideAll()
	{
		IsShowing = false;
		SetProcess(false);
		if (_curve != null)
		{
			_curve.Visible = false;
		}

		_curveMesh?.ClearSurfaces();
		if (_edge != null)
		{
			_edge.Visible = false;
		}

		_edgeMesh?.ClearSurfaces();
		if (_area != null)
		{
			_area.Visible = false;
		}

		if (_lineFill != null)
		{
			_lineFill.Visible = false;
		}
	}

	// ───────────────────────── 呼吸（示警用） ─────────────────────────

	public override void _Process(double delta)
	{
		if (!IsShowing)
		{
			return;
		}

		_pulseTime += (float)delta;
		ApplyTint(PulseScale());
	}

	/// <summary>当前呼吸系数（1 = 最亮，<c>1 - PulseDepth</c> = 最暗；关掉呼吸恒为 1）。</summary>
	private float PulseScale()
	{
		if (!Pulse || PulsePeriod <= 0.01f)
		{
			return 1f;
		}

		float phase = 0.5f + (0.5f * Mathf.Sin(_pulseTime * Mathf.Pi * 2f / PulsePeriod));
		return 1f - (Mathf.Clamp(PulseDepth, 0f, 1f) * (1f - phase));
	}

	// ───────────────────────── 画法（① / ③ 的曲线） ─────────────────────────

	/// <summary>①/③ 的共用一个实现：先采样曲线，再决定末端要不要一个圈。</summary>
	private void ShowCurve(Vector3 origin, Vector3 startVelocity, float gravity, float maxDistance, float endRadius)
	{
		bool plusArea = endRadius > 0f;
		Begin(plusArea ? IndicatorStyle.CurvePathPlusArea : IndicatorStyle.CurvePath);

		float g = Mathf.Max(0.01f, Mathf.Abs(gravity));
		float limit = maxDistance > 0f ? maxDistance : Length;
		List<Vector3> points = SampleBallistic(origin, startVelocity, g, limit);

		FillBand(_curveMesh, _curve, points, Width, null, _bandMaterial);
		LastSamplePoints = points.Count;

		if (points.Count < 2)
		{
			Complete($"曲线：初速 {startVelocity} 重力 {g:0.##} 最远 {limit:0.##}m 解不出飞行时间 → 没画");
			return;
		}

		if (plusArea)
		{
			PlaceArea(points[^1], endRadius);
		}

		Complete($"曲线{(plusArea ? "+末端圈" : string.Empty)}：{points.Count} 点（复用 DashPathIndicator.SampleParabola）"
			+ $" 初速 {startVelocity} 重力 {g:0.##} 最远 {limit:0.##}m 宽 {Width:0.##}m"
			+ (plusArea ? $" 末圈半径 {LastRadius:0.##}m" : string.Empty));
	}

	/// <summary>
	/// **抛物线采样**：时间轴 = min(落回发射高度的时间, 水平飞满 <paramref name="maxDistance"/> 的时间)，
	/// 然后整段交给 <see cref="DashPathIndicator.SampleParabola"/>（**复用，不重写**）。
	/// </summary>
	private List<Vector3> SampleBallistic(Vector3 origin, Vector3 startVelocity, float gravity, float maxDistance)
	{
		float horizontalSpeed = new Vector2(startVelocity.X, startVelocity.Z).Length();
		float flight = startVelocity.Y > 0.001f ? 2f * startVelocity.Y / gravity : 0f;
		if (horizontalSpeed > 0.001f && maxDistance > 0f)
		{
			float byDistance = maxDistance / horizontalSpeed;
			flight = flight > 0f ? Mathf.Min(flight, byDistance) : byDistance;
		}

		if (flight <= 0.001f)
		{
			return new List<Vector3>();
		}

		return DashPathIndicator.SampleParabola(
			origin,
			startVelocity,
			Vector3.Down * gravity,
			flight,
			Mathf.Max(2, CurveSteps));
	}

	// ───────────────────────── 画法（② / ③ 的圈、④ 的矩形） ─────────────────────────

	/// <summary>把一个圈摆到 <paramref name="worldPos"/> 的地面上（环 + 填充盘）。</summary>
	private void PlaceArea(Vector3 worldPos, float radius)
	{
		float r = Mathf.Max(0.1f, radius);
		LastRadius = r;
		Vector3 ground = SnapToGround(worldPos) + (Vector3.Up * GroundOffset);
		LastWorldPoint = ground;

		if (_area != null)
		{
			_area.Visible = true;
		}

		if (_ring != null)
		{
			_ring.Position = ground;
			_ring.Scale = new Vector3(r, r * Mathf.Clamp(RingFlatten, 0.05f, 1f), r);
		}

		if (_fill != null)
		{
			_fill.Position = ground;
			_fill.Scale = new Vector3(r, 1f, r);
		}
	}

	/// <summary>
	/// 把点压到地面上：从点上方 2m 往下 60m 打一条**只撞 World 层**的射线。
	/// 「敌人的落点」与「玩家的准星」两种用法都要贴地，所以统一在这里收口。
	/// </summary>
	private Vector3 SnapToGround(Vector3 point)
	{
		if (!IsInsideTree())
		{
			return point;
		}

		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(point + (Vector3.Up * 2f), point + (Vector3.Down * 60f), CombatLayers.World);
		Godot.Collections.Dictionary hit = space.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector3() : point;
	}

	/// <summary>
	/// **带宽/描边**的唯一实现：把折线画成一串四边形（每段一个四边形）。
	///
	/// <para><paramref name="flatFacing"/> 为 <c>null</c> → **面向镜头**（宽度方向 =
	/// <c>cross(段方向, 到镜头的方向)</c>）—— 这是 `docs/vfx.md` §4 里"拖尾做竖直的带"那条坑的正确做法：
	/// 曲线的带如果躺平或立在同一平面，从身后看只剩 1px。给 <see cref="Vector3.Up"/> → 带宽躺在水平面里
	/// （④ 的地面描边用它）。</para>
	/// </summary>
	private void FillBand(ImmediateMesh? mesh, MeshInstance3D? instance, IReadOnlyList<Vector3> points, float width, Vector3? flatFacing, Material? material)
	{
		if (mesh == null || instance == null)
		{
			return;
		}

		mesh.ClearSurfaces();
		if (points == null || points.Count < 2)
		{
			instance.Visible = false;
			return;
		}

		float half = Mathf.Max(0.01f, width) * 0.5f;
		Camera3D? camera = flatFacing == null ? GetViewport()?.GetCamera3D() : null;

		mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
		for (int i = 0; i + 1 < points.Count; i++)
		{
			Vector3 a = points[i];
			Vector3 b = points[i + 1];
			Vector3 segment = b - a;
			if (segment.LengthSquared() < 0.000001f)
			{
				continue;
			}

			Vector3 unit = segment.Normalized();
			Vector3 facing = flatFacing ?? (camera != null ? camera.GlobalPosition - ((a + b) * 0.5f) : Vector3.Up);
			Vector3 side = unit.Cross(facing);
			if (side.LengthSquared() < 0.000001f)
			{
				side = unit.Cross(Vector3.Right);
			}

			if (side.LengthSquared() < 0.000001f)
			{
				continue;
			}

			side = side.Normalized() * half;
			mesh.SurfaceAddVertex(a - side);
			mesh.SurfaceAddVertex(a + side);
			mesh.SurfaceAddVertex(b + side);
			mesh.SurfaceAddVertex(a - side);
			mesh.SurfaceAddVertex(b + side);
			mesh.SurfaceAddVertex(b - side);
		}

		mesh.SurfaceEnd();
		instance.Visible = true;
	}

	// ───────────────────────── 收尾 / 上色 ─────────────────────────

	/// <summary>每次 Show 之前先把上一次的画法清干净，再记下"这一次画什么"。</summary>
	private void Begin(IndicatorStyle style)
	{
		bool continuing = IsShowing && CurrentStyle == style;
		float previousPulse = _pulseTime;
		HideAll();
		CurrentStyle = style;
		LastRadius = 0f;
		LastSamplePoints = 0;
		_pulseTime = continuing ? previousPulse : 0f;
	}

	/// <summary>收尾：上色、开呼吸、写日志（同一个样式只在第一次出现时记一行，长按预览不会刷屏）。</summary>
	private void Complete(string source)
	{
		IsShowing = true;
		Style = CurrentStyle;   // 导出的 Style 与实际画法对齐（编辑器 / 探针里读得到）
		LastSource = source;
		ApplyTint(PulseScale());
		SetProcess(Pulse && PulsePeriod > 0.01f);

		if (_loggedStyle != CurrentStyle)
		{
			_loggedStyle = CurrentStyle;
			GameLog.Write("Combat", $"技能指示器：{StyleName(CurrentStyle)}（{source}；纯表现 无碰撞 无单位 无分组）");
		}
	}

	/// <summary>把"颜色 + 透明度 + 呼吸"落到四份材质上（**只改材质，不碰任何战斗状态**）。</summary>
	private void ApplyTint(float pulse)
	{
		Color baseColor = ResolvedColor;
		float alpha = Mathf.Clamp(Alpha, 0f, 1f) * Mathf.Clamp(pulse, 0f, 1f) * baseColor.A;
		Color tint = new(baseColor.R, baseColor.G, baseColor.B, alpha);

		if (_bandMaterial != null)
		{
			Tint(_bandMaterial, tint, 1f);
		}

		if (_ringMaterial != null)
		{
			Tint(_ringMaterial, tint, 1f);
		}

		if (_fillMaterial != null)
		{
			Tint(_fillMaterial, tint, FillAlphaScale);
		}

		if (_lineMaterial != null)
		{
			Tint(_lineMaterial, tint, FillAlphaScale);
		}
	}

	private void Tint(StandardMaterial3D material, Color tint, float alphaScale)
	{
		Color color = tint;
		color.A = Mathf.Clamp(tint.A * alphaScale, 0f, 1f);
		material.AlbedoColor = color;
		material.Emission = new Color(tint.R, tint.G, tint.B, 1f);
		material.EmissionEnergyMultiplier = EmissionEnergy;
	}

	/// <summary>预制体里的材质是模板：复制一份给本实例专用（同场景多实例互不染色）。</summary>
	private static StandardMaterial3D? OwnMaterial(MeshInstance3D? mesh)
	{
		if (mesh?.MaterialOverride is StandardMaterial3D template)
		{
			var copy = (StandardMaterial3D)template.Duplicate();
			mesh.MaterialOverride = copy;
			return copy;
		}

		return null;
	}

	private static string StyleName(IndicatorStyle style) => style switch
	{
		IndicatorStyle.CurvePath => "① 曲线路径",
		IndicatorStyle.AreaPoint => "② 区域指向",
		IndicatorStyle.CurvePathPlusArea => "③ 曲线 + 区域",
		_ => "④ 直线区域",
	};

	// ───────────────────────── 探针（DebugState / 自测取证） ─────────────────────────

	/// <summary>
	/// 探针用：一行的实际状态 —— 画成什么样 + 读到的入参 + **三项安全性取证**（现算，不是写死的字面量）。
	/// 拍摄台词与自测共用这一份判据。
	/// </summary>
	public string DebugState()
	{
		int colliders = 0;
		int units = 0;
		var grouped = new List<string>();
		CountDangerous(this, ref colliders, ref units, grouped);

		return $"技能指示器：样式={StyleName(CurrentStyle)}（导出 {StyleName(Style)}） 显示={IsShowing}"
			+ $"｜最近一次={LastSource}"
			+ $"｜参数 阵营={(IsHostile ? "敌人（红）" : "玩家（青蓝）")} 色={ResolvedColor} alpha={Alpha:0.##}"
			+ $" 宽={Width:0.##} 长={Length:0.##} 半径={Radius:0.##}（本次 {LastRadius:0.##}） 贴地={GroundOffset:0.##}"
			+ $" 描边={EdgeWidth:0.##} 采样段={CurveSteps}（本次 {LastSamplePoints} 点）"
			+ $"｜呼吸={(Pulse ? $"开（{PulsePeriod:0.##}s 深 {PulseDepth:0.##}，当前 {PulseScale():0.##}）" : "关")}"
			+ $"｜复用 采样器=DashPathIndicator.SampleParabola 准星=AimUtil.GetCrosshairPoint"
			+ $"｜安全性 碰撞体={colliders}（必须 0） 单位={units}（必须 0）"
			+ $" 子树分组={(grouped.Count == 0 ? "（无）" : string.Join(",", grouped))}（必须无）"
			+ $" 自身分组={GetGroups().Count}（必须 0）";
	}

	private static void CountDangerous(Node node, ref int colliders, ref int units, List<string> grouped)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is CollisionObject3D)
			{
				colliders++;
			}

			if (child is Unit)
			{
				units++;
			}

			if (child.GetGroups().Count > 0)
			{
				grouped.Add(child.Name);
			}

			CountDangerous(child, ref colliders, ref units, grouped);
		}
	}
}
