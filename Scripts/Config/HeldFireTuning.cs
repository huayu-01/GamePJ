using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>把指定齐射技能改成按住连射；参数通过来源租约撤销，不影响其他技能。</summary>
[Tool, GlobalClass]
public partial class HeldFireTuning : AbilityRewardTuning
{
	[Export] public float IntervalSeconds { get; set; } = .3f;
	[Export] public float DamageScale { get; set; } = .5f;
	[Export] public int ProjectileCount { get; set; } = 1;
	public override bool Supports(System.Type abilityType) => typeof(ArrowVolleyAbility).IsAssignableFrom(abilityType);
	public override void Apply(Ability ability) => ((ArrowVolleyAbility)ability).ConfigureHeldFire(this);
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(IntervalSeconds) || IntervalSeconds <= 0 || !float.IsFinite(DamageScale) || DamageScale < 0 || ProjectileCount < 1)
			yield return "连射间隔必须为正，伤害倍率非负，发数至少一发。";
	}
}
