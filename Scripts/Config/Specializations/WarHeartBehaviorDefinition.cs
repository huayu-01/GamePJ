using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WarHeartBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float ConversionRatio { get; set; } = 2f;
	public override SpecializationKind Kind => SpecializationKind.WarHeart;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WarHeartBehavior(context, this);
}
