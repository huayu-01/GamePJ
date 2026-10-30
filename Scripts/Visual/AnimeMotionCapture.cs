using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>一份只读模型捕获供多个局部模糊共享；不复制脚本、动画播放器或碰撞。</summary>
public partial class AnimeMotionCapture : Node
{
    public const int CaptureWidth = 512;
    public const int BlurTaps = 13;
    public int RequestedFrames { get; private set; }
    public int MeshCount => _meshes.Count;
    public bool Active => _viewport?.RenderTargetUpdateMode == SubViewport.UpdateMode.Always;
    public Vector2I CaptureSize => _viewport?.Size ?? Vector2I.Zero;
    public bool IsolatedWorld => _viewport?.OwnWorld3D == true;
    public ViewportTexture Texture => _viewport.GetTexture();
    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private Camera3D? _sourceCamera;
    private Node3D? _model;
    private Node3D _proxy = null!;
    private bool _requested;
    private readonly List<(Skeleton3D From, Skeleton3D To)> _skeletons = new();
    private readonly List<(MeshInstance3D From, MeshInstance3D To)> _meshes = new();

    public override void _Ready()
    {
        ProcessPriority = 20;
        _viewport = new SubViewport
        {
            Name = "ActorOnlyViewport", Size = new Vector2I(CaptureWidth, 288), OwnWorld3D = true,
            TransparentBg = true, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(_viewport);
        // 主相机已提供插值后变换，捕获相机不能再次进行物理插值。
        _camera = new Camera3D { Current = true, PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off }; _viewport.AddChild(_camera);
        _proxy = new Node3D { Name = "PoseProxy", PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
        _viewport.AddChild(_proxy);
        SetProcess(false);
    }

    public void Bind(Node3D model, Camera3D camera, Godot.Environment? environment, DirectionalLight3D? light)
    {
        Cancel();
        foreach (Node child in _proxy.GetChildren()) child.Free();
        _skeletons.Clear(); _meshes.Clear();
        _model = model; _sourceCamera = camera;
        var sources = new List<Skeleton3D>(); var meshes = new List<MeshInstance3D>();
        Collect(model, sources, meshes);
        foreach (var from in sources)
        {
            var to = new Skeleton3D(); _proxy.AddChild(to);
            for (int bone = 0; bone < from.GetBoneCount(); bone++) to.AddBone(from.GetBoneName(bone));
            for (int bone = 0; bone < from.GetBoneCount(); bone++)
            {
                to.SetBoneParent(bone, from.GetBoneParent(bone)); to.SetBoneRest(bone, from.GetBoneRest(bone));
            }
            _skeletons.Add((from, to));
        }
        foreach (var from in meshes)
        {
            var to = new MeshInstance3D { Mesh = from.Mesh, Skin = from.Skin, MaterialOverride = from.MaterialOverride,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _proxy.AddChild(to);
            for (int surface = 0; surface < from.Mesh!.GetSurfaceCount(); surface++)
                to.SetSurfaceOverrideMaterial(surface, from.GetSurfaceOverrideMaterial(surface));
            var sourceSkeleton = from.GetNodeOrNull<Skeleton3D>(from.Skeleton);
            foreach (var pair in _skeletons) if (pair.From == sourceSkeleton) { to.Skeleton = to.GetPathTo(pair.To); break; }
            _meshes.Add((from, to));
        }
        // 环境与灯光只在绑定时复制，捕获不加入主世界、不占用保留图层。
        foreach (Node child in _viewport.GetChildren()) if (child is WorldEnvironment or DirectionalLight3D) child.Free();
        _viewport.AddChild(new WorldEnvironment { Environment = environment == null ? new Godot.Environment
            { AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .65f }
            : (Godot.Environment)environment.Duplicate() });
        if (light != null) _viewport.AddChild(new DirectionalLight3D { Transform = light.GlobalTransform, LightColor = light.LightColor,
            LightEnergy = light.LightEnergy, ShadowEnabled = false });
    }

    public void SetCamera(Camera3D camera) => _sourceCamera = camera;

    public void Cancel()
    {
        _requested = false;
        if (_viewport != null) _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        SetProcess(false);
    }

    public void RequestFrame() { _requested = true; SetProcess(true); }

    public override void _Process(double delta)
    {
        if (!_requested || !IsInstanceValid(_model) || !IsInstanceValid(_sourceCamera))
        {
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _requested = false; SetProcess(false); return;
        }
        _requested = false; RequestedFrames++;
        var source = _sourceCamera!;
        Vector2 size = source.GetViewport().GetVisibleRect().Size;
        _viewport.Size = new Vector2I(CaptureWidth, Mathf.Max(2, Mathf.RoundToInt(CaptureWidth * size.Y / size.X)));
        _camera.GlobalTransform = source.GetGlobalTransformInterpolated();
        _camera.Projection = source.Projection; _camera.Fov = source.Fov; _camera.Size = source.Size;
        _camera.Near = source.Near; _camera.Far = source.Far; _camera.KeepAspect = source.KeepAspect;
        _camera.HOffset = source.HOffset; _camera.VOffset = source.VOffset; _camera.FrustumOffset = source.FrustumOffset;
        _proxy.GlobalTransform = _model!.GetGlobalTransformInterpolated();
        Transform3D inverse = _proxy.GlobalTransform.AffineInverse();
        foreach (var pair in _skeletons)
        {
            pair.To.Transform = inverse * pair.From.GetGlobalTransformInterpolated();
            for (int bone = 0; bone < pair.From.GetBoneCount(); bone++) pair.To.SetBonePose(bone, pair.From.GetBonePose(bone));
        }
        foreach (var pair in _meshes)
        {
            pair.To.Transform = inverse * pair.From.GetGlobalTransformInterpolated();
            pair.To.Visible = pair.From.IsVisibleInTree();
        }
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }

    private static void Collect(Node root, List<Skeleton3D> skeletons, List<MeshInstance3D> meshes)
    {
        if (root is AnimeMotionFx or AnimeMotionCapture) return;
        if (root is Skeleton3D skeleton) skeletons.Add(skeleton);
        if (root is MeshInstance3D { Mesh: not null } mesh) meshes.Add(mesh);
        foreach (Node child in root.GetChildren()) Collect(child, skeletons, meshes);
    }

    public override void _ExitTree() { _model = null; _sourceCamera = null; _skeletons.Clear(); _meshes.Clear(); }
}
