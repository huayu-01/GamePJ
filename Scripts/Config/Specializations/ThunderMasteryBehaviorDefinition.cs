using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderMasteryBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float DetonateScale { get; set; } = 0.3f;
	[Export] public int ExtraJumps { get; set; } = 2;
	public override SpecializationKind Kind => SpecializationKind.ThunderMastery;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new PassiveSpecializationBehavior(context, this);
}
