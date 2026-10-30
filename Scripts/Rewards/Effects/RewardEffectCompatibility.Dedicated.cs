using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>只读取旧格式；生产奖励与符文全部直接使用Effects，新内容不必登记处理器。</summary>
internal static partial class RewardEffectCompatibility
{
	private static readonly Dictionary<string, Func<RewardConfig, IEnumerable<RewardEffectDefinition>>> Dedicated = new()
	{
		["ChargeBehavior"] = row => One(row.ChargeBehavior), ["ChargedShot"] = row => One(row.ChargedShot),
		["VolleyRamp"] = row => One(row.VolleyRamp), ["WeaponRecall"] = row => One(row.WeaponRecall),
		["WeaponSeek"] = row => One(row.WeaponSeek), ["MeleeProjectile"] = row => One(row.MeleeProjectile), ["SkillEnhancement"] = row => One(row.SkillEnhancement),
		["TeleportJump"] = row => new RewardEffectDefinition[] { new TeleportJumpRewardEffect() },
		["GrantArmor"] = row => new RewardEffectDefinition[] { new GrantArmorRewardEffect { Armor = Number(row, "Armor", 0) } },
		["Specialization"] = row => WithStats(row, new SpecializationRewardEffect { Definition = SpecializationBehaviorCompatibility.Definition(row) }),
		["GuardOnSprint"] = row => new RewardEffectDefinition[] { new GuardOnSprintRewardEffect { Cooldown = Number(row, "Cooldown", 5), BlockDamageMultiplier = Number(row, "BlockDamageMultiplier", 0), FrontDotThreshold = Number(row, "FrontDotThreshold", 1) } },
		["SoulHarvestOnKill"] = row => new RewardEffectDefinition[] { new SoulHarvestRewardEffect { Chance = Number(row, "Chance", .3f), Heal = Number(row, "Heal", 8), PickupRadius = Number(row, "PickupRadius", 1.2f), LifetimeSeconds = Number(row, "LifetimeSeconds", 15), PickupDelaySeconds = Number(row, "PickupDelaySeconds", 0) } },
		["TablePulse"] = row => new RewardEffectDefinition[] { Pulse(row) },
		["RuneEffect"] = Rune,
		["AbilityOverride"] = row => WithStats(row, new LegacyOverride(row)),
		["SummonOnKill"] = row => LegacyNode(context => new SummonOnKillRule { RewardId = row.Id, SummonScene = Scene(row, "SummonScene", "res://Scenes/Summons/Drone.tscn"), MaxSummons = (int)Number(row, "MaxSummons", 2) }),
		["HomingMissileOnCrit"] = row => LegacyNode(context => new HomingMissileOnCritRule { RewardId = row.Id, Cooldown = Number(row, "Cooldown", 0), MissileScene = Scene(row, "ProjectileScene", "res://Scenes/Combat/Bullet.tscn"), Damage = Number(row, "Damage", 50), Speed = Number(row, "Speed", 60), SeekRange = Number(row, "SeekRange", 18), Lifetime = Number(row, "Lifetime", 4), BeaconStatusId = Text(row, "BeaconStatusId", "MissileBeacon"), BeaconSeconds = Number(row, "BeaconSeconds", 1.5f), TurnRateDegrees = Number(row, "TurnRateDegrees", 360) }),
		["SunsetSword"] = row => LegacyNode(context => new SunsetSwordRule { RewardId = row.Id, Cooldown = Number(row, "Cooldown", 0), TriggerInputAction = Text(row, "TriggerInputAction", "weapon_skill_1"), HoverSeconds = Number(row, "HoverSeconds", 1.5f), FallSeconds = Number(row, "FallSeconds", .35f), Radius = Number(row, "Radius", 2.5f), Damage = Number(row, "Damage", 40), HoverHeight = Number(row, "HoverHeight", 3.2f) }),
	};
	private static IEnumerable<RewardEffectDefinition> One(AbilityRewardTuning? tuning) { yield return new AbilityTuningRewardEffect { Tuning = tuning }; }
	private static IEnumerable<RewardEffectDefinition> WithStats(RewardConfig row, RewardEffectDefinition effect)
	{
		if (row.Modifiers.Length > 0) yield return new StatRewardEffect { Modifiers = row.Modifiers }; yield return effect;
	}
	private static bool Flag(RewardConfig row, string key) => row.HandlerParams.FirstOrDefault(p => p.Key == key)?.AsBool(false) ?? false;
	private static PackedScene? Scene(RewardConfig row, string key, string fallback) => RuntimeResources.Load<PackedScene>(Text(row, key, fallback));
	private static EventPulseRewardEffect Pulse(RewardConfig row) => new()
	{
		Events = Text(row, "Trigger", "Kill").Split(',').Aggregate((RewardPulseEvents)0, (all, value) => all | Enum.Parse<RewardPulseEvents>(value.Trim())),
		TargetAbility = ConfigRows.ById<AbilityConfig>(Text(row, "AbilityId", "")), RequiredAbility = row.RequiredEventAbility, RequireFinalComboStep = row.RequireFinalComboStep,
		RequireHover = Flag(row, "RequireHover"), Cooldown = Number(row, "Cooldown", 0), CooldownFraction = Number(row, "CooldownFraction", 0), CooldownSeconds = Number(row, "CooldownSeconds", 0), RefreshAbility = Flag(row, "RefreshAbility"),
		Heal = Number(row, "Heal", 0), HoverSeconds = Number(row, "HoverSeconds", 0), ExtraAirJumps = (int)Number(row, "ExtraAirJumps", 0), JumpCap = (int)Number(row, "JumpCap", 3), Radius = Number(row, "Radius", 0), Damage = Number(row, "Damage", 0)
	};
	private static IEnumerable<RewardEffectDefinition> Rune(RewardConfig row)
	{
		if (row.Modifiers.Length > 0) yield return new StatRewardEffect { Modifiers = row.Modifiers };
		if (Number(row, "Armor", 0) > 0) yield return new GrantArmorRewardEffect { Armor = Number(row, "Armor", 0) };
		if (Number(row, "ArmorPerKills", 0) > 0) yield return new ArmorOnKillRewardEffect { KillsPerTrigger = (int)Number(row, "ArmorPerKills", 0), ArmorPerTrigger = Number(row, "ArmorPerTrigger", 1) };
		if (Number(row, "DeathAvoid", 0) > 0) yield return new DeathAvoidRewardEffect { Charges = (int)Number(row, "DeathAvoid", 0), HealFraction = Number(row, "DeathAvoidHealFraction", .3f) };
		if (Number(row, "FarRange", 0) > 0) yield return new DistanceDamageRewardEffect { NearRange = Number(row, "NearRange", 0), NearScale = Number(row, "NearScale", 1), FarRange = Number(row, "FarRange", 0), FarScale = Number(row, "FarScale", 1) };
		if (Number(row, "MaxHealthDamageRatio", 0) > 0) yield return new MaxHealthDamageRewardEffect { Ratio = Number(row, "MaxHealthDamageRatio", 0) };
		if (Number(row, "PixieIntervalSeconds", 0) > 0) yield return new CompanionRewardEffect { IntervalSeconds = Number(row, "PixieIntervalSeconds", 0), SearchRadius = Number(row, "PixieRadius", 8), Damage = Number(row, "PixieDamage", 20), OrbitRadius = Number(row, "PixieOrbitRadius", 1.6f), OrbitDegreesPerSecond = Number(row, "PixieOrbitSpeed", 90) };
	}
	private static IEnumerable<RewardEffectDefinition> LegacyNode(Func<RewardContext, Node> create) { yield return new LegacyNodeDefinition(create); }
	private sealed partial class LegacyNodeDefinition : RewardEffectDefinition
	{
		private readonly Func<RewardContext, Node> _create;
		public LegacyNodeDefinition(Func<RewardContext, Node> create) => _create = create;
		public override IEnumerable<string> Validate() { yield break; }
		public override RewardEffectInstance Create(RewardContext context, string id) => new NodeRewardEffectInstance(context, id, _create(context));
	}
	/// <summary>旧任意属性覆盖只在此保留，当前有备无患已改成AbilityChargesTuning。</summary>
	private sealed partial class LegacyOverride : RewardEffectDefinition
	{
		private readonly RewardConfig _row;
		public LegacyOverride(RewardConfig row) => _row = row;
		public override IEnumerable<string> Validate() { if (string.IsNullOrEmpty(Text(_row, "AbilityId", ""))) yield return "AbilityId：旧属性覆盖缺少目标。"; }
		public override RewardEffectInstance Create(RewardContext context, string id) => new LegacyOverrideInstance(context, id, _row);
	}
	private sealed class LegacyOverrideInstance : RewardEffectInstance
	{
		private readonly RewardConfig _row; private Action? _restore;
		public LegacyOverrideInstance(RewardContext context, string id, RewardConfig row) : base(context, id) => _row = row;
		protected override void ApplyCore()
		{
			var ability = Ability.FindByAbilityId(Context.Player, Text(_row, "AbilityId", "")) ?? throw new InvalidOperationException($"{RewardId}：旧覆盖目标不存在。");
			var properties = ability.GetPropertyList().Select(p => p["name"].AsString()).ToHashSet(); var previous = new Dictionary<string, Variant>();
			_restore = () => { if (GodotObject.IsInstanceValid(ability)) foreach (var pair in previous) ability.Set(pair.Key, pair.Value); };
			foreach (var entry in _row.HandlerParams.Where(p => p.Key != "AbilityId")) { if (!properties.Contains(entry.Key)) throw new InvalidOperationException($"{RewardId}：未知技能属性 {entry.Key}。"); previous[entry.Key] = ability.Get(entry.Key); ability.Set(entry.Key, entry.Value); }
		}
		protected override void RemoveCore() => _restore?.Invoke();
	}
}
