using Godot;

namespace ActRoguelike.Config;

/// <summary>点按连射进化的强类型参数；用技能资源寻址，不按字符串反射写属性。</summary>
[Tool]
[GlobalClass]
public partial class VolleyRampTuning : Resource
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public int MaxCount { get; set; } = 3;
	[Export] public float WindowSeconds { get; set; } = 1.2f;
	[Export] public float ShotIntervalSeconds { get; set; } = 0.08f;
}
