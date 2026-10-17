using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>下落的强类型参数资源；武器只覆盖方向，共通技能保留速度与伤害权威。</summary>
[Tool]
[GlobalClass]
public partial class FallAttackTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public FallAttackAbility.DiveMode Mode { get; set; }
	[Export] public float DiveSpeed { get; set; } = 34f;
	[Export] public float StandardDiveSpeed { get; set; } = 34f;
	[Export] public float MaxDamageMultiplier { get; set; } = 2f;
	[Export] public float MaxDiveSeconds { get; set; } = 4f;
	[Export] public float LandRecoverSeconds { get; set; } = 0.08f;
	[Export] public float EventWaitSeconds { get; set; } = 2f;
	[Export] public float MaxDiveAngleDegrees { get; set; } = 35f;
	[Export] public float MaxAimDistance { get; set; } = 60f;
	[Export] public float GroundProbeDistance { get; set; } = 512f;
	[Export] public float GroundMinNormalY { get; set; } = 0.6f;

	public void ApplyTo(FallAttackAbility ability)
	{
		ability.DiveSpeed = DiveSpeed;
		ability.StandardDiveSpeed = StandardDiveSpeed;
		ability.MaxDamageMultiplier = MaxDamageMultiplier;
		ability.MaxDiveSeconds = MaxDiveSeconds;
		ability.LandRecoverSeconds = LandRecoverSeconds;
		ability.EventWaitSeconds = EventWaitSeconds;
		ApplyAimTo(ability);
	}
	public void ApplyAimTo(FallAttackAbility ability)
	{
		ability.HomingMode = Mode;
		ability.MaxDiveAngleDegrees = MaxDiveAngleDegrees;
		ability.MaxAimDistance = MaxAimDistance;
		ability.GroundProbeDistance = GroundProbeDistance;
		ability.GroundMinNormalY = GroundMinNormalY;
	}
}
