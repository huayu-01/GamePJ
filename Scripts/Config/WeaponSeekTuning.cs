using Godot;

namespace ActRoguelike.Config;

/// <summary>飞出武器按周期移向距离拥有者最近的敌人。</summary>
[Tool]
[GlobalClass]
public partial class WeaponSeekTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public float IntervalSeconds { get; set; } = 2f;
	[Export] public float TargetHeight { get; set; } = 0.9f;
}
