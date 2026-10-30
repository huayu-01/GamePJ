using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderBlastBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Radius { get; set; } = 3f;
	public override SpecializationKind Kind => SpecializationKind.ThunderBlast;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new PassiveSpecializationBehavior(context, this);
}
