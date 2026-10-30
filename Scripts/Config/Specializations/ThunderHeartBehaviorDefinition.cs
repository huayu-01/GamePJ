using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderHeartBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 14f;
	[Export] public int Jumps { get; set; } = 3;
	[Export] public float Range { get; set; } = 7f;
	[Export] public float CooldownSeconds { get; set; } = 1f;
	public override SpecializationKind Kind => SpecializationKind.ThunderHeart;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new ThunderHeartBehavior(context, this);
}
