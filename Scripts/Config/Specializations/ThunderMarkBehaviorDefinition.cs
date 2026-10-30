using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderMarkBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 40f;
	[Export] public float Delay { get; set; } = 4f;
	public override SpecializationKind Kind => SpecializationKind.ThunderMark;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new ThunderMarkBehavior(context, this);
}
