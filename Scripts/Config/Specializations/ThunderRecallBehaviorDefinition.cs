using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderRecallBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float LandingDamageScale { get; set; } = 1f;
	[Export] public float SpeedScale { get; set; } = 2f;
	[Export] public float DistanceScale { get; set; } = 1.5f;
	public override SpecializationKind Kind => SpecializationKind.ThunderRecall;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new ThunderRecallBehavior(context, this);
}
