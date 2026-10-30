using ActRoguelike.Run;
using ActRoguelike.Audio;
using ActRoguelike.Visual;
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
	private StageController? _stage;
	private Unit? _unit;
	private Tween? _transition;
	public bool Active => _active;
	public GpuParticles3D? Particles { get; private set; }
	public int EntryCount { get; private set; }
	public int CompletionCount { get; private set; }

	public override void _Ready()
	{
		_unit = GetParentOrNull<Unit>();
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
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

		_mesh.Mesh = BuildRing();
		_mesh.MaterialOverride = _material;
		_mesh.Visible = false;
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(1, .65f, .16f, 0));
		ramp.SetColor(1, new Color(1, .9f, .45f, 0));
		ramp.AddPoint(.2f, new Color(1, .8f, .28f, .9f));
		ramp.AddPoint(.65f, new Color(1, .65f, .15f, .6f));
		Particles = new GpuParticles3D
		{
			Name = "ChallengerParticles", Amount = 48, Lifetime = 1.1, Emitting = false, Visible = false,
			Position = Vector3.Up * .85f, LocalCoords = true,
			VisibilityAabb = new Aabb(new Vector3(-2, -1, -2), new Vector3(4, 5, 4)),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = .55f,
				Direction = Vector3.Up, Spread = 45, Gravity = new Vector3(0, .35f, 0),
				InitialVelocityMin = .4f, InitialVelocityMax = 1.1f, ScaleMin = .35f, ScaleMax = .8f,
				ColorRamp = new GradientTexture1D { Gradient = ramp },
			},
			DrawPass1 = new QuadMesh
			{
				Size = Vector2.One * .2f,
				Material = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true,
					EmissionEnabled = true, Emission = new Color(1, .65f, .12f), EmissionEnergyMultiplier = 1.2f,
				},
			},
		};
		AddChild(Particles);
	}
	public override void _ExitTree()
	{
		if (_stage != null && IsInstanceValid(_stage)) _stage.ChallengeFinished -= OnChallengeFinished;
		if (_transition != null && IsInstanceValid(_transition)) _transition.Kill();
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
		StageController? stage = StageController.Instance;
		if (_stage != stage)
		{
			if (_stage != null && IsInstanceValid(_stage)) _stage.ChallengeFinished -= OnChallengeFinished;
			_stage = stage;
			if (_stage != null) _stage.ChallengeFinished += OnChallengeFinished;
			SetActive(false);
		}
		// 状态从关卡管理器读，避免各处重复接线。
		bool wanted = _stage is { ChallengerActive: true } && _unit is { IsDead: false };
		SetActive(wanted);

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
	private void SetActive(bool active)
	{
		if (_active == active) return;
		_active = active;
		Particles!.Emitting = active;
		Particles.Visible = active;
		if (active)
		{
			EntryCount++;
			WorldPulseFx.Play(this, _unit!.GlobalPosition, new Color(1, .65f, .12f, .8f), 1.6f, .35f, false);
			AudioManager.Play(AudioIds.ChallengeBegin);
		}
		Play(active);
	}
	private void OnChallengeFinished(bool completed)
	{
		SetActive(false);
		// 退态可能来自失败、换关或死亡；只有关卡的成功事件才触发庆祝。
		if (!completed || _unit is not { IsDead: false }) return;
		CompletionCount++;
		WorldPulseFx.Play(this, _unit.GlobalPosition, new Color(.2f, 1, .65f, .9f), 2.8f, .65f, false);
		AudioManager.Play(AudioIds.ChallengeComplete);
	}

	/// <summary>出现：放大淡入；消失：拉伸并炸开淡出。</summary>
	private void Play(bool appearing)
	{
		// 新一轮开始时取消上一轮淡出的回调，避免把重新激活的标记藏掉。
		if (_transition != null && IsInstanceValid(_transition)) _transition.Kill();
		_mesh.Visible = true;
		Tween tween = CreateTween();
		_transition = tween;
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
