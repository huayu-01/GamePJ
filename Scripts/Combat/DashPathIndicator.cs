using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// **一条可视化的路径**（P4 新增）—— "冲刺/抛掷的路径指示器"的唯一落点。
///
/// <para>文档要两条这样的线：角色 B 技能1「闪」的「**需要设立一条可视化的路径**」
/// （设计文档第 101 行）、长枪武技2「长虹贯日」的「路径呈现抛物线（**有路径可视化指示器**）」
/// （第 151 行）。两者都是"一串折线"，所以只有这一个类：喂给它**折线点列**即可。</para>
///
/// <para>实现用 <see cref="ImmediateMesh"/>（每帧重算顶点，零美术资源、零预制体）+
/// 一个纯色 <see cref="StandardMaterial3D"/>，材质参数全部来自 <see cref="Configure"/> 的入参
/// （**调用方从表里读**，本类不写死任何数值）。</para>
///
/// <para>⚠️ <b>纯表现</b>：不参与任何战斗结算，默认 <c>Visible = false</c>，
/// 不显示时每帧开销为零。</para>
/// </summary>
public partial class DashPathIndicator : Node3D
{
	private MeshInstance3D? _mesh;
	private ImmediateMesh? _geometry;
	private StandardMaterial3D? _material;

	/// <summary>现在显示着吗（自测 / UI）。</summary>
	public bool IsShowing { get; private set; }

	/// <summary>当前画了几个点（自测取证）。</summary>
	public int PointCount { get; private set; }

	/// <summary>
	/// 配置外观（**数值全部由调用方从表里传入**）。
	/// </summary>
	/// <param name="color">线的颜色。</param>
	/// <param name="width">线宽（米）；≤0 用 0.06。</param>
	/// <param name="groundOffset">离地抬升（米；避免 z-fighting）。</param>
	public void Configure(Color color, float width, float groundOffset)
	{
		EnsureNodes();
		if (_material == null)
		{
			return;
		}

		_material.AlbedoColor = color;
		_material.Emission = color;
		_material.EmissionEnergyMultiplier = 1.4f;
		GroundOffset = groundOffset;
		Width = width > 0f ? width : 0.06f;
	}

	/// <summary>线宽（米）。</summary>
	public float Width { get; private set; } = 0.06f;

	/// <summary>离地抬升（米）。</summary>
	public float GroundOffset { get; private set; } = 0.05f;

	/// <summary>
	/// 画一条折线（世界坐标点列，至少 2 个点）。<paramref name="points"/> 为空 / 少于 2 个点就隐藏。
	/// </summary>
	public void Show(IReadOnlyList<Vector3> points)
	{
		EnsureNodes();
		if (_geometry == null || _mesh == null)
		{
			return;
		}

		_geometry.ClearSurfaces();
		PointCount = points?.Count ?? 0;
		if (points == null || points.Count < 2)
		{
			IsShowing = false;
			_mesh.Visible = false;
			return;
		}

		_geometry.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _material);
		foreach (Vector3 point in points)
		{
			_geometry.SurfaceAddVertex(point + Vector3.Up * GroundOffset);
		}

		_geometry.SurfaceEnd();
		IsShowing = true;
		_mesh.Visible = true;
	}

	/// <summary>隐藏（技能收招时调用）。</summary>
	public void HidePath()
	{
		IsShowing = false;
		PointCount = 0;
		if (_mesh != null)
		{
			_mesh.Visible = false;
		}

		_geometry?.ClearSurfaces();
	}

	/// <summary>
	/// **把"起点 → 方向 × 距离"采样成折线**（冲刺用：直线 + 落地贴合）。
	/// </summary>
	/// <param name="origin">起点（世界坐标）。</param>
	/// <param name="direction">方向（无需归一化）。</param>
	/// <param name="distance">长度（米）。</param>
	/// <param name="steps">采样段数（≥1）。</param>
	public static List<Vector3> SampleLine(Vector3 origin, Vector3 direction, float distance, int steps)
	{
		var points = new List<Vector3>();
		if (direction.LengthSquared() < 0.0001f || distance <= 0f)
		{
			return points;
		}

		Vector3 unit = direction.Normalized();
		int count = Mathf.Max(2, steps + 1);
		for (int i = 0; i < count; i++)
		{
			points.Add(origin + (unit * (distance * i / (count - 1f))));
		}

		return points;
	}

	/// <summary>
	/// **抛物线采样**（长枪武技2 用）：<c>p(t) = origin + startVelocity·t + ½·gravity·t²</c>。
	/// 时间轴由 <paramref name="flightTime"/> 定，采样点从 0 到 <paramref name="flightTime"/>。
	/// </summary>
	public static List<Vector3> SampleParabola(Vector3 origin, Vector3 startVelocity, Vector3 gravity, float flightTime, int steps)
	{
		var points = new List<Vector3>();
		if (flightTime <= 0f)
		{
			return points;
		}

		int count = Mathf.Max(2, steps + 1);
		for (int i = 0; i < count; i++)
		{
			float t = flightTime * i / (count - 1f);
			points.Add(origin + (startVelocity * t) + (gravity * 0.5f * t * t));
		}

		return points;
	}

	/// <summary>
	/// **由初速与目标水平距离解出抛物线**（长枪武技2 的实际抛掷口径）：
	/// 给定水平距离与"飞行时间"求垂直初速 —— 于是"路径中线延长"= 提高水平初速、飞行时间不变，
	/// 抛物线的**形状**（落点相对发射点的高度）也随之变大，正是文档说的"路径中线延长 20%"。
	/// </summary>
	public static List<Vector3> SampleArcToDistance(Vector3 origin, Vector3 flatDirection, float gravityY, float horizontalSpeed, float flightTime, int steps)
	{
		var points = new List<Vector3>();
		float speed = Mathf.Max(0.01f, horizontalSpeed);
		float time = Mathf.Max(0.01f, flightTime);
		Vector3 flat = flatDirection;
		flat.Y = 0f;
		if (flat.LengthSquared() < 0.0001f)
		{
			flat = Vector3.Forward;
		}

		flat = flat.Normalized();
		// 垂直初速：让 t = flightTime 时回到起点高度（对称抛物线）。
		float vertical = -0.5f * gravityY * time;
		Vector3 velocity = (flat * speed) + (Vector3.Up * vertical);
		return SampleParabola(origin, velocity, Vector3.Up * gravityY, time, steps);
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
			AlbedoColor = new Color(0.6f, 0.9f, 1f, 0.85f),
			EmissionEnabled = true,
			Emission = new Color(0.6f, 0.9f, 1f),
			VertexColorUseAsAlbedo = false,
			NoDepthTest = false,
		};

		_mesh = new MeshInstance3D
		{
			Name = "Path",
			Mesh = _geometry,
			Visible = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		AddChild(_mesh);
		// 输入点已是世界坐标，不能再叠加角色的平移、旋转和缩放。
		_mesh.TopLevel = true;
		_mesh.GlobalTransform = Transform3D.Identity;
		_mesh.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
	}
}
