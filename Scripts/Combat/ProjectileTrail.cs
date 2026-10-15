using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **剑气拖尾**（P6c 表现层；设计文档 `角色设计.txt` 第 110 / 114 行，灵剑 武技2「拔刀式」）：
///
/// <blockquote>
/// 「武技2[拔刀式]{cd:2}:基础蓄力{1}秒后朝准星方向发射**一发剑气**，对路径上的敌人造成伤害;」
/// 「剑气:能穿透路径上的敌人，但**飞行路径短**，可被格挡，剑气大小会等比攻击盒大小;」
/// </blockquote>
///
/// <para><b>挂在剑气本体上</b>（不是另做一个）：<c>Scenes/Combat/SwordQi.tscn</c> 已经是剑气本体
/// （<see cref="Projectile"/> + <c>Blade</c>/<c>Tail</c> 两个 <c>MeshInstance3D</c>），本节点是它的
/// <b>子节点</b>，每物理帧记一个"剑气当时在哪"，然后把最近 <see cref="Lifetime"/> 秒的采样点
/// 连成一条**带**（<see cref="ImmediateMesh"/> 三角形带，零美术资源、零预制体）。</para>
///
/// <para><b>宽度为什么在局部空间里画</b>：剑气的整体尺寸是 <c>ProjectileSizeScale</c>
/// （"剑气大小会**等比攻击盒大小**"）—— 采样点是世界坐标，用 <c>ToLocal</c> 换回本节点的局部空间，
/// 于是父节点的缩放会**自动**作用在拖尾上：攻击盒大的那一段剑气，拖尾也一起变宽。一条代码不分叉。</para>
///
/// <para>⚠️ <b>纯表现</b>：只有一个 <c>MeshInstance3D + ImmediateMesh</c>，**没有**
/// <c>CollisionObject3D</c>、不进任何分组、不参与命中（命中判定在 <see cref="Projectile"/> 自己的
/// 射线里，本类一行都没碰）。剑气被回收时拖尾跟着一起回收（它是子节点）。</para>
///
/// <para>所有几何量 / 颜色 / 时长都是 <c>[Export]</c>。</para>
/// </summary>
public partial class ProjectileTrail : Node3D
{
	[ExportGroup("采样")]
	/// <summary>采样间隔（秒）；≤0 = 每个物理帧都采一个点。</summary>
	[Export] public float SampleInterval { get; set; }

	/// <summary>一个采样点活多久（秒）—— 拖尾的长度就是 `弹速 × 它`。</summary>
	[Export] public float Lifetime { get; set; } = 0.18f;

	/// <summary>最多记多少个点（保险丝；按 <c>Lifetime / 物理帧</c> 估一个够用的数）。</summary>
	[Export(PropertyHint.Range, "2,128,1")] public int MaxPoints { get; set; } = 32;

	[ExportGroup("几何（局部空间；父节点的缩放会等比作用上去）")]
	/// <summary>**尾部**处的半宽（米，局部）。</summary>
	[Export] public float WidthTail { get; set; } = 0.05f;

	/// <summary>**头部**（剑气本体）处的半宽（米，局部）—— 与刀身高度同量级即可。</summary>
	[Export] public float WidthHead { get; set; } = 0.5f;

	/// <summary>宽度从尾到头怎么长：<c>1</c> = 线性；<c>&gt;1</c> = 大部分宽度集中在头部（更像刀光）。</summary>
	[Export(PropertyHint.Range, "0.2,4,0.1")] public float WidthCurve { get; set; } = 1.5f;

	/// <summary>拖尾离剑气中心往前多伸一点（米，局部；0 = 从剑气中心开始）。</summary>
	[Export] public float HeadExtend { get; set; } = 0.35f;

	[ExportGroup("配色")]
	/// <summary>尾部颜色（alpha 也在这里）。</summary>
	[Export] public Color TailColor { get; set; } = new(0.42f, 0.8f, 1f, 0f);

	/// <summary>头部颜色（alpha 也在这里）。</summary>
	[Export] public Color HeadColor { get; set; } = new(0.78f, 0.95f, 1f, 0.85f);

	/// <summary>自发光强度（不受光，竞技场再亮也看得清）。</summary>
	[Export] public float EmissionEnergy { get; set; } = 2.2f;

	/// <summary>加法混合（刀光/能量感）。关掉就是普通半透明。</summary>
	[Export] public bool AdditiveBlend { get; set; } = true;

	[ExportGroup("运行期可读状态（探针 / 自测）")]
	/// <summary>现在记着几个采样点。</summary>
	public int PointCount { get; private set; }

	/// <summary>已经画出来了吗（探针用）。</summary>
	public bool TrailVisible { get; private set; }

	/// <summary>本帧采样头尾的间距（米；探针用：0 = 剑气没在动，所以没拖尾）。</summary>
	public float SampleSpan { get; private set; }

	private readonly List<Vector3> _points = new();   // 世界坐标，[0] = 最新
	private readonly List<float> _ages = new();
	private MeshInstance3D? _mesh;
	private ImmediateMesh? _geometry;
	private StandardMaterial3D? _material;
	private float _sampleTimer;

	private Projectile? _projectile;

	public override void _Ready()
	{
		_projectile = GetParentOrNull<Projectile>();
		EnsureNodes();
	}

	/// <summary>本节点（剑气本体）现在有没有在飞 —— 没发射时 <see cref="Projectile.VelocityDirection"/> 是零向量。</summary>
	private bool Launching => _projectile != null && IsInstanceValid(_projectile)
		&& _projectile.VelocityDirection.LengthSquared() > 0.0001f;

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// 老化 + 丢掉过期的点。
		for (int i = _ages.Count - 1; i >= 0; i--)
		{
			_ages[i] += dt;
			if (_ages[i] > Lifetime)
			{
				_ages.RemoveAt(i);
				_points.RemoveAt(i);
			}
		}

		if (Launching)
		{
			bool due = SampleInterval <= 0f;
			if (!due)
			{
				_sampleTimer -= dt;
				due = _sampleTimer <= 0f;
			}

			if (due)
			{
				_sampleTimer = SampleInterval;
				_points.Insert(0, GlobalPosition);
				_ages.Insert(0, 0f);
				while (_points.Count > Mathf.Max(2, MaxPoints))
				{
					_points.RemoveAt(_points.Count - 1);
					_ages.RemoveAt(_ages.Count - 1);
				}
			}
		}

		PointCount = _points.Count;
		SampleSpan = _points.Count >= 2 ? _points[0].DistanceTo(_points[_points.Count - 1]) : 0f;
		Rebuild();
	}

	/// <summary>把采样点连成一条三角形带（局部空间 + 逐顶点颜色）。</summary>
	private void Rebuild()
	{
		if (_geometry == null || _mesh == null)
		{
			return;
		}

		_geometry.ClearSurfaces();
		if (_points.Count < 2 || SampleSpan <= 0.0005f)
		{
			TrailVisible = false;
			_mesh.Visible = false;
			return;
		}

		// 头部先往前伸一点（拖尾从刀尖长出来，而不是从剑气中心）。
		Vector3 headWorld = _points[0] + (Forward() * HeadExtend);
		var local = new List<Vector3>(_points.Count + 1) { ToLocal(headWorld) };
		for (int i = 0; i < _points.Count; i++)
		{
			local.Add(ToLocal(_points[i]));
		}

		var ages = new List<float>(_ages.Count + 1) { 0f };
		ages.AddRange(_ages);
		float newest = Mathf.Max(Lifetime, 0.0001f);

		_geometry.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _material);
		for (int i = 0; i < local.Count; i++)
		{
			// "上"方向：本节点局部 Y 去掉沿段方向的分量 → 一条竖直的带（与刀身同向）。
			Vector3 along = i == 0
				? (local[Mathf.Min(1, local.Count - 1)] - local[0])
				: local[i] - local[i - 1];
			Vector3 side = Perpendicular(along);

			// `tail` = 0 在头（最新采样点）、1 在尾（最老）。宽度与颜色都按它插值：
			// 头 = WidthHead/HeadColor，尾 = WidthTail/TailColor。
			float tail = 1f - Mathf.Clamp(ages[i] / newest, 0f, 1f);
			float width = Mathf.Lerp(WidthHead, WidthTail, Mathf.Pow(tail, Mathf.Max(0.2f, WidthCurve)));
			Color color = HeadColor.Lerp(TailColor, tail);

			// TriangleStrip：每个采样点贡献左右两个顶点 —— 顺序必须一致（同一侧先）。
			_geometry.SurfaceSetColor(color);
			_geometry.SurfaceAddVertex(local[i] - (side * width));
			_geometry.SurfaceSetColor(color);
			_geometry.SurfaceAddVertex(local[i] + (side * width));
		}

		_geometry.SurfaceEnd();
		TrailVisible = true;
		_mesh.Visible = true;
	}

	/// <summary>垂直方向：局部 Y 去掉沿段方向的分量（退化时退回局部 X）。</summary>
	private Vector3 Perpendicular(Vector3 along)
	{
		Vector3 up = Vector3.Up;
		if (along.LengthSquared() > 0.0000001f)
		{
			Vector3 dir = along.Normalized();
			up -= dir * up.Dot(dir);
		}

		return up.LengthSquared() > 0.0001f ? up.Normalized() : Vector3.Right;
	}

	/// <summary>剑气当前的朝向（局部 -Z 指向飞行方向，与 <see cref="Projectile"/> 的 <c>LookAt</c> 一致）。</summary>
	private Vector3 Forward()
	{
		Vector3 forward = -GlobalTransform.Basis.Z;
		return forward.LengthSquared() > 0.0001f ? forward.Normalized() : Vector3.Forward;
	}

	private void EnsureNodes()
	{
		if (_mesh != null)
		{
			return;
		}

		_geometry = new ImmediateMesh();
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = AdditiveBlend ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			EmissionEnabled = true,
			Emission = Colors.White,
			EmissionEnergyMultiplier = EmissionEnergy,
			DisableReceiveShadows = true,
		};

		_mesh = new MeshInstance3D
		{
			Name = "Ribbon",
			Mesh = _geometry,
			Visible = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = _material,
		};

		AddChild(_mesh);
	}

	/// <summary>探针用：一行的实际状态（截图与自检共用一份判据）。</summary>
	public string DebugState()
		=> $"剑气拖尾：可见={TrailVisible} 采样点={PointCount} 头尾间距={SampleSpan:0.###}m"
			+ $"（存活 {Lifetime:0.###}s 头半宽 {WidthHead:0.##} 尾半宽 {WidthTail:0.##} 曲线 {WidthCurve:0.#} 前伸 {HeadExtend:0.##}）"
			+ $"｜只读源 投射物={(IsInstanceValid(_projectile) ? _projectile!.SourceSceneName : "（没有父投射物）")}"
			+ $" 在飞={Launching}"
			+ $"｜纯表现 网格子节点={(_mesh != null ? 1 : 0)} 碰撞体=0 分组=0";
}
