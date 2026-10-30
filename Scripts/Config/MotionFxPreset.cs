using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>一次外轮廓拖抹的只读制作参数；计时和捕获由每个单位实例持有。</summary>
[Tool, GlobalClass]
public partial class MotionFxPreset : Resource, INumericContentIdentity
{
    [Export] public int NumericId { get; set; }
    private string _legacyId = string.Empty;
    [Export] public string Id { get => NumericContentIdentity.Key(_legacyId, NumericId); set => _legacyId = value; }
    public override void _ValidateProperty(Godot.Collections.Dictionary property) => NumericContentIdentity.HideLegacy(property);
    [Export] public string EventName { get; set; } = string.Empty;
    [Export] public string StopEventName { get; set; } = string.Empty;
    [Export] public string[] Bones { get; set; } = Array.Empty<string>();
    [Export] public Vector3 Offset { get; set; }
    [Export] public float Duration { get; set; } = .22f;
    [Export] public bool FollowAnimationSpeed { get; set; } = true;
    [Export] public bool CancelOnActionChange { get; set; } = true;
    [Export] public Vector2 BlurRadius { get; set; } = new(.48f, .48f);
    [Export] public float BlurSeconds { get; set; } = .1f;
    [Export] public float BlurMaxDistance { get; set; } = .85f;
    [Export] public float ContourOpacity { get; set; } = .85f;
    [Export] public float Width { get; set; } = .22f;
    [Export] public float Height { get; set; } = .23f;
    [Export] public float Lifetime { get; set; } = .1f;
    [Export] public float TearOpacity { get; set; } = .32f;
    [Export] public Color Tint { get; set; } = new("#9ca9bd");
    [Export] public Color Shade { get; set; } = new("#424f68");

    public IEnumerable<string> Validate(Skeleton3D? skeleton)
    {
        if (string.IsNullOrWhiteSpace(Id)) yield return "Id：必须填写播放预设名。";
        if (!float.IsFinite(Duration) || Duration < .01f || Duration > 2) yield return "Duration：必须在0.01到2秒之间。";
        if (!Offset.IsFinite() || !BlurRadius.IsFinite() || BlurRadius.X <= 0 || BlurRadius.Y <= 0) yield return "Offset / BlurRadius：挂点和半径必须有限，半径必须大于零。";
        if (!float.IsFinite(BlurSeconds) || BlurSeconds <= 0 || !float.IsFinite(BlurMaxDistance) || BlurMaxDistance <= 0) yield return "BlurSeconds / BlurMaxDistance：必须为正有限值。";
        if (!float.IsFinite(Lifetime) || Lifetime < .04f || !float.IsFinite(Width) || Width <= 0 || !float.IsFinite(Height) || Height <= 0) yield return "Lifetime / Width / Height：必须为正有限值，寿命至少0.04秒。";
        if (!float.IsFinite(ContourOpacity) || ContourOpacity < 0 || ContourOpacity > 1 || !float.IsFinite(TearOpacity) || TearOpacity < 0 || TearOpacity > 1) yield return "ContourOpacity / TearOpacity：必须在0到1之间。";
        foreach (string bone in Bones)
            if (string.IsNullOrWhiteSpace(bone) || skeleton == null || skeleton.FindBone(bone) < 0) yield return $"Bones：模型缺少骨骼 {bone}。";
    }
}
