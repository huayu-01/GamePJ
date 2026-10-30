using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ViolenceBehaviorDefinition : SpecializationBehaviorDefinition
{

	public override SpecializationKind Kind => SpecializationKind.Violence;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new PassiveSpecializationBehavior(context, this);
}
