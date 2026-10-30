using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>共享命中参数；动作段仅保留时序、分支与格挡组合。</summary>
[Tool, GlobalClass]
public abstract partial class EnemyHitboxAction : EnemyActionDefinition
{
	[Export] public float Damage { get; set; } = 12;
	[Export] public float Radius { get; set; } = 1.5f;
	[Export] public float Speed { get; set; } = 8;
	[Export] public bool Centered { get; set; }
	public override bool RequiresAttackEvent => true;
	public override string[] OwnedStepProperties => new[] { "Kind", "Damage", "Radius", "Speed", "Centered" };
	public override EnemyActionState Resolve(EnemyActionState state) => base.Resolve(state) with { Damage = Damage, Radius = Radius, Speed = Speed, Centered = Centered };
	public override IEnumerable<string> Validate()
	{
		if (!float.IsFinite(Damage) || Damage < 0) yield return "Damage：伤害必须非负且有限。";
		if (!float.IsFinite(Radius) || Radius <= 0) yield return "Radius：半径必须大于零且有限。";
		if (!float.IsFinite(Speed) || Speed < 0) yield return "Speed：速度必须非负且有限。";
	}
}
