using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindHeartBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 12f;
	[Export] public float Radius { get; set; } = 4f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	public override SpecializationKind Kind => SpecializationKind.WindHeart;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindHeartBehavior(context, this);
}
