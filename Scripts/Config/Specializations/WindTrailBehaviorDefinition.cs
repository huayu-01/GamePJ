using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindTrailBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 8f;
	[Export] public float Duration { get; set; } = 3f;
	[Export] public float Interval { get; set; } = 0.4f;
	[Export] public float Radius { get; set; } = 1.5f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	public override SpecializationKind Kind => SpecializationKind.WindTrail;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindTrailBehavior(context, this);
}
