using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.UI;

/// <summary>隔离的只读展台；旧单位预制体只在树外拆出Model，逻辑节点永不进入场景树。</summary>
public partial class CodexModelPreview : Control
{
	private SubViewport _viewport = null!;
	private Node3D _stage = null!, _modelRoot = null!;
	private Camera3D _camera = null!;
	public bool HasModel { get; private set; }
	public string CurrentModelPath { get; private set; } = "";
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// 固定展台纵横比，图片等比适配界面，避免宽详情页改变投影后裁掉模型顶部。
		_viewport = new SubViewport { Size = new Vector2I(480, 300), OwnWorld3D = true, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
		AddChild(_viewport); _stage = new Node3D(); _viewport.AddChild(_stage);
		_modelRoot = new Node3D(); _stage.AddChild(_modelRoot);
		var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.05f, .07f, .09f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(.8f, .85f, .95f), AmbientLightEnergy = .65f };
		_stage.AddChild(new WorldEnvironment { Environment = environment });
		_stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, -35, 0), LightEnergy = 1.6f });
		_stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-15, 140, 0), LightColor = new Color(.35f, .65f, 1), LightEnergy = .65f });
		_camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true }; _stage.AddChild(_camera);
		var picture = new TextureRect { Texture = _viewport.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
		AddChild(picture); picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}
	public bool ShowEntry(CodexEntry entry)
	{
		Clear();
		if (string.IsNullOrEmpty(entry.ModelPath) || !ResourceLoader.Exists(entry.ModelPath)) return false;
		var source = RuntimeResources.Load<PackedScene>(entry.ModelPath).Instantiate<Node3D>();
		Node3D model = source;
		if (entry.ExtractModel)
		{
			if (source.GetNodeOrNull<Node3D>("Model") is not { } visual) { source.Free(); return false; }
			source.RemoveChild(visual); model = visual; source.Free();
		}
		PruneLogicChildren(model);
		// 视觉资源若误填了单位或带逻辑的场景，保留图标回退，绝不将其加入场景树。
		if (!OnlyVisual(model)) { model.Free(); return false; }
		model.ProcessMode = ProcessModeEnum.Disabled; _modelRoot.AddChild(model); model.Transform = entry.ModelTransform;
		var meshes = new List<MeshInstance3D>(); NodeSearch.CollectInSubtree(model, meshes);
		if (model is MeshInstance3D rootMesh) meshes.Add(rootMesh);
		bool hasBounds = false; Aabb bounds = default;
		foreach (var mesh in meshes)
		{
			if (mesh.Mesh == null) continue;
			var current = mesh.GlobalTransform * mesh.GetAabb(); bounds = hasBounds ? bounds.Merge(current) : current; hasBounds = true;
		}
		if (!hasBounds || bounds.Size.LengthSquared() < .0001f) { Clear(); return false; }
		Vector3 center = bounds.GetCenter(); float diameter = Mathf.Max(bounds.Size.X, Mathf.Max(bounds.Size.Y, bounds.Size.Z));
		_camera.GlobalPosition = center + new Vector3(.8f, .35f, 1.4f) * diameter * 2;
		_camera.LookAt(center);
		// 按实际观察方向投影包围盒，给细长武器和矮胖生物分别留出同等边距。
		var view=_camera.GlobalTransform.AffineInverse();Vector2 minimum=new(float.PositiveInfinity,float.PositiveInfinity),maximum=new(float.NegativeInfinity,float.NegativeInfinity);
		for(int corner=0;corner<8;corner++) {
			Vector3 point=view*bounds.GetEndpoint(corner);var p=new Vector2(point.X,point.Y);minimum=minimum.Min(p);maximum=maximum.Max(p);
		}
		Vector2 extent=maximum-minimum;
		_camera.Size=Mathf.Max(.3f,Mathf.Max(extent.Y,extent.X/(480f/300f))*1.08f);
		CurrentModelPath = entry.ModelPath; HasModel = true; Visible = true;
		// 静态展台按需只画一帧；关闭和闲置时不产生渲染开销。
		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once; return true;
	}
	private static bool OnlyVisual(Node node)
	{
		if (node is CollisionObject3D || node.GetScript().VariantType != Variant.Type.Nil) return false;
		foreach (Node child in node.GetChildren()) if (!OnlyVisual(child)) return false;
		return true;
	}
	private static void PruneLogicChildren(Node root)
	{
		foreach (Node child in root.GetChildren())
			if (child is CollisionObject3D || child.GetScript().VariantType != Variant.Type.Nil) { root.RemoveChild(child); child.Free(); }
			else PruneLogicChildren(child);
	}
	public void Clear()
	{
		if (_modelRoot != null) foreach (Node child in _modelRoot.GetChildren()) { _modelRoot.RemoveChild(child); child.QueueFree(); }
		HasModel = false; CurrentModelPath = ""; Visible = false;
		if (_viewport != null) _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
	}
}
