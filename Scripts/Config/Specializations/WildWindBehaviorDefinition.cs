using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WildWindBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float SizeScale { get; set; } = 1.5f;
	public override SpecializationKind Kind => SpecializationKind.WildWind;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WildWindBehavior(context, this);
}
