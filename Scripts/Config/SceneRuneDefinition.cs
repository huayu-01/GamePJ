using System;
using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Config;

/// <summary>场景只装备一行；实例与计时由运行组件拥有，资源只提供可编辑的效果参数。</summary>
[Tool, GlobalClass]
public partial class SceneRuneDefinition : ConfigRow, IContentValidatable
{
	public enum RuneKind { Missile, Updraft, PowerField, HealingSpring, StarRain }
	public enum MissileEffect { Knockback, Heal, Shield, Damage }
	[Export] public RuneKind Kind { get; set; }
	[Export] public MissileEffect Impact { get; set; }
	[Export] public Texture2D? Icon { get; set; }
	[Export] public Color Tint { get; set; } = new(.35f, .85f, 1);
	[Export] public float Weight { get; set; } = 1;
	[Export] public float InitialDelay { get; set; } = 20;
	[Export] public float Interval { get; set; } = 14;
	[Export] public float Lifetime { get; set; } = 12;
	[Export] public float WarningSeconds { get; set; } = .9f;
	[Export] public int MaxActive { get; set; } = 2;
	[Export] public float MinDistance { get; set; } = 4;
	[Export] public float MaxDistance { get; set; } = 12;
	[Export] public float Radius { get; set; } = 3;
	[Export] public float Speed { get; set; } = 9;
	[Export] public float Damage { get; set; } = 18;
	[Export] public float Impulse { get; set; } = 10;
	[Export] public float Heal { get; set; } = 24;
	[Export] public float ShieldRatio { get; set; } = .15f;
	[Export] public float ShieldSeconds { get; set; } = 6;
	[Export] public float LaunchHeight { get; set; } = 3;
	[Export] public float DamageMultiplier { get; set; } = 1.25f;
	[Export] public float HealPerSecond { get; set; } = 6;
	[Export] public float FallHeight { get; set; } = 9;
	public IEnumerable<string> Validate()
	{
		if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Impact)) yield return "Kind / Impact：需要有效的场景效果类别。";
		foreach (float value in new[] { Weight, InitialDelay, MinDistance, Damage, Heal, Impulse, ShieldRatio })
			if (!float.IsFinite(value) || value < 0) yield return "权重、等待、距离及效果量需要非负有限值。";
		foreach (float value in new[] { Interval, Lifetime, WarningSeconds, MaxDistance, Radius, Speed, ShieldSeconds, LaunchHeight, DamageMultiplier, HealPerSecond, FallHeight })
			if (!float.IsFinite(value) || value <= 0) yield return "周期、范围、持续时间和速度需要正有限值。";
		if (MaxDistance < MinDistance || WarningSeconds < .35f || MaxActive < 1 || MaxActive > 8 || DamageMultiplier < 1) yield return "距离、预警、实例数量或增伤倍率超出允许范围。";
	}
}
