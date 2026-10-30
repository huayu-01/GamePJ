using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindSwordBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 18f;
	[Export] public float Interval { get; set; } = 1f;
	[Export] public float Lifetime { get; set; } = 3f;
	[Export] public float Radius { get; set; } = 0.35f;
	[Export] public float Range { get; set; } = 18f;
	[Export] public float Speed { get; set; } = 16f;
	[Export] public float TurnRate { get; set; } = 0f;
	public override SpecializationKind Kind => SpecializationKind.WindSword;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindSwordBehavior(context, this);
}
