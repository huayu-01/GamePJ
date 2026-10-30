using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class BloodArmorBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float ArmorHealthRatio { get; set; } = 1f;
	public override SpecializationKind Kind => SpecializationKind.BloodArmor;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new BloodArmorBehavior(context, this);
}
