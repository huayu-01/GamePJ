using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WarAfterimageBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Chance { get; set; } = 0.3f;
	public override SpecializationKind Kind => SpecializationKind.WarAfterimage;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WarAfterimageBehavior(context, this);
}
