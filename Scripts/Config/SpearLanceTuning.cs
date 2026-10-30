using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class SpearLanceTuning : AbilityRewardTuning
{
	[Export] public float CooldownScale { get; set; } = .3f;
	public float CooldownReduction => 1 - CooldownScale;
	public override bool Supports(System.Type abilityType) => typeof(IPiercingSpearAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability)
	{
		var spear = (IPiercingSpearAbility)ability;
		spear.LanceUnlimitedTargets = true; ability.Cooldown *= CooldownScale;
	}
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(CooldownScale) || CooldownScale <= 0 || CooldownScale > 1) yield return "CooldownScale：必须在0到1之间。";
	}
}
