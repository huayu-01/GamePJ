using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class StormBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float DamagePerSpeed { get; set; } = 0.02f;
	public override SpecializationKind Kind => SpecializationKind.Storm;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new StormBehavior(context, this);
}
