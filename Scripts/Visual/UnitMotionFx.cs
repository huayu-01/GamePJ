using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>单位的外轮廓表现入口；函数与动画事件共用预设，始终只读模型，不介入能力或命中。</summary>
public partial class UnitMotionFx : Node3D
{
    public const string NodeName = "MotionFx";
    private bool _enabled = true;
    [Export] public bool Enabled { get => _enabled; set { _enabled = value; if (!value) CancelAll(); } }
    public AnimeMotionCapture? Capture { get; private set; }
    public IReadOnlyList<AnimeMotionFx> Effects => _effects;
    public int PlayCount { get; private set; }
    public int EventPlayCount { get; private set; }
    public int BindingRevision { get; private set; }
    public bool HasActiveEffects => _effects.Exists(fx => fx.IsPhysicsProcessing());
    private readonly List<AnimeMotionFx> _effects = new();
    private readonly Dictionary<string, Group> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Group> _events = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Group> _stopEvents = new(StringComparer.Ordinal);
    private Unit _unit = null!;
    private UnitAnimator _animator = null!;
    private Node3D? _model;
    private Skeleton3D? _skeleton;
    private UnitVisualProfile? _profile;
    private Node3D? _pool;

    public static UnitMotionFx? Configure(Unit unit, Node3D? model, UnitVisualProfile? profile)
    {
        var component = unit.GetNodeOrNull<UnitMotionFx>(NodeName);
        if (model == null || profile == null || profile.MotionEffects.Length == 0)
        {
            component?.ClearBinding();
            return component;
        }
        if (component == null) { component = new UnitMotionFx { Name = NodeName }; unit.AddChild(component); }
        component.BindVisual(model, profile);
        return component;
    }

    public override void _Ready()
    {
        _unit = GetParent<Unit>(); _animator = UnitAnimator.Ensure(_unit);
        ProcessPhysicsPriority = 90; ProcessPriority = -20;
        _animator.SkillEvent += OnEvent; _animator.VisualActionChanged += OnActionChanged;
        _unit.Died += OnDied;
        SetPhysicsProcess(false); SetProcess(false);
    }

    public void BindVisual(Node3D model, UnitVisualProfile profile)
    {
        if (_model == model && ReferenceEquals(_profile, profile)) return;
        ClearBinding(); BindingRevision++;
        _model = model; _profile = profile; _skeleton = profile.ResolveSkeleton(model);
        _pool = new Node3D { Name = "MotionFxPool" }; AddChild(_pool);
        Capture = new AnimeMotionCapture { Name = "SharedCapture" }; _pool.AddChild(Capture);
        foreach (var preset in profile.MotionEffects)
        {
            if (preset == null) continue;
            string[] errors = preset.Validate(_skeleton).ToArray();
            if (errors.Length > 0 || _groups.ContainsKey(preset.Id))
            {
                GD.PushWarning($"[UnitMotionFx] 跳过无效预设 {preset.Id}：{string.Join("；", errors)}"); continue;
            }
            var group = new Group { Preset = preset };
            int count = Mathf.Max(1, preset.Bones.Length);
            for (int i = 0; i < count; i++)
            {
                var anchor = new Node3D(); _pool.AddChild(anchor);
                var fx = new AnimeMotionFx { ProcessPhysicsPriority = 100, Width = preset.Width, Height = preset.Height,
                    BlurRadius = preset.BlurRadius, BlurSeconds = preset.BlurSeconds, BlurMaxDistance = preset.BlurMaxDistance,
                    ContourOpacity = preset.ContourOpacity, TearOpacity = preset.TearOpacity, Lifetime = preset.Lifetime,
                    Tint = preset.Tint, Shade = preset.Shade };
                _pool.AddChild(fx); fx.Bind(anchor); fx.SetBlurCapture(Capture);
                group.Targets.Add((anchor, preset.Bones.Length == 0 ? -1 : _skeleton!.FindBone(preset.Bones[i]), fx));
                _effects.Add(fx);
            }
            _groups.Add(preset.Id, group);
            if (!string.IsNullOrEmpty(preset.EventName)) _events.TryAdd(preset.EventName, group);
            if (!string.IsNullOrEmpty(preset.StopEventName)) _stopEvents.TryAdd(preset.StopEventName, group);
        }
        // 场景的相机可能晚于单位进树；只等到首次可用，之后待机不保留逐帧轮询。
        SetPhysicsProcess(!WarmCapture());
    }

    public bool Play(string presetId)
    {
        if (!Enabled || !IsInstanceValid(_unit) || _unit.IsDead || !IsInstanceValid(_model)
            || !_groups.TryGetValue(presetId, out var group) || !WarmCapture()) return false;
        UpdateAnchors(group);
        foreach (var target in group.Targets)
        {
            target.Fx.ClockScale = Clock(group.Preset);
            target.Fx.Begin(group.Preset.Duration);
        }
        PlayCount++; SetPhysicsProcess(true); SetProcess(true);
        return true;
    }

    public void Stop(string presetId)
    {
        if (_groups.TryGetValue(presetId, out var group)) foreach (var target in group.Targets) target.Fx.Stop();
    }

    public void Cancel(string presetId)
    {
        if (_groups.TryGetValue(presetId, out var group)) foreach (var target in group.Targets) target.Fx.Cancel();
        if (!HasActiveEffects) CancelAll();
    }

    public void CancelAll()
    {
        foreach (var fx in _effects) fx.Cancel();
        Capture?.Cancel(); SetPhysicsProcess(false); SetProcess(false);
    }

    private bool WarmCapture()
    {
        if (Capture == null || !IsInstanceValid(_model) || GetViewport().GetCamera3D() is not { } camera) return false;
        if (Capture.MeshCount == 0)
        {
            Node scene = GetTree().CurrentScene ?? GetTree().Root;
            var environment = _unit.GetWorld3D().Environment;
            var light = NodeSearch.FindInSubtree<DirectionalLight3D>(scene);
            Capture.Bind(_model!, camera, environment, light);
        }
        else Capture.SetCamera(camera);
        return true;
    }

    private float Clock(MotionFxPreset preset) => _unit.ImpactPause?.IsHolding == true ? 0 :
        preset.FollowAnimationSpeed ? _animator.PlaybackSpeed : 1;

    private void UpdateAnchors(Group group)
    {
        foreach (var target in group.Targets)
            target.Anchor.GlobalPosition = target.Bone < 0 ? _unit.GlobalTransform * group.Preset.Offset :
                _skeleton!.GlobalTransform * (_skeleton.GetBoneGlobalPose(target.Bone) * group.Preset.Offset);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled || !IsInstanceValid(_model) || _unit.IsDead) { CancelAll(); return; }
        WarmCapture();
        foreach (var group in _groups.Values)
        {
            bool active = false;
            foreach (var target in group.Targets)
            {
                target.Fx.ClockScale = Clock(group.Preset);
                active |= target.Fx.IsPhysicsProcessing();
            }
            if (active) UpdateAnchors(group);
        }
        if (!HasActiveEffects && Capture?.MeshCount > 0) CancelAll();
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_model) || _unit.IsDead) { CancelAll(); return; }
        if (GetViewport().GetCamera3D() is { } camera) Capture?.SetCamera(camera);
        foreach (var group in _groups.Values) foreach (var target in group.Targets) target.Fx.ClockScale = Clock(group.Preset);
    }

    private void OnEvent(string name, ulong playback)
    {
        if (playback != _animator.SkillPlaybackId) return;
        if (_events.TryGetValue(name, out var group) && Play(group.Preset.Id)) EventPlayCount++;
        if (_stopEvents.TryGetValue(name, out var stopped)) Stop(stopped.Preset.Id);
    }

    private void OnActionChanged()
    {
        foreach (var group in _groups.Values)
            if (group.Preset.CancelOnActionChange) foreach (var target in group.Targets) target.Fx.Cancel();
        if (!HasActiveEffects) CancelAll();
    }

    private void OnDied(HitInstance _) => CancelAll();

    public override void _Notification(int what)
    {
        if (what == NotificationPaused) Capture?.Cancel();
    }

    public void ClearBinding()
    {
        CancelAll(); _groups.Clear(); _events.Clear(); _stopEvents.Clear(); _effects.Clear();
        _pool?.QueueFree(); _pool = null; Capture = null; _model = null; _skeleton = null; _profile = null;
    }

    public override void _ExitTree()
    {
        CancelAll();
        if (IsInstanceValid(_animator)) { _animator.SkillEvent -= OnEvent; _animator.VisualActionChanged -= OnActionChanged; }
        if (IsInstanceValid(_unit)) _unit.Died -= OnDied;
        _groups.Clear(); _events.Clear(); _stopEvents.Clear(); _effects.Clear();
        _model = null; _skeleton = null; _profile = null; Capture = null; _pool = null;
    }

    private sealed class Group
    {
        public MotionFxPreset Preset = null!;
        public readonly List<(Node3D Anchor, int Bone, AnimeMotionFx Fx)> Targets = new();
    }
}
