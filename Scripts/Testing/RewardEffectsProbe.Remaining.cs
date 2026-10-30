using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Testing;

public partial class RewardEffectsProbe
{
	private void RemainingEffects()
	{
		Check(ConfigIndex.Settings.Upgrades.All(row => row.Effects.Length > 0) && ConfigIndex.Settings.Runes.All(row => row.Effects.Length > 0), "全部生产增幅与符文均使用组合效果资源，不依赖处理器分发");
		var actor = MobileActor(); var host = RewardEffectHost.Ensure(actor.Player);
		var target = new AbilityConfig { Id = "probe_tuning", ClassHint = nameof(ArrowVolleyAbility) };
		var ability = new ArrowVolleyAbility { AbilityId = target.Id, ChargeBaseSeconds = 1, ChargeStepSeconds = .4f }; actor.Player.AddChild(ability);
		var firstTuning = new ChargeBehaviorTuning { TargetAbility = target, StepTimeScale = .5f, AlwaysPerfectRelease = true };
		var secondTuning = new ChargeBehaviorTuning { TargetAbility = target, BaseSecondsOverride = .3f, AutoReleaseOnFullCharge = true };
		void Tuning(string id, AbilityRewardTuning resource) => actor.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = id, Effects = new RewardEffectDefinition[] { new AbilityTuningRewardEffect { Tuning = resource } } })!, actor);
		Tuning("probe_tuning_first", firstTuning); Tuning("probe_tuning_second", secondTuning); firstTuning.StepTimeScale = 99;
		Check(ability.AlwaysPerfectRelease && ability.AutoReleaseOnFullCharge && Mathf.IsEqualApprox(ability.ChargeStepSecondsEffective, .2f), "技能增强冻结参数，两个来源各自生效");
		host.ReleaseReward(actor.Build, "probe_tuning_first");
		Check(!ability.AlwaysPerfectRelease && ability.AutoReleaseOnFullCharge && Mathf.IsEqualApprox(ability.ChargeStepSecondsEffective, .4f) && Mathf.IsEqualApprox(ability.ChargeBaseSecondsEffective, .3f), "逆序撤销较早增强，保留较晚来源且不重复缩时");
		host.ReleaseReward(actor.Build, "probe_tuning_second");
		Check(!ability.AutoReleaseOnFullCharge && Mathf.IsEqualApprox(ability.ChargeBaseSecondsEffective, 1), "最后增强撤销后恢复能力原参数");
		var badTarget = new RewardConfig { Id = "probe_missing_target", Effects = new RewardEffectDefinition[] { Stats(2), new AbilityTuningRewardEffect { Tuning = new AbilityChargesTuning { TargetAbility = new AbilityConfig { Id = "absent" } } } } };
		bool failed = false; try { actor.Build.Add(RewardEffectRegistry.Compile(badTarget)!, actor); } catch (InvalidOperationException) { failed = true; }
		Check(failed && !actor.Build.Has(badTarget.Id) && actor.Player.Stats.Get("DamageMultiplier") == 1, "目标缺失明确失败，并撤回本次组合的属性与获取记录");
		actor.Player.GrantArmor(7); float armor = actor.Player.Armor;
		var pending = new RewardConfig { Id = "probe_armor_pending", Effects = new RewardEffectDefinition[] { new GrantArmorRewardEffect { Armor = 20 }, new FailingEffectDefinition() } };
		try { actor.Build.Add(RewardEffectRegistry.Compile(pending)!, actor); } catch (InvalidOperationException) { }
		Near(actor.Player.Armor, armor, "组合尚未全部应用时失败，不发放一次性护甲");
		var committing = new RewardConfig { Id = "probe_armor_commit", Effects = new RewardEffectDefinition[] { new GrantArmorRewardEffect { Armor = 20 }, new FailingCommitDefinition() } };
		try { actor.Build.Add(RewardEffectRegistry.Compile(committing)!, actor); } catch (InvalidOperationException) { }
		Near(actor.Player.Armor, armor, "提交后续步骤失败也回滚本次护甲，不吞原有护甲");
		var converted = new RewardConfig { Id = "probe_conversion_fail", Effects = new RewardEffectDefinition[] { new SpecializationRewardEffect { Definition = new BloodArmorBehaviorDefinition { ArmorHealthRatio = 2 } }, new FailingEffectDefinition() } };
		float health = actor.Player.Health, max = actor.Player.MaxHealth;
		try { actor.Build.Add(RewardEffectRegistry.Compile(converted)!, actor); } catch (InvalidOperationException) { }
		Check(actor.Player.Armor == armor && actor.Player.Health == health && actor.Player.MaxHealth == max, "血铠应用后的组合失败恢复原护甲、生命与转换修正");
		var grant = new RewardConfig { Id = "probe_armor_granted", Effects = new RewardEffectDefinition[] { new GrantArmorRewardEffect { Armor = 3 } } };
		actor.Build.Add(RewardEffectRegistry.Compile(grant)!, actor); host.ReleaseReward(actor.Build, grant.Id);
		Near(actor.Player.Armor, armor + 3, "正常移除一次性奖励不追缴已经发放的护甲");
		var armorRuleRow = new RewardConfig { Id = "probe_armor_rule", Effects = new RewardEffectDefinition[] { new ArmorOnKillRewardEffect { KillsPerTrigger = 1, ArmorPerTrigger = 2 } } };
		actor.Build.Add(RewardEffectRegistry.Compile(armorRuleRow)!, actor); Actor(Faction.Enemy).Player.ForceKill(actor.Player); armor = actor.Player.Armor;
		host.ReleaseReward(actor.Build, armorRuleRow.Id); Actor(Faction.Enemy).Player.ForceKill(actor.Player);
		Near(actor.Player.Armor, armor, "撤销逐杀加甲当帧退订真实击杀事实");
		var protection = new RewardConfig { Id = "probe_protection", Effects = new RewardEffectDefinition[] { new DeathAvoidRewardEffect(), new MaxHealthDamageRewardEffect { Ratio = .1f } } };
		actor.Build.Add(RewardEffectRegistry.Compile(protection)!, actor); var death = actor.Player.GetChildren().OfType<RuneDeathAvoidRule>().Single();
		host.ReleaseReward(actor.Build, protection.Id); Check(!death.TryAvoidDeath(actor.Player), "撤销保命规则当帧移除注册，迟到调用也不会消耗次数或复活");
		actor.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_teleport_1", Effects = new RewardEffectDefinition[] { new TeleportJumpRewardEffect() } })!, actor);
		actor.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_teleport_2", Effects = new RewardEffectDefinition[] { new TeleportJumpRewardEffect() } })!, actor);
		host.ReleaseReward(actor.Build, "probe_teleport_1"); Check(actor.Player.Movement!.TeleportJumpEnabled, "多个瞬移跳来源移除一个仍保持生效"); host.ReleaseReward(actor.Build, "probe_teleport_2"); Check(!actor.Player.Movement.TeleportJumpEnabled, "最后瞬移跳来源撤销恢复原模式");
		Check(new AbilityTuningRewardEffect { Tuning = new WeaponSeekTuning { TargetAbility = target } }.Validate().Any(), "制作期拒绝把飞镰索敌参数安装到箭矢技能");
		Check(new EventPulseRewardEffect { CooldownFraction = .5f }.Validate().Any() && new DeathAvoidRewardEffect { HealFraction = 2 }.Validate().Any(), "制作期拒绝缺失冷却目标与非法恢复比例");
		Check(typeof(AbilityTuningRewardEffect).GetProperty(nameof(AbilityTuningRewardEffect.Tuning))!.PropertyType == typeof(AbilityRewardTuning), "编辑器参数入口限制为增强基类，新增子类无需维护中央类型清单");
		foreach (string id in new[] { "evo_spare_dodge", "evo_thunder_pool", "rune_death_armor" })
		{
			RewardConfig row = ConfigRows.ById<RewardConfig>(id) ?? (RewardConfig)ConfigRows.ById<RuneConfig>(id)!;
			using var copy = (RewardConfig)row.Duplicate(true);
			string path = $"res://Logs/validation/{id}_effect_roundtrip.tres";
			Error saved = ResourceSaver.Save(copy, path);
			var restored = ResourceLoader.Load<RewardConfig>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
			string rootText = FileAccess.GetFileAsString(path).Split("[resource]")[^1];
			bool parameters = restored.Effects.OfType<AbilityTuningRewardEffect>().FirstOrDefault() is not { } skill || skill.Tuning is AbilityChargesTuning { MaxCharges: 3, RechargeSeconds: .5f, TargetAbility.Id: "skl_a1_skill_1" };
			Check(saved == Error.Ok && restored.Effects.Length == row.Effects.Length && restored.Modifiers.Length == row.Modifiers.Length && parameters
				&& !System.Text.RegularExpressions.Regex.IsMatch(rootText, @"(?m)^(ApplyHandler|HandlerParams|Modifiers|SpecializationBehavior|SpecializationTargets|ConvertedRewards|ChargeBehavior|ChargedShot|WeaponRecall|WeaponSeek|SkillEnhancement|MeleeProjectile|VolleyRamp) ="), $"{id}效果资源保存往返保留实际参数，不重复写旧入口");
		}
		host.ReleaseBuild(actor.Build);
	}
}

internal partial class FailingCommitDefinition : RewardEffectDefinition
{
	public override IEnumerable<string> Validate() => Array.Empty<string>();
	public override RewardEffectInstance Create(RewardContext context, string id) => new FailingCommit(context, id);
	private sealed class FailingCommit : RewardEffectInstance
	{
		public FailingCommit(RewardContext context, string id) : base(context, id) { }
		protected override void ApplyCore() { }
		protected override void CommitCore() => throw new InvalidOperationException("预期的提交失败");
		protected override void RemoveCore() { }
	}
}

