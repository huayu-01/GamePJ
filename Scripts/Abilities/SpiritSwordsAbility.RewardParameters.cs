using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class SpiritSwordsAbility : IAbilityEnhancementConsumer
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = KillExtensionSeconds;
		return () => { restoreBase(); KillExtensionSeconds = value; };
	}

	public void ApplyEnhancement(SkillEnhancementTuning tuning) { ConfigureKillExtension(tuning.KillExtensionSeconds); }
}
