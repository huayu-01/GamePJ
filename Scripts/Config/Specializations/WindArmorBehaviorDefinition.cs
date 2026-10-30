using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindArmorBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Reduction { get; set; } = 0.3f;
	public override SpecializationKind Kind => SpecializationKind.WindArmor;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindArmorBehavior(context, this);
}
