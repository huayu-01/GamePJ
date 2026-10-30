using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class BloodBattleBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float MaxDamageBonus { get; set; } = 2f;
	public override SpecializationKind Kind => SpecializationKind.BloodBattle;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new BloodBattleBehavior(context, this);
}
