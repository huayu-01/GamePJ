using ActRoguelike.Core;
using System.Collections.Generic;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>只读单位状态，按效果行绘制图标、层数和世界特效。不修改状态、材质或战斗节点。</summary>
public partial class StatusEffectFx : Node3D
{
	[Export] public float PollSeconds { get; set; } = 0.08f;
	[Export] public float BadgeWorldSize { get; set; } = 0.58f;
	[Export] public float BadgeSpacing { get; set; } = 0.72f;
	[Export] public float StackHeight { get; set; } = 0.38f;
	[Export] public int StackFontSize { get; set; } = 42;
	[Export] public float StackPixelSize { get; set; } = 0.009f;
	[Export] public Color StackColor { get; set; } = Colors.White;
	[Export] public Color StackOutlineColor { get; set; } = new(0.025f, 0.035f, 0.06f);
	[Export] public int RingSegments { get; set; } = 64;
	[Export] public float RingWidth { get; set; } = 0.035f;
	[Export] public float GroundOffset { get; set; } = 0.09f;

	private sealed class Visual
	{
		public required EffectConfig Row;
		public required Node3D Root;
		public required Sprite3D Icon;
		public required Label3D Stacks;
		public required ImmediateMesh Mesh;
		public int StackCount;
	}

	private static PackedScene? _scene;
	private readonly Dictionary<string, EffectConfig> _fallbacks = new();
	private Unit _unit = null!;
	private readonly Dictionary<string, Visual> _visuals = new();
	private readonly HashSet<string> _present = new();
	private readonly List<string> _removed = new();
	private StandardMaterial3D _material = null!;
	private float _pollTimer;
	private float _time;
	private int _visualSources;
	public int VisualCount => _visuals.Count;

	/// <summary>由 UnitPresentation 统一接入；新增单位无需逐个挂节点。</summary>
	public static void Ensure(Unit unit)
	{
		foreach (Node child in unit.GetChildren())
		{
			if (child is StatusEffectFx)
			{
				return;
			}
		}
		_scene ??= RuntimeResources.Load<PackedScene>("res://Scenes/Combat/StatusEffectFx.tscn");
		if (_scene?.Instantiate() is StatusEffectFx fx)
		{
			// 本方法由单位的子节点 _Ready 调用，此时父节点仍在遍历子树，必须延后挂载兄弟节点。
			Callable.From(() =>
			{
				if (IsInstanceValid(unit) && !unit.IsQueuedForDeletion()) unit.AddChild(fx);
				else fx.Free();
			}).CallDeferred();
		}
	}

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			VertexColorUseAsAlbedo = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		_unit.StatusesChanged += Wake;
		_unit.Died += OnDied;
		_unit.ChildEnteredTree += OnChildEntered;
		_unit.ChildExitingTree += OnChildExiting;
		for (int i = 0, count = _unit.GetChildCount(); i < count; i++)
			if (_unit.GetChild(i) is IStatusVisualSource) _visualSources++;
		SetPhysicsProcess(_unit.Statuses.Count > 0 || _visualSources > 0);
	}
	private void Wake() { if (_unit.IsDead) return; _pollTimer = 0; SetPhysicsProcess(true); }
	private void OnChildEntered(Node child) { if (child is IStatusVisualSource) { _visualSources++; Wake(); } }
	private void OnChildExiting(Node child) { if (child is IStatusVisualSource) { _visualSources = Mathf.Max(0, _visualSources - 1); Wake(); } }
	private void OnDied(HitInstance hit) { ClearVisuals(); SetPhysicsProcess(false); }
	public override void _ExitTree()
	{
		if (!IsInstanceValid(_unit)) return;
		_unit.StatusesChanged -= Wake; _unit.Died -= OnDied;
		_unit.ChildEnteredTree -= OnChildEntered; _unit.ChildExitingTree -= OnChildExiting;
	}

	public override void _PhysicsProcess(double delta)
	{
		using var profile=RuntimeFrameProfiler.Measure("StatusFx");
		if (_unit.IsDead)
		{
			ClearVisuals();
			return;
		}
		float dt = (float)delta;
		_time += dt;
		_pollTimer -= dt;
		if (_pollTimer <= 0f)
		{
			_pollTimer = Mathf.Max(0.01f, PollSeconds);
			SyncStatuses();
			// 普通状态由事件唤醒；独立规则的只读来源仍保留原轮询契约。
			if (_visuals.Count == 0 && _visualSources == 0) SetPhysicsProcess(false);
		}
		if (_visuals.Count == 0) return;
		int slot = 0;
		Camera3D? camera = GetViewport().GetCamera3D();
		Vector3 right = camera == null ? Vector3.Right : GlobalBasis.Inverse() * camera.GlobalBasis.X;
		foreach (Visual visual in _visuals.Values)
		{
			EffectConfig row = visual.Row;
			Vector3 badge = Vector3.Up * row.VisualHeight + right * ((slot++ - (_visuals.Count - 1) * 0.5f) * BadgeSpacing);
			visual.Icon.Position = badge;
			visual.Stacks.Position = badge + Vector3.Up * StackHeight;
			DrawVisual(visual);
		}
	}

	private void SyncStatuses()
	{
		_present.Clear();
		foreach (ActiveStatus status in _unit.Statuses)
		{
			string id = status.Effect.Id;
			EffectConfig? row = ConfigRows.ById<EffectConfig>(id);
			if (row?.HideStatusVisual == true)
			{
				continue;
			}
			if (row == null || row.VisualStyle == StatusVisualStyle.None)
			{
				if (!_fallbacks.TryGetValue(id, out row))
				{
					row = new EffectConfig { Id = id, DisplayName = id, VisualStyle = StatusVisualStyle.Orbit,
						VisualColor = new Color(.55f, .85f, 1f, .8f), VisualIcon = ActRoguelike.UI.UiIcons.Named("ic_ui_reward_mutation") };
					_fallbacks.Add(id, row);
				}
			}
			_present.Add(id);
			SyncVisual(row, Mathf.Max(1, status.Stacks));
		}
		for (int i = 0, count = _unit.GetChildCount(); i < count; i++)
		{
			Node child = _unit.GetChild(i);
			if (child is IStatusVisualSource source)
				foreach (EffectConfig row in source.ActiveStatusVisuals())
				{
					_present.Add(row.Id); SyncVisual(row, 1);
				}
		}
		_removed.Clear();
		foreach (string id in _visuals.Keys)
		{
			if (!_present.Contains(id))
			{
				_removed.Add(id);
			}
		}
		foreach (string id in _removed)
		{
			_visuals[id].Root.QueueFree();
			_visuals.Remove(id);
		}
	}
	private void SyncVisual(EffectConfig row, int stacks)
	{
		if (!_visuals.TryGetValue(row.Id, out Visual? visual))
		{
			visual = CreateVisual(row); _visuals.Add(row.Id, visual);
		}
		visual.StackCount = stacks; visual.Stacks.Visible = row.VisualShowStacks;
		visual.Stacks.Text = stacks.ToString();
	}

	private Visual CreateVisual(EffectConfig row)
	{
		var root = new Node3D { Name = row.Id };
		var icon = new Sprite3D
		{
			Name = "Icon",
			Texture = row.VisualIcon,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			Modulate = row.VisualColor,
			PixelSize = BadgeWorldSize / Mathf.Max(1, row.VisualIcon?.GetWidth() ?? 1),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = row.VisualIcon != null,
		};
		var stacks = new Label3D
		{
			Name = "Stacks",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			FontSize = StackFontSize,
			PixelSize = StackPixelSize,
			Modulate = StackColor,
			OutlineModulate = StackOutlineColor,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		var mesh = new ImmediateMesh();
		root.AddChild(new MeshInstance3D
		{
			Name = "WorldVisual",
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
		root.AddChild(icon);
		root.AddChild(stacks);
		AddChild(root);
		return new Visual { Row = row, Root = root, Icon = icon, Stacks = stacks, Mesh = mesh };
	}

	private void DrawVisual(Visual visual)
	{
		ImmediateMesh mesh = visual.Mesh;
		EffectConfig row = visual.Row;
		mesh.ClearSurfaces();
		mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
		float phase = _time * row.VisualSpeed;
		int count = Mathf.Clamp(row.VisualCount, 1, 24);
		switch (row.VisualStyle)
		{
			case StatusVisualStyle.Sigil:
				// 层数用实心的小菱形逐层填亮，数字与图标始终面向镜头。
				for (int i = 0; i < count; i++)
				{
					float angle = Mathf.Tau * i / count + phase * 0.2f;
					Vector3 point = new(Mathf.Cos(angle) * row.VisualRadius, row.VisualHeight - StackHeight, Mathf.Sin(angle) * row.VisualRadius);
					Crystal(mesh, point, row.VisualSize * 0.5f, row.VisualSize, Fade(row.VisualColor, i < visual.StackCount ? 1f : 0.15f));
				}
				break;
			case StatusVisualStyle.Frost:
				Ring(mesh, row.VisualRadius, GroundOffset, RingWidth, Fade(row.VisualColor, 0.65f));
				for (int i = 0; i < count; i++)
				{
					float angle = Mathf.Tau * i / count;
					Vector3 point = new(Mathf.Cos(angle) * row.VisualRadius, GroundOffset, Mathf.Sin(angle) * row.VisualRadius);
					Crystal(mesh, point, row.VisualSize, row.VisualSize * (2.8f + 0.3f * Mathf.Sin(phase + i)), row.VisualColor);
				}
				break;
			case StatusVisualStyle.Embers:
				Ring(mesh, row.VisualRadius, GroundOffset, RingWidth * 0.7f, Fade(row.VisualColor, 0.45f));
				for (int i = 0; i < count; i++)
				{
					float rise = Mathf.PosMod(phase * 0.5f + i / (float)count, 1f);
					float angle = Mathf.Tau * i / count + phase * 0.5f;
					float radius = row.VisualRadius * (1f - rise * 0.4f);
					Vector3 point = new(Mathf.Cos(angle) * radius, GroundOffset + rise * row.VisualHeight * 0.7f, Mathf.Sin(angle) * radius);
					Crystal(mesh, point, row.VisualSize * (1f - rise), row.VisualSize * (2f + rise), Fade(row.VisualColor, 1f - rise));
					// 火芯比外焰更亮，保留小面积暖白，避免橙色火星融进敌人的暖色模型。
					Color core = row.VisualColor.Lerp(Colors.White, 0.65f);
					Crystal(mesh, point + Vector3.Up * row.VisualSize * 0.1f, row.VisualSize * (1f - rise) * 0.3f, row.VisualSize * 1.2f, Fade(core, 1f - rise));
				}
				break;
			case StatusVisualStyle.Orbit:
				Ring(mesh, row.VisualRadius, GroundOffset, RingWidth, Fade(row.VisualColor, 0.6f));
				for (int i = 0; i < count; i++)
				{
					float angle = Mathf.Tau * i / count + phase;
					Vector3 point = new(Mathf.Cos(angle) * row.VisualRadius, row.VisualHeight * 0.5f, Mathf.Sin(angle) * row.VisualRadius);
					Crystal(mesh, point, row.VisualSize * 0.4f, row.VisualSize, row.VisualColor);
				}
				break;
		}
		mesh.SurfaceEnd();
	}

	private void Ring(ImmediateMesh mesh, float radius, float height, float width, Color color)
	{
		int segments = Mathf.Clamp(RingSegments, 12, 128);
		for (int i = 0; i < segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			float b = Mathf.Tau * (i + 1) / segments;
			Vector3 outerA = new(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
			Vector3 outerB = new(Mathf.Cos(b) * radius, height, Mathf.Sin(b) * radius);
			float inner = Mathf.Max(0f, radius - width);
			Vector3 innerA = new(Mathf.Cos(a) * inner, height, Mathf.Sin(a) * inner);
			Vector3 innerB = new(Mathf.Cos(b) * inner, height, Mathf.Sin(b) * inner);
			Triangle(mesh, outerA, outerB, innerA, color);
			Triangle(mesh, innerA, outerB, innerB, color);
		}
	}

	private static void Crystal(ImmediateMesh mesh, Vector3 point, float radius, float height, Color color)
	{
		Vector3 tip = point + Vector3.Up * height;
		Vector3 left = point + Vector3.Left * radius;
		Vector3 right = point + Vector3.Right * radius;
		Vector3 front = point + Vector3.Forward * radius;
		Vector3 back = point + Vector3.Back * radius;
		Triangle(mesh, left, front, tip, color);
		Triangle(mesh, front, right, tip, Fade(color, 0.75f));
		Triangle(mesh, right, back, tip, color);
		Triangle(mesh, back, left, tip, Fade(color, 0.5f));
	}

	private static void Triangle(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 c, Color color)
	{
		mesh.SurfaceSetColor(color);
		mesh.SurfaceAddVertex(a);
		mesh.SurfaceAddVertex(b);
		mesh.SurfaceAddVertex(c);
	}

	private static Color Fade(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
	private void ClearVisuals()
	{
		foreach (Visual visual in _visuals.Values)
		{
			visual.Root.QueueFree();
		}
		_visuals.Clear();
	}

	public int DisplayedStacks(string id) => _visuals.TryGetValue(id, out Visual? visual) ? visual.StackCount : 0;
	public string DebugState()
	{
		var states = new List<string>();
		foreach (Visual visual in _visuals.Values)
		{
			states.Add($"{visual.Row.Id}:{visual.Row.VisualStyle}×{visual.StackCount} 图标={visual.Icon.Texture?.ResourcePath}");
		}
		return $"状态表现：{VisualCount} 项 [{string.Join(" / ", states)}]"
			+ $"｜碰撞体={FindChildren("*", "CollisionObject3D", true, false).Count} 单位={FindChildren("*", "Unit", true, false).Count} 分组={GetGroups().Count}";
	}
}
