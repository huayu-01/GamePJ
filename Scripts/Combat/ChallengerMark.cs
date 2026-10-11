using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 「挑战者」状态的地面标记（用户要求的表现）：
/// 角色脚底一圈三角形，每个三角形有一个角指向中心的角色；
/// 挑战期间常驻，挑战结束时消融并炸开消失。
///
/// 状态由关卡管理器（StageController.ChallengerActive）持有，这里只负责表现 ——
/// 交互物那边也读同一个状态来决定"弹菜单还是拒绝"。
/// </summary>
public partial class ChallengerMark : Node3D
{
	/// <summary>环的半径。</summary>
	[Export] public float Radius { get; set; } = 1.15f;

	/// <summary>三角形数量。</summary>
	[Export] public int Segments { get; set; } = 6;

	/// <summary>离地高度。</summary>
	/// <summary>离地高度：单位原点在脚底，所以贴地值取一个很小的正数。
	/// 原来是 -0.85（认为原点在身体中心），结果整圈三角环埋在地面以下，玩家根本看不到。</summary>
	[Export] public float Height { get; set; } = 0.06f;

	/// <summary>出现 / 消失的时长。</summary>
	[Export] public float FadeSeconds { get; set; } = 0.25f;

	private MeshInstance3D _mesh = null!;
	private StandardMaterial3D _material = null!;
	private bool _active;
	private float _scale = 1f;
	private float _spin;

	public override void _Ready()
	{
		_mesh = new MeshInstance3D { Name = "Mark" };
		AddChild(_mesh);
		Position = new Vector3(0f, Height, 0f);

		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(1f, 0.75f, 0.25f, 0f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.6f, 0.15f),
		};

		_mesh.Mesh = BuildRing();
		_mesh.MaterialOverride = _material;
		_mesh.Visible = false;
	}

	/// <summary>用代码生成一圈三角形：每个三角形的尖角朝中心。</summary>
	private ArrayMesh BuildRing()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int i = 0; i < Segments; i++)
		{
			float angle = Mathf.Tau * i / Segments;
			var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
			var side = new Vector3(-dir.Z, 0f, dir.X);

			Vector3 tip = dir * (Radius * 0.52f);
			Vector3 b1 = (dir * Radius) + (side * (Radius * 0.26f));
			Vector3 b2 = (dir * Radius) - (side * (Radius * 0.26f));

			st.AddVertex(tip);
			st.AddVertex(b1);
			st.AddVertex(b2);
		}

		st.GenerateNormals();
		return st.Commit();
	}

	public override void _Process(double delta)
	{
		// 状态从关卡管理器读，避免各处重复接线。
		bool wanted = StageController.Instance is { ChallengerActive: true };
		if (wanted != _active)
		{
			_active = wanted;
			Play(wanted);
		}

		if (!_mesh.Visible)
		{
			return;
		}

		float dt = (float)delta;

		// 缓慢自转 + 轻微呼吸，让"被锁定"这件事看起来是活的。
		_spin += dt * 0.9f;
		RotateY(dt * 0.9f);
		float breath = 1f + (Mathf.Sin(_spin * 3f) * 0.04f);
		Scale = Vector3.One * (_scale * breath);
	}

	/// <summary>出现：放大淡入；消失：拉伸并炸开淡出。</summary>
	private void Play(bool appearing)
	{
		_mesh.Visible = true;
		Tween tween = CreateTween();
		tween.SetParallel(true);

		if (appearing)
		{
			_scale = 0.2f;
			_material.AlbedoColor = new Color(1f, 0.75f, 0.25f, 0f);
			tween.TweenMethod(Callable.From<float>(v => _scale = v), 0.2f, 1f, FadeSeconds);
			tween.TweenProperty(_material, "albedo_color:a", 0.85f, FadeSeconds);
		}
		else
		{
			_scale = 1f;
			tween.TweenMethod(Callable.From<float>(v => _scale = v), 1f, 1.9f, FadeSeconds * 1.6f);
			tween.TweenProperty(_material, "albedo_color:a", 0f, FadeSeconds * 1.6f);
			tween.Chain().TweenCallback(Callable.From(() =>
			{
				_mesh.Visible = false;
				_scale = 1f;
			}));
		}
	}
}
