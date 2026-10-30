using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WarSpearBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 50f;
	[Export] public float Lifetime { get; set; } = 4f;
	[Export] public float Range { get; set; } = 18f;
	[Export] public float Speed { get; set; } = 40f;
	[Export] public float TurnRate { get; set; } = 360f;
	public override SpecializationKind Kind => SpecializationKind.WarSpear;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WarSpearBehavior(context, this);
}
