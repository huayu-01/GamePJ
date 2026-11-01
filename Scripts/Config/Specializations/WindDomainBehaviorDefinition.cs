using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindDomainBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 15f;
	[Export] public float Radius { get; set; } = 3f;
	[Export] public float TickInterval { get; set; } = 1f;
	[Export] public float HoverExtensionSeconds { get; set; } = 2f;
	[Export] public float HoverExtensionInterval { get; set; } = 1f;
	[Export] public float ContactHeight { get; set; } = 3f;
	/// <summary>角色获得**所有**浮空时的时长倍率（2026-11-01 用户口径 A：+50%）。</summary>
	[Export] public float HoverDurationScale { get; set; } = 1.5f;
	/// <summary>每次跳跃额外授予的浮空秒数（受 <see cref="HoverDurationScale"/> 放大）。</summary>
	[Export] public float JumpHoverSeconds { get; set; } = 1f;
	/// <summary>「跳跃授予浮空」的重复间隔（秒）。</summary>
	[Export] public float JumpHoverRepeatSeconds { get; set; } = 3f;
	public override SpecializationKind Kind => SpecializationKind.WindDomain;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindDomainBehavior(context, this);
}
