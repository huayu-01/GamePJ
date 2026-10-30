using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>飞出武器抵达后等待，再移向距离武器最近的敌人。</summary>
[Tool]
[GlobalClass]
public partial class WeaponSeekTuning : AbilityRewardTuning
{
	[Export] public float IntervalSeconds { get; set; } = 1f;
	[Export] public float TargetHeight { get; set; } = 0.9f;
	[Export] public float FlightSpeedScale { get; set; } = 2f;
	public override bool Supports(System.Type abilityType) => typeof(ScytheThrowAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) { ((ScytheThrowAbility)ability).ConfigureSeeking(IntervalSeconds, TargetHeight, FlightSpeedScale); }
	public override System.Collections.Generic.IEnumerable<string> Validate() { foreach (var error in RewardEffectValidation.NonNegative(this)) yield return error; if (FlightSpeedScale <= 0) yield return "FlightSpeedScale：必须大于零。"; }
}
