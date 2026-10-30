using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实模型、真实动画事件与单位生命周期的外轮廓组件专项；无玩家、存档和手工计时兜底。</summary>
public partial class MotionFxIntegrationProbe : Node3D
{
    private int _passed, _failed;
    private Unit _unit = null!;
    private UnitVisualProfile _profile = null!;
    private UnitAnimator _animator = null!;
    private UnitMotionFx _motion = null!;
    private void Check(bool value, string text)
    {
        if (value) _passed++; else _failed++;
        GD.Print($"[MotionFxIntegration] {(value ? "PASS" : "FAIL")} {text}");
    }
    private async Task Physics(int frames)
    {
        for (int i = 0; i < frames; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private async Task Draw() => await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            await Physics(4);
            var floor = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0, Position = Vector3.Down * .5f };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) } });
            floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(40, 1, 40) } }); AddChild(floor);
            AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#cbc9c1"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightEnergy = .6f } });
            AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -20, 0) });
            var camera = new Camera3D { Position = new Vector3(1, 4, 10), Current = true }; AddChild(camera); camera.LookAt(Vector3.Up);
            _profile = RuntimeResources.Load<UnitVisualProfile>("res://Config/VisualProfiles/stone_golem_plus_motion.tres");
            Check(!_profile.Validate().Any(), "制作档案、预设及骨骼引用通过真实模型校验");
            _unit = Spawn("Primary", Vector3.Zero);
            await Physics(4); await Draw();
            _animator = _unit.GetNode<UnitAnimator>(UnitAnimator.NodeName);
            _motion = _unit.GetNode<UnitMotionFx>(UnitMotionFx.NodeName);
            Check(_motion.Effects.Count == 3 && _motion.Capture?.MeshCount > 0, "仅凭视觉档案自动安装三条挂点与一份模型捕获");
            var poolIds = _motion.Effects.Select(fx => fx.GetInstanceId()).ToArray();
            var capture = _motion.Capture!;
            ulong captureId = capture.GetInstanceId();
            Check(!_motion.HasActiveEffects && !capture.Active && !_motion.IsPhysicsProcessing(), "初始化预热后待机不绘制、不轮询");
            Check(!_motion.Play("missing") && _motion.Effects.Count == 3, "未知函数预设不会创建资源或触发效果");
            _unit.Movement!.BeginVelocityOverride(this, Vector3.Right * 3);
            Check(_motion.Play("body_dash"), "统一函数入口可以播放全身预设");
            // 物理可能在一个渲染帧内补步，额外等待捕获回调提交，避免把首次请求误当作已绘制。
            await Physics(8); await Draw(); await Draw();
            Check(_motion.Effects[0].LiveSamples > 1 && _motion.Effects[0].BlurFrames > 0 && capture.RequestedFrames > 0,
                $"函数触发真实移动轨迹和GPU外轮廓绘制（samples={_motion.Effects[0].LiveSamples}, blur={_motion.Effects[0].BlurFrames}, capture={capture.RequestedFrames}, speed={_unit.Velocity.Length():0.00}）");
            _motion.Cancel("body_dash");
            Check(!_motion.HasActiveEffects && !capture.Active && _motion.Effects.All(fx => !fx.BlurVisible && !fx.TrailVisible), "取消当帧清空拖尾并关闭共享捕获");

            _animator.PlaySkillAnimation("Integration", 1);
            int events = _motion.EventPlayCount;
            await Physics(17);
            Check(_motion.EventPlayCount == events + 2, "真实动画跨越两个特效事件，统一转发到Play");
            Check(_motion.Effects[0].RemainingWindow > 0 && _motion.Effects.Skip(1).All(fx => fx.RemainingWindow == 0), "停止双拳事件只结束指定预设，保留全身窗口");
            _animator.StopSkillAnimation();
            Check(!_motion.HasActiveEffects && !capture.Active, "动作打断立即关闭表现，不等待尾部寿命");
            events = _motion.EventPlayCount;
            _animator.PlaySkillAnimation("Integration", 1);
            _animator.PlaySkillAnimation("Plain", 1);
            await Physics(12);
            Check(_motion.EventPlayCount == events, "动作交接后旧动画的未到达事件不再触发");

            _animator.PlaySkillAnimation("Plain", 1); _motion.Play("body_dash"); await Physics(6);
            float normal = _motion.Effects[0].RemainingWindow;
            _animator.PlaySkillAnimation("Plain", 2); _motion.Play("body_dash"); await Physics(6);
            Check(_motion.Effects[0].ClockScale == 2 && _motion.Effects[0].RemainingWindow < normal - .06f, "动画两倍速同时推进表现窗口和衰减时钟");
            _unit.ImpactPause!.Request(.25f, 0);
            float held = _motion.Effects[0].RemainingWindow;
            await Physics(6);
            Check(_motion.Effects[0].ClockScale == 0 && Mathf.Abs(_motion.Effects[0].RemainingWindow - held) < .02f, "局部顿帧冻结窗口，不产生额外时间推进");
            _unit.ImpactPause.Clear(); await Physics(2);
            Check(_motion.Effects[0].ClockScale == 2, "顿帧结束恢复原动画倍速");
            _animator.PlaySkillAnimation("Plain", 1); _motion.Play("body_dash"); await Physics(2); await Draw();
            held = _motion.Effects[0].RemainingWindow;
            GetTree().Paused = true;
            for (int i = 0; i < 5; i++) await Draw();
            Check(Mathf.IsEqualApprox(_motion.Effects[0].RemainingWindow, held) && !capture.Active, "全局暂停冻结表现并停止额外模型捕获");
            GetTree().Paused = false; await Physics(3);
            Check(_motion.Effects[0].RemainingWindow < held, "全局恢复后正常继续窗口");
            _animator.StopSkillAnimation();

            Unit other = Spawn("Independent", new Vector3(-3, 0, 0)); await Physics(3);
            var otherMotion = other.GetNode<UnitMotionFx>(UnitMotionFx.NodeName);
            _animator.RaiseAnimationEvent("FX_MOTION_HANDS");
            Check(_motion.HasActiveEffects && !otherMotion.HasActiveEffects && otherMotion.EventPlayCount == 0, "事件只影响本单位，多个单位共用预设不串状态");
            _motion.Enabled = false;
            Check(!_motion.Play("body_dash") && !_motion.HasActiveEffects && !capture.Active, "关闭组件后立即停止，函数和事件均不可绕过开关");
            _motion.Enabled = true;
            for (int i = 0; i < 4; i++) { _motion.Play("body_dash"); _motion.Play("double_fist"); _motion.CancelAll(); }
            Check(_motion.Effects.Select(fx => fx.GetInstanceId()).SequenceEqual(poolIds) && _motion.Capture?.GetInstanceId() == captureId, "反复调用复用固定挂点与同一捕获，不按事件创建视口");

            _motion.Play("body_dash"); _motion.Stop("body_dash"); await Physics(12); await Draw();
            Check(!_motion.HasActiveEffects && !capture.Active, "Stop停止续采样，尾部自然收尽并关闭回调");
            Check(_profile.MotionEffects[0].Duration == .65f && _profile.MotionEffects[1].Duration == .22f, "共享资源未被单位计时或倍速修改");
            var invalid = new MotionFxPreset { Id = "invalid", Bones = new[] { "AbsentBone" } };
            Check(invalid.Validate(_profile.ResolveSkeleton(_unit.GetNode("Model"))).Any(), "缺失挂点在制作校验阶段明确报告"); invalid.Dispose();
            _motion.Play("body_dash");
            var empty = (UnitVisualProfile)_profile.Duplicate(); empty.MotionEffects = Array.Empty<MotionFxPreset>();
            _animator.BindVisual(_unit.GetNode("Model"), empty);
            Check(_motion.Capture == null && !_motion.HasActiveEffects && !capture.Active, "切换无特效档案立即释放旧绑定和绘制");
            await Physics(2);
            _animator.BindVisual(_unit.GetNode("Model"), _profile);
            Check(_motion.Effects.Count == 3 && _motion.Capture != null && _motion.BindingRevision == 2, "换回档案重新绑定挂点，预设名称继续有效");
            Check(otherMotion.Capture != null && otherMotion.Capture.GetInstanceId() != _motion.Capture!.GetInstanceId(), "换模不影响其他单位的捕获实例");
            _motion.Play("body_dash"); _unit.ForceKill();
            Check(!_motion.HasActiveEffects && !_motion.Capture!.Active, "死亡同步取消全部预设及角色捕获");
            other.QueueFree(); await Physics(2);
            Check(!IsInstanceValid(otherMotion), "单位退树时组件、事件订阅与捕获一起销毁");
        }
        catch (Exception ex) { Check(false, ex.ToString()); }
        finally { GetTree().Paused = false; }
        string output = ProjectSettings.GlobalizePath("res://Logs/evidence/anime-motion/integration-validation.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { passed = _failed == 0, pass = _passed, fail = _failed }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"[MotionFxIntegration] SUMMARY pass={_passed} fail={_failed}");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private Unit Spawn(string name, Vector3 position)
    {
        var unit = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/HeavySlammer.tscn").Instantiate<Unit>();
        unit.Name = name; unit.ProcessMode = ProcessModeEnum.Pausable;
        unit.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled;
        var row = (EnemyConfig)RuntimeResources.Load<EnemyConfig>("res://Config/Tables/Enemies/enemy_heavy_slammer.tres").Duplicate(); row.VisualProfile = _profile; row.ApplyTo(unit);
        unit.GetNode<UnitAnimator>(UnitAnimator.NodeName).SkillLibraryDirectory = "";
        Node3D model = unit.GetNode<Node3D>("Model"); AnimeMotionLab.ApplyPalette(model);
        var player = NodeSearch.FindInSubtree<AnimationPlayer>(model)!;
        var plain = (Animation)player.GetAnimation("slam").Duplicate(true);
        var clip = (Animation)plain.Duplicate(true);
        foreach (var cue in new[] { ("FX_MOTION_HANDS", .08), ("FX_MOTION_BODY", .10), ("FX_MOTION_HANDS_STOP", .15) })
        {
            int track = clip.AddTrack(Animation.TrackType.Method); clip.TrackSetPath(track, new NodePath("."));
            clip.TrackInsertKey(track, cue.Item2, new Godot.Collections.Dictionary { ["method"] = "anim_event", ["args"] = new Godot.Collections.Array { cue.Item1 } });
            clip.TrackSetEnabled(track, false);
        }
        var library = new AnimationLibrary(); library.AddAnimation("Integration", clip); library.AddAnimation("Plain", plain);
        player.AddAnimationLibrary("skills", library);
        unit.Position = position + Vector3.Up * .04f; AddChild(unit);
        return unit;
    }
}
