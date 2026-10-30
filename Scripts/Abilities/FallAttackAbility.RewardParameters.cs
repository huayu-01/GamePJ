using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class FallAttackAbility : IAbilityEnhancementConsumer
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = LandingKnockback;
		return () => { restoreBase(); LandingKnockback = value; };
	}

	public void ApplyEnhancement(SkillEnhancementTuning tuning) { if (tuning.LandingKnockback >= 0) ConfigureLandingKnockback(tuning.LandingKnockback); }
}
