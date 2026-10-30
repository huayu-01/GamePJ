using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindDomainBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 15f;
	[Export] public float Radius { get; set; } = 5f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	[Export] public float HoverExtensionSeconds { get; set; } = 2f;
	[Export] public float HoverExtensionInterval { get; set; } = 1f;
	[Export] public float ContactHeight { get; set; } = 3f;
	public override SpecializationKind Kind => SpecializationKind.WindDomain;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindDomainBehavior(context, this);
}
