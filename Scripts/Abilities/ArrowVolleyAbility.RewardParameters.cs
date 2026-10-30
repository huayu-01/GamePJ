using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class ArrowVolleyAbility
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var held = (_heldFireInterval, _heldFireCount, _heldFireDamageScale, HoldToCharge, HoldToMaintain);
		var value = (_volleyCountOverride, _damagePerLostArrow, _seekRange, _seekTurnRate, _seekDistinctHostiles, RampMaxCount, RampWindowSeconds, RampShotIntervalSeconds, ChargedShotThresholdSeconds, ChargedShotSizeScale, ChargedShotPierce);
		return () => { restoreBase(); (_heldFireInterval, _heldFireCount, _heldFireDamageScale, HoldToCharge, HoldToMaintain) = held; (_volleyCountOverride, _damagePerLostArrow, _seekRange, _seekTurnRate, _seekDistinctHostiles, RampMaxCount, RampWindowSeconds, RampShotIntervalSeconds, ChargedShotThresholdSeconds, ChargedShotSizeScale, ChargedShotPierce) = value; };
	}
}
