using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class SpearThrowAbility
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = (RecallOnSecondPress, AutoRecallOnSettle, LanceUnlimitedTargets);
		return () => { restoreBase(); (RecallOnSecondPress, AutoRecallOnSettle, LanceUnlimitedTargets) = value; };
	}
}
