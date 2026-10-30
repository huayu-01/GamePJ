using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderPoolBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 25f;
	[Export] public float Duration { get; set; } = 6f;
	[Export] public float Radius { get; set; } = 4f;
	[Export] public float TickInterval { get; set; } = 1f;
	public override SpecializationKind Kind => SpecializationKind.ThunderPool;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new ThunderPoolBehavior(context, this);
}
