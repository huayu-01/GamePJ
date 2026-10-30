using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class ThunderArmorBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Damage { get; set; } = 14f;
	[Export] public int Jumps { get; set; } = 3;
	[Export] public float Range { get; set; } = 7f;
	[Export] public float ReflectDamage { get; set; } = 10f;
	public override SpecializationKind Kind => SpecializationKind.ThunderArmor;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new ThunderArmorBehavior(context, this);
}
