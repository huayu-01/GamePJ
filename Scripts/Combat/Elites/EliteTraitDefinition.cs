using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>资源负责参数及行为工厂；波次不按名称或特性Id安装效果。</summary>
[Tool, GlobalClass]
public abstract partial class EliteTraitDefinition : Resource, INumericContentIdentity
{
	[Flags] public enum Capability { Any = 0, Movement = 1, Projectile = 2, ImpactPath = 4, Approachable = 8 }
	[Export] public int NumericId { get; set; }
	private string _legacyId = string.Empty;
	[Export] public string Id { get => NumericContentIdentity.Key(_legacyId, NumericId); set => _legacyId = value; }
	public override void _ValidateProperty(Godot.Collections.Dictionary property) => NumericContentIdentity.HideLegacy(property);
	[Export] public string Title { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	[Export] public string NameKey { get; set; } = "";
	[Export] public string DescriptionKey { get; set; } = "";
	[Export] public int ExtraCost { get; set; } = 2;
	[Export] public int MaxCarriers { get; set; } = 2;
	[Export] public float Weight { get; set; } = 1;
	[Export] public float Cooldown { get; set; } = 6;
	[Export] public float Radius { get; set; } = 3;
	[Export] public float Damage { get; set; } = 12;
	[Export] public float Duration { get; set; } = 3;
	[Export] public float Magnitude { get; set; } = 1.2f;
	[Export] public int Count { get; set; } = 3;
	[Export] public float WarningSeconds { get; set; } = .8f;
	[Export] public float PreSpawnScale { get; set; } = 1;
	[Export] public PackedScene? SummonScene { get; set; }
	[Export] public Capability Requirements { get; set; }
	public abstract EliteTraitBehavior Create(Unit owner, EliteTraitWave wave);
	public virtual bool Supports(Unit owner)
	{
		if (owner.Faction != Faction.Enemy || owner.HasTag("Boss") || owner.HasMeta("EliteSummoned")) return false;
		var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(owner, abilities);
		if (Requirements.HasFlag(Capability.Movement) && owner.GetNodeOrNull<CharacterMovement>("Movement") == null) return false;
		var steps = abilities.OfType<EnemyPlanAbility>().Where(a => a.Plan != null).SelectMany(a => a.Plan.Opening.Concat(a.Plan.Near).Concat(a.Plan.Far).Concat(a.Plan.Closing)).ToArray();
		if (Requirements.HasFlag(Capability.Projectile) && !abilities.Any(a => a is ProjectileAttackAbility) && !steps.Any(s => s.Kind is EnemyActionStep.ActionKind.Shot or EnemyActionStep.ActionKind.ArcShot)) return false;
		if (Requirements.HasFlag(Capability.ImpactPath) && !steps.Any(s => s.Kind is EnemyActionStep.ActionKind.Charge or EnemyActionStep.ActionKind.HomingCharge or EnemyActionStep.ActionKind.Dive or EnemyActionStep.ActionKind.Melee or EnemyActionStep.ActionKind.Spin) && !owner.HasTag("Melee")) return false;
		if (Requirements.HasFlag(Capability.Approachable) && (owner.HasTag("Ranged") || steps.Any(s => s.Kind is EnemyActionStep.ActionKind.Burrow or EnemyActionStep.ActionKind.BackHop))) return false;
		return true;
	}
	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title)) yield return "Id与名称不能为空";
		if (ExtraCost < 0 || MaxCarriers < 1 || Count < 1) yield return "费用、承载上限或效果数量无效";
		foreach (float value in new[] { Weight, Cooldown, Radius, Damage, Duration, Magnitude, WarningSeconds, PreSpawnScale }) if (!float.IsFinite(value) || value < 0) yield return "数值必须非负且有限";
		if (PreSpawnScale < 1 || PreSpawnScale > 1.5f || WarningSeconds < .35f) yield return "体型或预警越过安全范围";
	}
}
