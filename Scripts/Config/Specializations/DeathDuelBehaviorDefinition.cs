using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class DeathDuelBehaviorDefinition : SpecializationBehaviorDefinition
{
	[Export] public float Duration { get; set; } = 5;
	[Export] public float Cooldown { get; set; } = 20;
	[Export] public int RequiredKills { get; set; } = 3;
	[Export] public float HealFraction { get; set; } = .3f;
	public override SpecializationKind Kind => SpecializationKind.DeathDuel;
	protected override SpecializationBehavior CreateCore(SpecializationContext context) => new DeathDuelBehavior(context.Owner, Duration, Cooldown, RequiredKills, HealFraction);
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(Duration) || Duration <= 0 || !float.IsFinite(Cooldown) || Cooldown < 0) yield return "Duration / Cooldown：持续时间必须大于零，冷却必须非负且有限。";
		if (RequiredKills <= 0 || !float.IsFinite(HealFraction) || HealFraction <= 0 || HealFraction > 1) yield return "RequiredKills / HealFraction：击杀数必须大于零，恢复比例必须处于(0, 1]。";
	}
}
