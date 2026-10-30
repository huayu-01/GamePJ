using System;
using System.Collections.Generic;
using Godot;
using ActRoguelike.Core;

namespace ActRoguelike.Visual;

/// <summary>只消费视觉挂点和触发窗口；稀疏撕边与共享角色捕获的局部方向模糊，不参与战斗。</summary>
public partial class AnimeMotionFx : Node3D
{
    [Export] public NodePath SourcePath { get; set; } = new();
    [Export] public NodePath ModelPath { get; set; } = new();
    [Export] public Color Tint { get; set; } = new("#9ca9bd");
    [Export] public Color Shade { get; set; } = new("#424f68");
    [Export] public float Width { get; set; } = .42f;
    [Export] public float Height { get; set; } = .65f;
    [Export] public float Lifetime { get; set; } = .13f;
    [Export(PropertyHint.Range, "0,2,1")] public int MaxEchoes { get; set; }
    [Export] public Vector2 BlurRadius { get; set; } = new(.9f, .9f);
    [Export] public float BlurSeconds { get; set; } = .08f;
    [Export] public float BlurMaxDistance { get; set; } = .85f;
    [Export] public float ContourOpacity { get; set; } = .85f;
    [Export] public float TearOpacity { get; set; } = .32f;
    [Export] public float EchoInterval { get; set; } = .065f;
    [Export] public float TeleportDistance { get; set; } = 4f;
    [Export] public float MaxTrailDistance { get; set; } = 3.5f;
    public float ClockScale { get; set; } = 1f;
    public int EchoCount { get; private set; }
    public int PeakEchoes { get; private set; }
    public int TotalEchoes { get; private set; }
    public int PeakSamples { get; private set; }
    public int PulseCount { get; private set; }
    public int PoolSize => _echoes.Count;
    public int LiveSamples => _count;
    public float RemainingWindow => _window;
    public bool TrailVisible => _ribbon?.Visible == true;
    public bool BlurVisible => _blur?.Visible == true;
    public int BlurFrames { get; private set; }
    public float PeakBlurDistance { get; private set; }
    public const int RibbonTriangles = 28;
    private readonly Vector3[] _samples = new Vector3[8];
    private readonly float[] _ages = new float[8];
    private readonly Vector3[] _shaderPoints = new Vector3[8];
    private readonly List<EchoSlot> _echoes = new();
    private Node3D? _source;
    private Node3D? _model;
    private MeshInstance3D? _ribbon;
    private ShaderMaterial? _ribbonMaterial;
    private MeshInstance3D? _blur;
    private ShaderMaterial? _blurMaterial;
    private AnimeMotionCapture? _capture;
    private int _count;
    private float _window;
    private float _sampleTimer;
    private float _echoTimer;
    private int _nextEcho;
    private bool _initialized;
    private static readonly StringName ProgressParameter = "progress";
    private static readonly StringName PathParameter = "path_points";
    private static readonly StringName VisibilityParameter = "visibility";
    private static readonly StringName CameraParameter = "camera_local";
    private static readonly StringName MotionParameter = "motion_uv";
    private static readonly StringName CenterParameter = "center_uv";
    private static readonly StringName RadiusParameter = "radius_uv";
    private static readonly StringName StrengthParameter = "strength";

    public override void _Ready()
    {
        var mesh = new ArrayMesh();
        var vertices = new Vector3[32];
        var uv = new Vector2[32];
        var indices = new int[84];
        int at = 0;
        for (int plane = 0; plane < 2; plane++)
        {
            for (int i = 0; i < 8; i++)
                for (int edge = 0; edge < 2; edge++)
                {
                    int index = plane * 16 + i * 2 + edge;
                    vertices[index] = new Vector3(i / 7f, edge * 2 - 1, plane);
                    uv[index] = new Vector2(i / 7f, edge);
                }
            for (int i = 0; i < 7; i++)
            {
                int a = plane * 16 + i * 2;
                indices[at++] = a; indices[at++] = a + 1; indices[at++] = a + 2;
                indices[at++] = a + 1; indices[at++] = a + 3; indices[at++] = a + 2;
            }
        }
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _ribbonMaterial = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Effects/AnimeMotion/smear_ribbon.gdshader") };
        _ribbonMaterial.SetShaderParameter("primary_color", Tint);
        _ribbonMaterial.SetShaderParameter("shade_color", Shade);
        _ribbonMaterial.SetShaderParameter("ribbon_width", Width);
        _ribbonMaterial.SetShaderParameter("ribbon_height", Height);
        _ribbonMaterial.SetShaderParameter("tear_opacity", TearOpacity);
        _ribbon = new MeshInstance3D
        {
            Name = "SmearRibbon", Mesh = mesh, MaterialOverride = _ribbonMaterial, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(Vector3.One * -6f, Vector3.One * 12f),
        };
        AddChild(_ribbon);
        _ribbon.TopLevel = true;
        _ribbon.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _initialized = true;
        SetPhysicsProcess(false); SetProcess(false);
        if (!SourcePath.IsEmpty) Bind(GetNodeOrNull<Node3D>(SourcePath), ModelPath.IsEmpty ? null : GetNodeOrNull<Node3D>(ModelPath));
        else if (IsInstanceValid(_source)) Bind(_source, _model);
    }

    /// <summary>换模或改挂点时显式重绑；只创建 MeshInstance 与 Skeleton，不复制模型脚本和动画播放器。</summary>
    public void Bind(Node3D? source, Node3D? model = null)
    {
        Cancel();
        foreach (var slot in _echoes) slot.Root.QueueFree();
        _echoes.Clear();
        _source = source; _model = model; _nextEcho = 0;
        if (!_initialized || !IsInstanceValid(model)) return;
        var skeletons = new List<Skeleton3D>();
        var meshes = new List<MeshInstance3D>();
        Collect(model!, skeletons, meshes);
        if (meshes.Count == 0) return;
        for (int i = 0; i < Mathf.Clamp(MaxEchoes, 0, 2); i++)
            _echoes.Add(CreateSlot(skeletons, meshes, i));
    }

    public void Begin(float seconds = .16f)
    {
        if (!_initialized || !IsInstanceValid(_source)) return;
        Cancel();
        _window = Mathf.Clamp(seconds, .01f, 2f);
        _sampleTimer = 0; _echoTimer = .025f;
        Append(_source!.GlobalPosition);
        PulseCount++;
        SetPhysicsProcess(true); SetProcess(true);
    }

    public void Stop() => _window = 0;

    public void SetBlurCapture(AnimeMotionCapture capture)
    {
        _capture = capture;
        if (_blur == null)
        {
            _blurMaterial = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Effects/AnimeMotion/directional_blur.gdshader"), RenderPriority = 10 };
            _blur = new MeshInstance3D { Name = "LocalDirectionalBlur", Mesh = new QuadMesh { Size = BlurRadius * 2f + Vector2.One * BlurMaxDistance * 2f },
                MaterialOverride = _blurMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_blur); _blur.TopLevel = true; _blur.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        }
        _blurMaterial!.SetShaderParameter("actor_texture", capture.Texture);
        _blurMaterial.SetShaderParameter("contour_opacity", ContourOpacity);
        _ribbonMaterial!.SetShaderParameter("actor_texture", capture.Texture);
        _ribbonMaterial.SetShaderParameter("protect_silhouette", true);
    }

    public void Cancel()
    {
        _window = 0; _count = 0; EchoCount = 0;
        if (_ribbon != null) _ribbon.Visible = false;
        if (_blur != null) _blur.Visible = false;
        foreach (var slot in _echoes) { slot.Age = float.PositiveInfinity; slot.Root.Visible = false; }
        SetPhysicsProcess(false); SetProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_source)) { Cancel(); return; }
        float dt = (float)delta * Mathf.Max(0, ClockScale);
        if (dt <= 0) return;
        for (int i = 0; i < _count; i++) _ages[i] += dt;
        while (_count > 0 && _ages[_count - 1] > Mathf.Max(.04f, Lifetime)) _count--;
        EchoCount = 0;
        foreach (var slot in _echoes)
        {
            slot.Age += dt;
            bool alive = slot.Age < Mathf.Max(.04f, Lifetime) * 1.5f;
            slot.Root.Visible = alive;
            if (alive) { slot.Material.SetShaderParameter(ProgressParameter, slot.Age / (Mathf.Max(.04f, Lifetime) * 1.5f)); EchoCount++; }
        }
        if (_window > 0)
        {
            _window = Mathf.Max(0, _window - dt);
            _sampleTimer -= dt; _echoTimer -= dt;
            Vector3 current = _source!.GlobalPosition;
            if (_count > 0 && current.DistanceTo(_samples[0]) > TeleportDistance) { Cancel(); return; }
            if (_sampleTimer <= 0) { Append(current); _sampleTimer += 1f / 60f; }
            if (_echoTimer <= 0 && _echoes.Count > 0 && IsInstanceValid(_model))
            {
                Freeze(_echoes[_nextEcho]);
                _nextEcho = (_nextEcho + 1) % _echoes.Count;
                _echoTimer = Mathf.Max(.03f, EchoInterval);
                EchoCount = 0;
                foreach (var slot in _echoes) if (slot.Root.Visible) EchoCount++;
            }
        }
        PeakEchoes = Mathf.Max(PeakEchoes, EchoCount);
        PeakSamples = Mathf.Max(PeakSamples, _count);
        if (_window <= 0 && _count == 0 && EchoCount == 0) Cancel();
    }

    public override void _Process(double delta)
    {
        if (_ribbon == null || _ribbonMaterial == null || !IsInstanceValid(_source)) return;
        bool moving = _count > 1 && _samples[0].DistanceSquaredTo(_samples[_count - 1]) > .0005f;
        _ribbon.Visible = moving;
        if (_blur != null) _blur.Visible = false;
        if (!moving) return;
        Vector3 origin = _window > 0 ? _source!.GetGlobalTransformInterpolated().Origin : _samples[0];
        _ribbon.GlobalTransform = new Transform3D(Basis.Identity, origin);
        _shaderPoints[0] = Vector3.Zero;
        for (int i = 1; i < 8; i++) _shaderPoints[i] = _samples[Mathf.Min(i, _count - 1)] - origin;
        _ribbonMaterial.SetShaderParameter(PathParameter, _shaderPoints);
        _ribbonMaterial.SetShaderParameter(VisibilityParameter, Mathf.Clamp(1f - _ages[0] / Mathf.Max(.04f, Lifetime), 0, 1));
        if (GetViewport().GetCamera3D() is { } camera) _ribbonMaterial.SetShaderParameter(CameraParameter, camera.GlobalPosition - origin);
        UpdateBlur(origin);
    }

    private void UpdateBlur(Vector3 origin)
    {
        if (_blur == null || _blurMaterial == null || !IsInstanceValid(_capture) || _count < 2) return;
        if (GetViewport().GetCamera3D() is not { } camera || camera.IsPositionBehind(origin)) return;
        // 多个物理采样求速度；慢放只改变时间推进，保持同一曝光形状。
        int sample = Mathf.Min(_count - 1, 3);
        float seconds = _ages[sample] - _ages[0];
        Vector3 velocity = seconds > .0001f ? (_samples[0] - _samples[sample]) / seconds : Vector3.Zero;
        float distance = Mathf.Min(velocity.Length() * BlurSeconds, BlurMaxDistance);
        if (distance < .025f) return;
        Vector3 direction = velocity.Normalized();
        Vector2 viewSize = GetViewport().GetVisibleRect().Size;
        Vector2 center = camera.UnprojectPosition(origin);
        Vector2 motion = (camera.UnprojectPosition(origin + direction * distance) - center) / viewSize;
        Vector2 radius = new(
            camera.UnprojectPosition(origin + camera.GlobalBasis.X * BlurRadius.X).DistanceTo(center) / viewSize.X,
            camera.UnprojectPosition(origin + camera.GlobalBasis.Y * BlurRadius.Y).DistanceTo(center) / viewSize.Y);
        _blur.GlobalTransform = new Transform3D(camera.GlobalBasis, origin + camera.GlobalBasis.Z * .9f - direction * distance * .3f);
        _blurMaterial.SetShaderParameter(MotionParameter, motion);
        _blurMaterial.SetShaderParameter(CenterParameter, center / viewSize);
        _blurMaterial.SetShaderParameter(RadiusParameter, radius);
        _blurMaterial.SetShaderParameter(StrengthParameter, Mathf.Clamp(distance / .12f, 0, 1) * Mathf.Clamp(1f - _ages[0] / Lifetime, 0, 1));
        _blur.Visible = true; BlurFrames++; PeakBlurDistance = Mathf.Max(PeakBlurDistance, distance);
        _capture!.RequestFrame();
    }

    private void Append(Vector3 point)
    {
        _count = Mathf.Min(8, _count + 1);
        for (int i = _count - 1; i > 0; i--) { _samples[i] = _samples[i - 1]; _ages[i] = _ages[i - 1]; }
        _samples[0] = point; _ages[0] = 0;
        while (_count > 1 && point.DistanceTo(_samples[_count - 1]) > Mathf.Max(.1f, MaxTrailDistance)) _count--;
    }

    private sealed class EchoSlot
    {
        public Node3D Root = null!;
        public ShaderMaterial Material = null!;
        public Skeleton3D[] FromSkeletons = Array.Empty<Skeleton3D>();
        public Skeleton3D[] ToSkeletons = Array.Empty<Skeleton3D>();
        public MeshInstance3D[] FromMeshes = Array.Empty<MeshInstance3D>();
        public MeshInstance3D[] ToMeshes = Array.Empty<MeshInstance3D>();
        public float Age = float.PositiveInfinity;
    }

    private EchoSlot CreateSlot(List<Skeleton3D> skeletons, List<MeshInstance3D> meshes, int index)
    {
        var slot = new EchoSlot
        {
            Root = new Node3D { Name = "PoseEcho" + index, Visible = false },
            Material = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Effects/AnimeMotion/pose_echo.gdshader") },
            FromSkeletons = skeletons.ToArray(), ToSkeletons = new Skeleton3D[skeletons.Count],
            FromMeshes = meshes.ToArray(), ToMeshes = new MeshInstance3D[meshes.Count],
        };
        AddChild(slot.Root); slot.Root.TopLevel = true;
        slot.Root.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        slot.Material.SetShaderParameter("echo_color", Tint.Darkened(.15f + index * .12f));
        slot.Material.SetShaderParameter("seed", (float)index * 3.7f);
        for (int i = 0; i < skeletons.Count; i++)
        {
            var from = skeletons[i]; var to = new Skeleton3D { Name = "Skeleton" + i };
            slot.Root.AddChild(to); slot.ToSkeletons[i] = to;
            for (int bone = 0; bone < from.GetBoneCount(); bone++) to.AddBone(from.GetBoneName(bone));
            for (int bone = 0; bone < from.GetBoneCount(); bone++) { to.SetBoneParent(bone, from.GetBoneParent(bone)); to.SetBoneRest(bone, from.GetBoneRest(bone)); }
        }
        for (int i = 0; i < meshes.Count; i++)
        {
            var from = meshes[i];
            var to = new MeshInstance3D { Name = "EchoMesh" + i, Mesh = from.Mesh, Skin = from.Skin, MaterialOverride = slot.Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            slot.Root.AddChild(to); slot.ToMeshes[i] = to;
            var sourceSkeleton = from.GetNodeOrNull<Skeleton3D>(from.Skeleton);
            for (int s = 0; s < skeletons.Count; s++)
                if (skeletons[s] == sourceSkeleton) { to.Skeleton = to.GetPathTo(slot.ToSkeletons[s]); break; }
        }
        return slot;
    }

    private void Freeze(EchoSlot slot)
    {
        slot.Root.GlobalTransform = _model!.GlobalTransform;
        Transform3D inverse = slot.Root.GlobalTransform.AffineInverse();
        for (int i = 0; i < slot.ToSkeletons.Length; i++)
        {
            var from = slot.FromSkeletons[i]; var to = slot.ToSkeletons[i];
            to.Transform = inverse * from.GlobalTransform;
            for (int bone = 0; bone < from.GetBoneCount(); bone++) to.SetBonePose(bone, from.GetBonePose(bone));
        }
        for (int i = 0; i < slot.ToMeshes.Length; i++)
        {
            slot.ToMeshes[i].Transform = inverse * slot.FromMeshes[i].GlobalTransform;
            slot.ToMeshes[i].Visible = slot.FromMeshes[i].IsVisibleInTree();
        }
        slot.Root.ResetPhysicsInterpolation();
        slot.Material.SetShaderParameter(ProgressParameter, 0f);
        slot.Root.Visible = true; slot.Age = 0;
        TotalEchoes++;
    }

    private static void Collect(Node root, List<Skeleton3D> skeletons, List<MeshInstance3D> meshes)
    {
        if (root is AnimeMotionFx or AnimeMotionCapture) return;
        if (root is Skeleton3D skeleton) skeletons.Add(skeleton);
        if (root is MeshInstance3D { Mesh: not null } mesh) meshes.Add(mesh);
        foreach (Node child in root.GetChildren()) Collect(child, skeletons, meshes);
    }

    public override void _ExitTree()
    {
        _source = null; _model = null; _capture = null; _echoes.Clear();
    }
}
