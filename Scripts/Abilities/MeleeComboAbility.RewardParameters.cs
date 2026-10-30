using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class MeleeComboAbility : IAbilityEnhancementConsumer
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = (_extraProjectile, _singleStep, _singleStepDamageScale, _singleStepSizeScale, _singleStepForwardReachScale);
		return () => { restoreBase(); (_extraProjectile, _singleStep, _singleStepDamageScale, _singleStepSizeScale, _singleStepForwardReachScale) = value; };
	}

	public void ApplyEnhancement(SkillEnhancementTuning tuning) { ConfigureSingleStep(tuning.ComboStepIndex, tuning.DamageScale, tuning.SizeScale, tuning.ForwardReachScale); }
}
