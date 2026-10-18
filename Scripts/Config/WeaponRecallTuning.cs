using Godot;

namespace ActRoguelike.Config;

/// <summary>投掷技能的召回策略与全部蓄力时间缩放。</summary>
[Tool]
[GlobalClass]
public partial class WeaponRecallTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public float ChargeTimeScale { get; set; } = 1f;
	[Export] public bool RecallOnSecondPress { get; set; }
	[Export] public bool AutoRecallOnSettle { get; set; } = true;
}
