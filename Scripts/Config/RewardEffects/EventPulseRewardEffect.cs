using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Flags]
public enum RewardPulseEvents { Kill = 1, Parried = 2, Critical = 4, ArmorBroken = 8, Damaged = 16 }

/// <summary>战斗事实触发的冷却、治疗、滞空与范围伤害组合；技能使用资源引用。</summary>
[Tool, GlobalClass]
public partial class EventPulseRewardEffect : RewardEffectDefinition
{
	[Export] public RewardPulseEvents Events { get; set; } = RewardPulseEvents.Kill;
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public AbilityConfig? RequiredAbility { get; set; }
	[Export] public bool RequireFinalComboStep { get; set; }
	[Export] public bool RequireHover { get; set; }
	[Export] public float Cooldown { get; set; }
	[Export] public float CooldownFraction { get; set; }
	[Export] public float CooldownSeconds { get; set; }
	[Export] public bool RefreshAbility { get; set; }
	[Export] public float Heal { get; set; }
	[Export] public float HoverSeconds { get; set; }
	[Export] public int ExtraAirJumps { get; set; }
	[Export] public int JumpCap { get; set; } = 3;
	[Export] public float Radius { get; set; }
	[Export] public float Damage { get; set; }
	public override IEnumerable<string> Validate()
	{
		foreach (string error in RewardEffectValidation.NonNegative(this)) yield return error;
		if (Events == 0 || ((int)Events & ~31) != 0) yield return "Events：必须选择支持的战斗事实。";
		if (CooldownFraction > 1) yield return "CooldownFraction：不得超过1。";
		if ((CooldownFraction > 0 || CooldownSeconds > 0 || RefreshAbility) && TargetAbility == null) yield return "TargetAbility：修改冷却必须指定目标技能。";
		if ((Radius > 0) != (Damage > 0)) yield return "Radius / Damage：范围伤害需要同时填写半径和伤害。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new TriggerEffectInstance(context, rewardId,
		new TablePulseRule { EventKinds = Enum.GetValues<RewardPulseEvents>().Where(e => Events.HasFlag(e)).Select(e => Enum.Parse<GameplayEventKind>(e.ToString())).ToArray(),
			Trigger = Events.ToString().Replace(", ", ","), AbilityId = TargetAbility?.Id ?? "", RequiredAbilityId = RequiredAbility?.Id ?? "", RequireFinalComboStep = RequireFinalComboStep,
			RequireHover = RequireHover, Cooldown = Cooldown, CooldownFraction = CooldownFraction, CooldownSeconds = CooldownSeconds, RefreshAbility = RefreshAbility,
			Heal = Heal, HoverSeconds = HoverSeconds, ExtraAirJumps = ExtraAirJumps, JumpCap = JumpCap, Radius = Radius, Damage = Damage });
}

