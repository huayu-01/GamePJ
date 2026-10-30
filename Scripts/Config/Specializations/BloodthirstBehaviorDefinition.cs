using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class BloodthirstBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Duration { get; set; } = 5;
	[Export] public float HealFraction { get; set; } = .05f;
	[Export] public float CritChance { get; set; } = .1f;
	public override SpecializationKind Kind => SpecializationKind.Bloodthirst;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new BloodthirstBehavior(context.Owner, Duration, HealFraction, CritChance);
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(Duration) || Duration <= 0) yield return "Duration：持续时间必须大于零且有限。";
		if (!float.IsFinite(HealFraction) || HealFraction < 0 || !float.IsFinite(CritChance) || CritChance < 0) yield return "HealFraction / CritChance：恢复比例和暴击加成必须非负且有限。";
	}
}
