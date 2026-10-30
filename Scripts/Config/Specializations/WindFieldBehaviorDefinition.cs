using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class WindFieldBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 10f;
	[Export] public float Duration { get; set; } = 18f;
	[Export] public float Interval { get; set; } = 3f;
	[Export] public float Radius { get; set; } = 3f;
	[Export] public float SpawnRange { get; set; } = 8f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	public override SpecializationKind Kind => SpecializationKind.WindField;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new WindFieldBehavior(context, this);
}
