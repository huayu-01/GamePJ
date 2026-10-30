using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class ScytheThrowAbility
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = (SeekIntervalSeconds, SeekTargetHeight, SeekFlightSpeedScale);
		return () => { restoreBase(); (SeekIntervalSeconds, SeekTargetHeight, SeekFlightSpeedScale) = value; };
	}
}
