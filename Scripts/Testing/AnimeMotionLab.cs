using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using ActRoguelike.Core;
using ActRoguelike.Visual;

namespace ActRoguelike.Testing;

/// <summary>独立对照场景：同一骨骼动作与轨迹，只给右侧模型叠加视觉特效。</summary>
public partial class AnimeMotionLab : Node3D
{
    private readonly Node3D[] _models = new Node3D[2];
    private readonly AnimationPlayer[] _players = new AnimationPlayer[2];
    private readonly Skeleton3D[] _skeletons = new Skeleton3D[2];
    private readonly List<AnimeMotionFx> _effects = new();
    private readonly List<ulong> _materialIds = new();
    private readonly Node3D[] _hands = new Node3D[2];
    private AnimeMotionFx _dash = null!;
    private Node3D _bodyAnchor = null!;
    private Camera3D _camera = null!;
    private AnimeMotionCapture _capture = null!;
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _light = null!;
    private Label _modeLabel = null!, _stateLabel = null!, _leftLabel = null!, _rightLabel = null!;
    private float _elapsed, _time;
    private int _cycle = -1, _mode;
    private float _rate = 1;
    private bool _enabled = true, _triggered, _check, _finished;
    private float _maxPoseDifference;
    private int _activeFrames, _pass, _fail;
    private string _view = "dash";
    private bool _savedStill, _comparingFrame, _comparedFrame;
    private int _interiorPixels, _changedInteriorPixels, _changedOutsidePixels;

    public override void _Ready()
    {
        ProcessPhysicsPriority = -20;
        GetWindow().ContentScaleSize = new Vector2I(1280, 720);
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--motion-smear-check") _check = true;
            if (argument.StartsWith("--motion-demo=", StringComparison.Ordinal)) _view = argument[14..];
        }
        _mode = _view == "action" ? 1 : 0;
        CreateStage();
        for (int i = 0; i < 2; i++) CreateModel(i);
        _capture = new AnimeMotionCapture { Name = "SharedActorCapture" }; AddChild(_capture);
        _capture.Bind(_models[1], _camera, _environment, _light);
        _bodyAnchor = new Node3D { Name = "BodyMotionAnchor" }; AddChild(_bodyAnchor);
        _dash = new AnimeMotionFx { Name = "DashSmear", Width = .55f, Height = .65f, Lifetime = .10f, MaxEchoes = 0,
            BlurRadius = new Vector2(1.55f, 1.55f), ContourOpacity = .85f,
            Tint = new Color("#a3b2ce"), Shade = new Color("#46536e") };
        AddChild(_dash); _dash.Bind(_bodyAnchor, _models[1]); _dash.SetBlurCapture(_capture); _effects.Add(_dash);
        for (int i = 0; i < 2; i++)
        {
            _hands[i] = new Node3D { Name = "HandAnchor" + i }; AddChild(_hands[i]);
            var effect = new AnimeMotionFx { Name = "HandSmear" + i, Width = .22f, Height = .23f, Lifetime = .10f, MaxEchoes = 0,
                BlurRadius = new Vector2(.48f, .48f), ContourOpacity = .9f, BlurSeconds = .10f,
                Tint = new Color("#afb9cb"), Shade = new Color("#3e4a60") };
            AddChild(effect); effect.Bind(_hands[i]); effect.SetBlurCapture(_capture); _effects.Add(effect);
        }
        CreateHud();
        RecordMaterials(_models[1], _materialIds);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void CreateStage()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("#eeeae2"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("#c8d0df"), AmbientLightEnergy = .65f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        _environment = environment;
        AddChild(new WorldEnvironment { Environment = environment });
        _light = new DirectionalLight3D { RotationDegrees = new Vector3(-52, -28, 0), LightEnergy = 1.0f, ShadowEnabled = true }; AddChild(_light);
        var floor = new StandardMaterial3D { AlbedoColor = new Color("#d8d6d0"), Roughness = 1 };
        AddChild(new MeshInstance3D { Name = "Floor", Mesh = new BoxMesh { Size = new Vector3(24, .1f, 14), Material = floor }, Position = new Vector3(0, -.08f, 0) });
        var laneMaterial = new StandardMaterial3D { AlbedoColor = new Color("#c5c7cb"), Roughness = 1 };
        for (int i = 0; i < 2; i++)
        {
            AddChild(new MeshInstance3D { Name = "Lane" + i, Mesh = new BoxMesh { Size = new Vector3(5.5f, .025f, 3.8f), Material = laneMaterial }, Position = new Vector3(i == 0 ? -3.4f : 3.4f, -.012f, 0) });
            var markMaterial = new StandardMaterial3D { AlbedoColor = new Color(i == 0 ? "#9da1aa" : "#8d9fb9"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(5.5f, .01f, .035f), Material = markMaterial }, Position = new Vector3(i == 0 ? -3.4f : 3.4f, .009f, 1.92f) });
        }
        _camera = new Camera3D { Position = new Vector3(0, 4.1f, 10.4f), Fov = 35, Current = true };
        AddChild(_camera); _camera.LookAt(new Vector3(0, .65f, 0));
    }

    private void CreateModel(int index)
    {
        var scene = RuntimeResources.Load<PackedScene>("res://Assets/Enemies/StoneGolemPlus/stone_golem_plus.glb");
        Node3D model = scene.Instantiate<Node3D>();
        model.Name = index == 0 ? "Original" : "MotionStyled";
        AddChild(model); model.Scale = Vector3.One * .67f;
        _models[index] = model;
        _players[index] = Find<AnimationPlayer>(model)!;
        _skeletons[index] = Find<Skeleton3D>(model)!;
        _players[index].ProcessMode = ProcessModeEnum.Disabled;
        ApplyPalette(model);
    }

    internal static void ApplyPalette(Node root)
    {
        if (root is MeshInstance3D { Mesh: not null } mesh)
        {
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                string name = mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty;
                bool eyes = name.StartsWith("GolemPlus_Eyes", StringComparison.Ordinal);
                bool dark = name.StartsWith("GolemPlus_Sockets", StringComparison.Ordinal);
                var material = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Materials/EnemyAnime/anime_paint.gdshader") };
                material.SetShaderParameter("paint_color", new Color(eyes ? "#ffd477" : dark ? "#252c3a" : "#858890"));
                material.SetShaderParameter("shade_color", new Color(eyes ? "#f5bd53" : dark ? "#161d2b" : "#3c4558"));
                material.SetShaderParameter("light_color", new Color(eyes ? "#ffe4a1" : "#b3b7c1"));
                material.SetShaderParameter("brush_amount", 0f);
                material.SetShaderParameter("edge_light", .015f);
                if (eyes) material.SetShaderParameter("emissive_amount", .35f);
                mesh.SetSurfaceOverrideMaterial(surface, material);
            }
        }
        foreach (Node child in root.GetChildren()) ApplyPalette(child);
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
        canvas.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddLabel(root, "动漫高速拖抹", new Vector2(36, 24), 32);
        _modeLabel = AddLabel(root, "", new Vector2(38, 70), 19);
        _leftLabel = AddLabel(root, "原动作", Vector2.Zero, 22);
        _rightLabel = AddLabel(root, "外轮廓模糊 ＋ 稀疏撕边", Vector2.Zero, 22);
        _stateLabel = AddLabel(root, "", new Vector2(38, 626), 18);
        AddLabel(root, "F1 位移    F2 动作帧    F3 效果开关    空格 慢放    R 重播", new Vector2(38, 670), 17);
    }

    private static Label AddLabel(Node parent, string text, Vector2 position, int size)
    {
        var label = new Label { Text = text, Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("#303746"));
        parent.AddChild(label); return label;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_comparingFrame) return;
        _elapsed += (float)delta;
        _time += (float)delta * _rate;
        float time = Mathf.Max(0, _time - .35f);
        int cycle = (int)(time / 2.25f);
        float phase = time % 2.25f;
        bool newCycle = cycle != _cycle;
        if (newCycle)
        {
            _cycle = cycle; _triggered = false;
            foreach (var effect in _effects) effect.Cancel();
        }
        float dashPhase = Mathf.Clamp((phase - .48f) / .25f, 0, 1);
        float x = _mode == 0 ? Mathf.Lerp(-1.05f, 1.05f, dashPhase) : 0;
        string clip = _mode == 0 ? "walk" : "slam";
        float sampleTime = _mode == 0 ? .10f + dashPhase * .50f : Mathf.Min(phase, 1.20f);
        for (int i = 0; i < 2; i++)
        {
            _models[i].Position = new Vector3((i == 0 ? -3.4f : 3.4f) + x, .035f, 0);
            _models[i].Rotation = new Vector3(0, _mode == 0 ? .65f : -.12f, 0);
            Sample(_players[i], clip, sampleTime);
            if (newCycle) _models[i].ResetPhysicsInterpolation();
        }
        _bodyAnchor.GlobalPosition = _models[1].GlobalPosition + Vector3.Up * .96f;
        for (int i = 0; i < 2; i++)
        {
            int bone = _skeletons[1].FindBone(i == 0 ? "RightHand" : "LeftHand");
            _hands[i].GlobalPosition = _skeletons[1].GlobalTransform * (_skeletons[1].GetBoneGlobalPose(bone) * new Vector3(0, .22f, 0));
        }
        float start = _mode == 0 ? .47f : .48f;
        if (!_triggered && phase >= start)
        {
            _triggered = true;
            if (_enabled)
            {
                if (_mode == 0) _dash.Begin(.28f);
                else { _effects[1].Begin(.22f); _effects[2].Begin(.22f); }
            }
        }
        foreach (var effect in _effects) effect.ClockScale = _rate;
        for (int i = 0; i < _skeletons[0].GetBoneCount(); i++)
            _maxPoseDifference = Mathf.Max(_maxPoseDifference, _skeletons[0].GetBonePoseRotation(i).AngleTo(_skeletons[1].GetBonePoseRotation(i)));
        foreach (var effect in _effects) if (effect.TrailVisible) { _activeFrames++; break; }
        _modeLabel.Text = (_mode == 0 ? "快速位移 · 外轮廓拖尾，内部保持清晰" : "动作发力窗口 · 双拳外缘方向模糊") + (_rate < 1 ? "    0.25× 慢放" : "");
        bool active = _mode == 0 ? _dash.TrailVisible : _effects[1].TrailVisible || _effects[2].TrailVisible;
        _stateLabel.Text = !_enabled ? "效果已关闭 · 两边保持同一动作" : active ? "发力窗口：拖抹开启" : "等待下一次发力 · 自动循环";
        _leftLabel.Position = _camera.UnprojectPosition(new Vector3(-3.4f, 2.55f, 0)) - new Vector2(34, 0);
        _rightLabel.Position = _camera.UnprojectPosition(new Vector3(3.4f, 2.55f, 0)) - new Vector2(120, 0);
        if (_check && !_savedStill && cycle == 1 && phase > (_mode == 0 ? .67f : .59f)) { _savedStill = true; SaveStill(); }
        if (_check && !_comparedFrame && cycle == 2 && phase > (_mode == 0 ? .65f : .57f)) { _comparedFrame = true; CompareContourFrame(); }
        if (_check && !_finished && _elapsed > 7.05f) { _finished = true; Finish(); }
    }

    private static void Sample(AnimationPlayer player, string clip, float time)
    {
        if (player.CurrentAnimation != clip) player.Play(clip);
        player.Seek(time, true); player.Advance(0); player.Pause();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.PhysicalKeycode == Key.F1) ResetDemo(0);
        else if (key.PhysicalKeycode == Key.F2) ResetDemo(1);
        else if (key.PhysicalKeycode == Key.F3) { _enabled = !_enabled; ResetDemo(_mode); }
        else if (key.PhysicalKeycode == Key.Space) _rate = _rate == 1 ? .25f : 1f;
        else if (key.PhysicalKeycode == Key.R) ResetDemo(_mode);
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void ResetDemo(int mode)
    {
        _mode = mode; _time = 0; _cycle = -1;
        foreach (var effect in _effects) effect.Cancel();
    }

    private async void SaveStill()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        string path = ProjectSettings.GlobalizePath("res://Logs/evidence/anime-motion/" + _view + "-contour.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        image.SavePng(path);
    }

    private async void CompareContourFrame()
    {
        // 在第三个循环固定姿势，比较同一画面的开关效果；不影响第二循环的交付动图。
        _comparingFrame = true;
        var callbacks = new List<(bool Render, bool Physics)>();
        foreach (var model in _models) model.ResetPhysicsInterpolation();
        foreach (var effect in _effects)
        {
            callbacks.Add((effect.IsProcessing(), effect.IsPhysicsProcessing()));
            effect.SetProcess(false); effect.SetPhysicsProcess(false);
        }
        _capture.RequestFrame();
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var styled = GetViewport().GetTexture().GetImage();
            using var mask = _capture.Texture.GetImage();
            foreach (var effect in _effects) effect.Visible = false;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var clean = GetViewport().GetTexture().GetImage();
            int width = styled.GetWidth(), height = styled.GetHeight();
            for (int y = 2; y < mask.GetHeight() - 2; y++)
                for (int x = mask.GetWidth() / 2; x < mask.GetWidth() - 2; x++)
                {
                    int px = (2 * x + 1) * width / (2 * mask.GetWidth());
                    int py = (2 * y + 1) * height / (2 * mask.GetHeight());
                    Color a = styled.GetPixel(px, py), b = clean.GetPixel(px, py);
                    bool changed = Mathf.Max(Mathf.Abs(a.R - b.R), Mathf.Max(Mathf.Abs(a.G - b.G), Mathf.Abs(a.B - b.B))) > 2f / 255f;
                    bool interior = mask.GetPixel(x, y).A > .99f && mask.GetPixel(x - 1, y).A > .99f && mask.GetPixel(x + 1, y).A > .99f
                        && mask.GetPixel(x, y - 1).A > .99f && mask.GetPixel(x, y + 1).A > .99f;
                    if (interior) { _interiorPixels++; if (changed) _changedInteriorPixels++; }
                    else if (mask.GetPixel(x, y).A < .01f && changed) _changedOutsidePixels++;
                }
            string directory = ProjectSettings.GlobalizePath("res://Logs/evidence/anime-motion/");
            mask.SavePng(directory + _view + "-silhouette-mask.png");
        }
        finally
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                _effects[i].Visible = true;
                _effects[i].SetProcess(callbacks[i].Render); _effects[i].SetPhysicsProcess(callbacks[i].Physics);
            }
            _comparingFrame = false;
        }
    }

    private void Check(bool condition, string name)
    {
        if (condition) { _pass++; GD.Print("[AnimeMotion] PASS ", name); }
        else { _fail++; GD.Print("[AnimeMotion] FAIL ", name); }
    }

    private void Finish()
    {
        Check(_maxPoseDifference < .001f, "两边骨骼动作相同，效果没有改动源姿势");
        Check(_activeFrames > 0, "真实运动窗口出现拖抹");
        Check(_effects.TrueForAll(effect => effect.PoolSize == 0 && effect.TotalEchoes == 0), "移除硬边整模型残影");
        Check(_mode != 0 || _dash.BlurFrames > 0, "快速位移触发像素方向模糊");
        Check(_mode != 1 || (_effects[1].BlurFrames > 0 && _effects[2].BlurFrames > 0), "双拳分别触发局部像素模糊");
        Check(_capture.RequestedFrames > 0 && _capture.MeshCount > 0 && _capture.IsolatedWorld, "共享捕获只绘制隔离的目标模型");
        Check(_capture.CaptureSize == new Vector2I(512, 288), "角色捕获分辨率固定为512×288");
        Check(_interiorPixels > 100 && _changedInteriorPixels == 0, "同帧开关像素对照：模型内部没有模糊或撕边覆盖");
        Check(_changedOutsidePixels > 10, "同帧开关像素对照：剪影外存在实际拖尾变化");
        Check(!_capture.Active && CountForbidden(_capture, true) == 0, "窗口结束关闭捕获且代理没有业务节点");
        Check(_mode != 1 || (_effects[1].PeakSamples > 1 && _effects[2].PeakSamples > 1), "动作窗口追踪真实双手骨骼");
        Check(CountForbidden(this, false) == 0, "演示与特效没有单位、碰撞或命中节点");
        foreach (var effect in _effects) Check(CountForbidden(effect, true) == 0, "残影不携带动画播放器或逻辑");
        var after = new List<ulong>(); RecordMaterials(_models[1], after);
        Check(after.Count == _materialIds.Count && after.TrueForAll(id => _materialIds.Contains(id)), "源模型材质引用保持不变");
        Check(_effects.TrueForAll(effect => !effect.TrailVisible && !effect.BlurVisible && effect.EchoCount == 0), "自然衰减后没有残留绘制");
        foreach (var effect in _effects) effect.Cancel();
        Check(_effects.TrueForAll(effect => !effect.TrailVisible && !effect.BlurVisible && effect.EchoCount == 0 && effect.LiveSamples == 0), "取消后清理轨迹和模糊");
        var report = new Dictionary<string, object>
        {
            ["passed"] = _fail == 0, ["pass"] = _pass, ["fail"] = _fail, ["mode"] = _view,
            ["max_pose_difference"] = _maxPoseDifference, ["active_physics_samples"] = _activeFrames,
            ["ribbon_triangles_per_effect"] = AnimeMotionFx.RibbonTriangles,
            ["echo_pool_size"] = _dash.PoolSize, ["peak_echoes"] = _dash.PeakEchoes,
            ["total_echoes"] = _dash.TotalEchoes, ["source_model_triangles"] = CountTriangles(_models[1]),
            ["pixel_motion_blur"] = true, ["blur_taps"] = AnimeMotionCapture.BlurTaps,
            ["capture_frames"] = _capture.RequestedFrames, ["capture_width"] = _capture.CaptureSize.X,
            ["capture_height"] = _capture.CaptureSize.Y, ["benchmark"] = false,
            ["contour_only"] = true, ["interior_pixels_checked"] = _interiorPixels,
            ["changed_interior_pixels"] = _changedInteriorPixels, ["changed_outside_pixels"] = _changedOutsidePixels,
        };
        string path = ProjectSettings.GlobalizePath("res://Logs/evidence/anime-motion/" + _view + "-validation.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"[AnimeMotion] SUMMARY pass={_pass} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private static int CountForbidden(Node root, bool animations)
    {
        int count = root is CollisionObject3D || root is ActRoguelike.Combat.Unit || (animations && root is AnimationPlayer) ? 1 : 0;
        foreach (Node child in root.GetChildren()) count += CountForbidden(child, animations);
        return count;
    }

    private static int CountTriangles(Node root)
    {
        // 只在验证结束时读取一次原网格，不参与运行期特效采样。
        int count = root is MeshInstance3D { Mesh: not null } mesh ? mesh.Mesh.GetFaces().Length / 3 : 0;
        foreach (Node child in root.GetChildren()) count += CountTriangles(child);
        return count;
    }

    private static void RecordMaterials(Node root, List<ulong> into)
    {
        if (root is MeshInstance3D { Mesh: not null } mesh)
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                if (mesh.GetSurfaceOverrideMaterial(i) is { } material) into.Add(material.GetInstanceId());
        foreach (Node child in root.GetChildren()) RecordMaterials(child, into);
    }

    private static T? Find<T>(Node root) where T : Node
    {
        if (root is T found) return found;
        foreach (Node child in root.GetChildren()) if (Find<T>(child) is { } result) return result;
        return null;
    }
}
