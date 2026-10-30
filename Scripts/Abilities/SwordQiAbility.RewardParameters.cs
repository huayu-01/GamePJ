using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class SwordQiAbility : IAbilityEnhancementConsumer
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = (_enhancedSize, _originalSweep, Damage, SweepMaxHits, SweepRadius);
		return () => { restoreBase(); (_enhancedSize, _originalSweep, Damage, SweepMaxHits, SweepRadius) = value; };
	}

	public void ApplyEnhancement(SkillEnhancementTuning tuning) { ConfigureEnhancement(tuning.DamageScale, tuning.SizeScale, tuning.ProjectileSweepMaxHits, tuning.ProjectileSweepRadius); }
}
