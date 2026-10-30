using ActRoguelike.Abilities;

namespace ActRoguelike.Config;

/// <summary>参数资源负责选择目标和应用；通用宿主仅管理来源及恢复。</summary>
public interface IAbilityRewardTuning
{
	AbilityConfig? TargetAbility { get; set; }
	void Apply(Ability ability);
	bool Supports(Ability ability);
}

public interface IAbilityEnhancementConsumer
{
	void ApplyEnhancement(SkillEnhancementTuning tuning);
}
