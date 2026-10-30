using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class LandingBurstAbility : IAbilityEnhancementConsumer
{
	public override Action CaptureRewardParameters()
	{
		var restoreBase = base.CaptureRewardParameters();
		var value = (DamageScale, _originalBurstShape);
		var shape = _hitbox?.GetNodeOrNull<CollisionShape3D>("Shape"); var originalShape = shape?.Shape;
		return () => { restoreBase(); (DamageScale, _originalBurstShape) = value; if (IsInstanceValid(shape)) shape!.Shape = originalShape; };
	}

	public void ApplyEnhancement(SkillEnhancementTuning tuning) { ConfigureEnhancement(tuning.DamageScale, tuning.SizeScale); }
}
