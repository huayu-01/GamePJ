using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Visual;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>复用正式森林、玩家和敌人执行器的独立实战入口；性能采样不录屏、不读回图片、不写玩家存档。</summary>
public partial class AnimeMotionCombatLab : Node3D
{
    private const int EnemyCount = 8;
    private const double PhaseSeconds = 7.2;
    private readonly List<Actor> _actors = new();
    private readonly List<PhaseResult> _results = new();
    private readonly List<Sample> _samples = new(6000);
    private PlayerController _player = null!;
    private Camera3D _camera = null!;
    private Label _label = null!;
    private Vector3 _center;
    private bool _ready, _benchmark, _captureRun, _saved, _finishing;
    private int _phase = -1, _limit = 8, _hits, _peakConcurrent, _bout;
    private bool _enabled = true;
    private double _phaseTime, _elapsed, _hudTimer;
    private ulong _lastFrame;
    private PhaseResult _current = null!;
    private string _output = "res://Logs/evidence/anime-motion-combat/";

    public override async void _Ready()
    {
        ProcessPhysicsPriority = -20;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--motion-combat-benchmark") _benchmark = true;
            if (arg == "--motion-combat-capture") _captureRun = true;
            if (arg.StartsWith("--motion-combat-output=", StringComparison.Ordinal)) _output = arg[23..];
        }
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(_output));
        GD.Seed(20261025);
        AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/ExpeditionForest.tscn").Instantiate());
        AddChild(new MapManager { Name = "MapManager" });
        _player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<PlayerController>();
        if (_benchmark) _player.MaxHealth = 1000000;
        _player.KnockbackResistance = 1;
        AddChild(_player);
        _center = GetNode<Marker3D>("ExpeditionForest/NatureArena/CombatMarkers/PlayerStart").GlobalPosition;
        _player.GlobalPosition = _center; _player.ResetPhysicsInterpolation();
        _player.Damaged += OnDamaged;
        _camera = new Camera3D { Name = "CombatOverview", Position = _center + new Vector3(0, 10, 17), Fov = 48, Current = true };
        AddChild(_camera); _camera.LookAt(_center + Vector3.Up * .6f);
        BuildHud(); Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_benchmark)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0; OS.LowProcessorUsageMode = false;
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        }
        while (MapManager.Instance?.IsReady != true) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!MapManager.Instance.TryValidPoint(_center, _center, out _center, _player)) throw new InvalidOperationException("测试中心没有可达地面");
        _player.GlobalPosition = _center; _player.ResetPhysicsInterpolation();
        for (int i = 0; i < EnemyCount; i++) SpawnActor(i);
        ResetBout();
        _ready = true;
        // 所有代理、材质与攻击先预热；第一轮编译和导航不计入结果。
        ulong warmupEnd = Time.GetTicksMsec() + (_benchmark ? 4500UL : 400UL);
        while (Time.GetTicksMsec() < warmupEnd) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _elapsed = 0; _phaseTime = 0; _lastFrame = Time.GetTicksUsec();
        if (_benchmark) { Input.ActionPress("fire"); NextPhase(); }
        RenderingServer.FramePostDraw += SampleFrame;
    }

    private void SpawnActor(int index)
    {
        Unit unit = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/HeavySlammer.tscn").Instantiate<Unit>();
        unit.Name = "StoneMotionEnemy" + index;
        var row = (EnemyConfig)ConfigRows.ById<EnemyConfig>("enemy_heavy_slammer")!.Duplicate();
        row.VisualProfile = RuntimeResources.Load<UnitVisualProfile>("res://Config/VisualProfiles/stone_golem_plus_motion.tres");
        if (_benchmark) row.MaxHealth = 1000000;
        row.ApplyTo(unit);
        var model = unit.GetNode<Node3D>("Model");
        AnimeMotionLab.ApplyPalette(model);
        var playback = NodeSearch.FindInSubtree<AnimationPlayer>(model)!;
        var clip = (Animation)playback.GetAnimation("slam").Duplicate(true);
        foreach (var cue in new[] { ("ATK", .55), ("FX_MOTION_BODY", .55), ("FX_MOTION_HANDS", .48) })
        {
            int track = clip.AddTrack(Animation.TrackType.Method);
            clip.TrackSetPath(track, new NodePath("."));
            clip.TrackInsertKey(track, cue.Item2, new Godot.Collections.Dictionary { ["method"] = "anim_event", ["args"] = new Godot.Collections.Array { cue.Item1 } });
            clip.TrackSetEnabled(track, false);
        }
        var library = new AnimationLibrary(); library.AddAnimation("EnemyAction", clip); playback.AddAnimationLibrary("skills", library);
        unit.GetNode<UnitAnimator>(UnitAnimator.NodeName).SkillLibraryDirectory = "";
        // 仅测试行采用冲锋动作定义，走正式动画事件、预警、命中体、移动所有权和恢复生命周期。
        var step = new EnemyActionStep { Action = new ChargeEnemyAction { Damage = 6, Radius = .75f, Speed = 10, TravelDistance = 7 },
            WindupSeconds = .65f, ActiveSeconds = .8f, RecoverSeconds = .35f };
        unit.GetNode<EnemyPlanAbility>("AbilityPlan").Plan = new EnemyActionPlan { Far = new[] { step }, Near = new[] { step },
            CooldownSeconds = 1.2f, TriggerDistance = 20, NearDistance = 3 };
        var angle = Mathf.Tau * index / EnemyCount;
        Vector3 desired = _center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 6f;
        if (!MapManager.Instance!.TryValidPoint(desired, _center, out Vector3 spawn, unit))
            throw new InvalidOperationException("敌人测试点没有通过连通性与净空校验");
        unit.Position = spawn + Vector3.Up * .05f;
        AddChild(unit);
        var actor = new Actor { Unit = unit, Spawn = unit.GlobalPosition, Animator = unit.GetNode<UnitAnimator>(UnitAnimator.NodeName),
            Ability = unit.GetNode<EnemyPlanAbility>("AbilityPlan"), Motion = UnitMotionFx.Configure(unit, model, row.VisualProfile)! };
        actor.CaptureRid = actor.Capture.GetNode<SubViewport>("ActorOnlyViewport").GetViewportRid();
        if (_benchmark) RenderingServer.ViewportSetMeasureRenderTime(actor.CaptureRid, true);
        _actors.Add(actor);
    }

    private void BuildHud()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        var panel = new PanelContainer { Position = new Vector2(24, 20), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.025f, .04f, .05f, .85f),
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 });
        canvas.AddChild(panel);
        _label = new Label { Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
        _label.AddThemeFontSizeOverride("font_size", 21); panel.AddChild(_label);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_ready) return;
        _elapsed += delta; _phaseTime += delta;
        if (_benchmark || _captureRun) _player.Heal(1000);
        int bout = (int)(_phaseTime / 3.2);
        if ((_benchmark || _captureRun) && bout != _bout) { _bout = bout; ResetBout(); }
        for (int i = 0; i < _actors.Count; i++) _actors[i].Motion.Enabled = _enabled && i < _limit;
        if (_benchmark && _phaseTime >= PhaseSeconds) NextPhase();
        if (_captureRun && _elapsed > 7.2) { GD.Print("[MotionCombat] CAPTURE PASS"); GetTree().Quit(); }
    }

    private void ResetBout()
    {
        _player.GlobalPosition = _center; _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
        foreach (var actor in _actors)
        {
            actor.Ability.ResetForLoadout(); actor.Animator.StopSkillAnimation();
            actor.Unit.GlobalPosition = actor.Spawn; actor.Unit.Velocity = Vector3.Zero; actor.Unit.ResetPhysicsInterpolation();
            actor.Motion.CancelAll();
        }
    }

    private void NextPhase()
    {
        if (_phase >= 0) { _current.Samples = _samples.ToArray(); _results.Add(_current); _samples.Clear(); }
        _phase++;
        if (_phase >= 12) { Finish(); return; }
        _limit = new[] { 1, 4, 8 }[_phase / 4];
        _enabled = _phase % 4 is 1 or 2;
        _phaseTime = 0; _bout = 0; ResetBout();
        _current = new PhaseResult { Index = _phase, Enabled = _enabled, Limit = _limit };
        GD.Print($"[MotionCombat] phase={_phase} enabled={_enabled} cap={_limit}");
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        _hudTimer -= delta;
        if (_hudTimer > 0) return; _hudTimer = .2;
        int active = _actors.Count(a => a.Capture.Active);
        _label.Text = $"森林实战 · 外轮廓动态模糊\n敌人 {_actors.Count(a => !a.Unit.IsDead)}   效果 {(_enabled ? "开启" : "关闭")}   上限 {_limit}   当前捕获 {active}\n"
            + (_benchmark ? $"性能对照 {_phase + 1}/12 · 预热后采样 · 无录屏\n" : "WASD 移动 · 左键攻击 · F3 效果开关 · F4 数量1/4/8 · F5 复位\n")
            + $"FPS {Engine.GetFramesPerSecond()}   玩家受击 {_hits}   实际攻击事件 {_actors.Sum(a => a.Ability.ExecutedAttackCount)}";
    }

    private void SampleFrame()
    {
        ulong now = Time.GetTicksUsec(); double frame = (now - _lastFrame) / 1000.0; _lastFrame = now;
        if (!_ready || _finishing) return;
        int active = 0; double gpuCapture = 0, cpuCapture = 0;
        foreach (var actor in _actors)
            if (actor.Capture.Active)
            {
                active++;
                if (_benchmark)
                {
                    Rid rid = actor.CaptureRid;
                    gpuCapture += RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
                    cpuCapture += RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid);
                }
            }
        _peakConcurrent = Mathf.Max(_peakConcurrent, active);
        if (_captureRun && !_saved && _elapsed > .55 && active > 0) { _saved = true; SaveStill(); }
        if (!_benchmark || _phase < 0 || _phaseTime < .4) return;
        Rid main = GetViewport().GetViewportRid();
        _samples.Add(new Sample(frame, RenderingServer.ViewportGetMeasuredRenderTimeGpu(main), gpuCapture,
            RenderingServer.ViewportGetMeasuredRenderTimeCpu(main) + cpuCapture + RenderingServer.GetFrameSetupTimeCpu(),
            Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000,
            Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000,
            RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame), active));
    }

    private void SaveStill()
    {
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath(_output + "combat.png"));
    }

    private void Finish()
    {
        _finishing = true; Input.ActionRelease("fire");
        bool passed = _results.All(p => p.Samples.Length > 100 && p.Samples.Any(s => s.MainGpuMs > 0)
            && (!p.Enabled || p.Samples.Count(s => s.Captures == p.Limit) > 20))
            && _hits > 0 && _actors.All(a => a.Ability.ExecutedAttackCount > 0) && _peakConcurrent == EnemyCount;
        var report = new { passed, gpu = RenderingServer.GetVideoAdapterName(), cpu = OS.GetProcessorName(),
            engine = Engine.GetVersionInfo()["string"].AsString(), renderer = "Forward+", build = "Godot editor project / Debug C#",
            resolution = GetViewport().GetVisibleRect().Size.ToString(), vsync = DisplayServer.WindowGetVsyncMode().ToString(),
            enemy_count = EnemyCount, actual_player_hits = _hits, executed_attacks = _actors.Sum(a => a.Ability.ExecutedAttackCount),
            peak_captures = _peakConcurrent, phase_seconds = PhaseSeconds, warmup_per_phase_seconds = .4,
            order = "per cap 1/4/8: OFF ON ON OFF; same 8 real AI enemies, reset every3.2s", recording = false,
            phases = _results.Select(p => new { p.Index, p.Limit, p.Enabled, frames = p.Samples.Length,
                full_concurrency_frames = p.Samples.Count(s => s.Captures == p.Limit),
                frame_ms = Summary(p.Samples.Select(s => s.FrameMs)),
                gpu_total_ms = Summary(p.Samples.Select(s => s.MainGpuMs + s.CaptureGpuMs)),
                gpu_active_ms = Summary(p.Samples.Where(s => !p.Enabled || s.Captures == p.Limit).Select(s => s.MainGpuMs + s.CaptureGpuMs)),
                render_cpu_ms = Summary(p.Samples.Select(s => s.RenderCpuMs)),
                process_ms = Summary(p.Samples.Select(s => s.ProcessMs)), physics_ms = Summary(p.Samples.Select(s => s.PhysicsMs)),
                draw_calls = Summary(p.Samples.Select(s => (double)s.DrawCalls)) }) };
        string dir = ProjectSettings.GlobalizePath(_output);
        File.WriteAllText(dir + "benchmark.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        var csv = new StringBuilder("phase,cap,enabled,frame_ms,main_gpu_ms,capture_gpu_ms,render_cpu_ms,process_ms,physics_ms,draw_calls,active_captures\n");
        foreach (var p in _results) foreach (var s in p.Samples)
            csv.AppendLine(FormattableString.Invariant($"{p.Index},{p.Limit},{p.Enabled},{s.FrameMs:F5},{s.MainGpuMs:F5},{s.CaptureGpuMs:F5},{s.RenderCpuMs:F5},{s.ProcessMs:F5},{s.PhysicsMs:F5},{s.DrawCalls},{s.Captures}"));
        File.WriteAllText(dir + "frames.csv", csv.ToString());
        GD.Print($"[MotionCombat] BENCHMARK {(passed ? "PASS" : "FAIL")} hits={_hits} peak={_peakConcurrent}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private static object Summary(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length == 0 ? new { n = 0, mean = 0d, median = 0d, p95 = 0d } :
            new { n = sorted.Length, mean = sorted.Average(), median = sorted[sorted.Length / 2], p95 = sorted[(int)((sorted.Length - 1) * .95)] };
    }

    private void OnDamaged(DamageResult _, HitInstance __) => _hits++;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_benchmark || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.PhysicalKeycode == Key.F3) _enabled = !_enabled;
        else if (key.PhysicalKeycode == Key.F4) _limit = _limit == 1 ? 4 : _limit == 4 ? 8 : 1;
        else if (key.PhysicalKeycode == Key.F5) ResetBout();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePostDraw -= SampleFrame;
        Input.ActionRelease("fire");
        if (IsInstanceValid(_player)) _player.Damaged -= OnDamaged;
        _actors.Clear();
    }

    private sealed class Actor
    {
        public Unit Unit = null!;
        public UnitAnimator Animator = null!;
        public EnemyPlanAbility Ability = null!;
        public UnitMotionFx Motion = null!;
        public AnimeMotionCapture Capture => Motion.Capture!;
        public Rid CaptureRid;
        public Vector3 Spawn;
    }
    private sealed class PhaseResult
    {
        public int Index, Limit;
        public bool Enabled;
        public Sample[] Samples = Array.Empty<Sample>();
    }
    private readonly record struct Sample(double FrameMs, double MainGpuMs, double CaptureGpuMs, double RenderCpuMs,
        double ProcessMs, double PhysicsMs, ulong DrawCalls, int Captures);
}
