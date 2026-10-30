using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

public partial class RewardEffectsProbe
{
	private RewardContext MobileActor()
	{
		var unit = new Unit { Faction = Faction.Player, MaxHealth = 1000 }; var weapon = new Weapon(); unit.AddChild(weapon);
		unit.AddChild(new CharacterMovement { Name = "Movement" }); unit.AddChild(new SprintController { Name = "Sprint" }); AddChild(unit);
		unit.SetPhysicsProcess(false); unit.Movement!.SetPhysicsProcess(false);
		return new RewardContext { Player = unit, Weapon = weapon, Build = new RunBuild() };
	}
	private void Dedicated()
	{
		Check(ConfigRows.ById<RewardConfig>("mut_happy_runner")!.Effects.Single() is HappyRunnerRewardEffect
			&& ConfigRows.ById<RewardConfig>("mut_stomp")!.Effects.Single() is JumpPulseRewardEffect
			&& ConfigRows.ById<RewardConfig>("mut_stomp_kill")!.Effects.Single() is KillAirJumpRewardEffect, "奔跑、起跳与逐杀补跳生产行使用独立效果资源");
		var first = MobileActor(); var second = MobileActor(); var enemy = Actor(Faction.Enemy).Player;
		var happy = new HappyRunnerRewardEffect { DamageFreeSeconds = 1, HealPerSecond = 10 };
		var definition = RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_runner", Effects = new RewardEffectDefinition[] { happy } })!;
		first.Build.Add(definition, first); second.Build.Add(definition, second);
		var runner = first.Player.GetChildren().OfType<HappyRunnerRule>().Single(); var otherRunner = second.Player.GetChildren().OfType<HappyRunnerRule>().Single();
		runner.SetPhysicsProcess(false); otherRunner.SetPhysicsProcess(false);
		first.Player.GetNode<SprintController>("Sprint").DebugSprintOverride = true; second.Player.GetNode<SprintController>("Sprint").DebugSprintOverride = true;
		Hit(enemy, first.Player, 100); Hit(enemy, second.Player, 100); happy.HealPerSecond = 999;
		runner._PhysicsProcess(1.5); Near(first.Player.Health, 905, "奔跑效果冻结参数，跨无伤门槛仅恢复超出的半秒"); Near(second.Player.Health, 900, "共享定义不会共享持有者计时");
		first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(first.Build);
		Check(!runner.IsInsideTree() && first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).InstanceCount == 0 && otherRunner.IsInsideTree(), "撤销持续规则当帧退树，仅清理自己的实例");
		runner._PhysicsProcess(2); Near(first.Player.Health, 905, "停止持续规则后即使迟到更新也不再回血");
		var otherHost = second.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName); second.Player.RemoveChild(otherHost); otherRunner._PhysicsProcess(2);
		Check(second.Player.Health == 900 && otherRunner.ProcessMode == ProcessModeEnum.Disabled, "持有者组件退树时先停规则，父单位仍在树内也不二次拆子节点"); otherHost.QueueFree();
		var jumps = new KillAirJumpRewardEffect { ExtraAirJumps = 1, JumpCap = 3 };
		var jumpRow = new RewardConfig { Id = "probe_jumps", Effects = new RewardEffectDefinition[] { jumps } }; first.Build.Add(RewardEffectRegistry.Compile(jumpRow)!, first);
		var killRule = first.Player.GetChildren().OfType<KillAirJumpRule>().Single(); killRule.Stop(); Actor(Faction.Enemy).Player.ForceKill(first.Player);
		Check(first.Player.Movement!.ExtraAirJumps == 0 && killRule.IsStopped, "直接停止逐杀规则也会当帧退订自己的总线，不能绕过基类停止状态");
		first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(first.Build, jumpRow.Id);
		first.Build.Add(RewardEffectRegistry.Compile(jumpRow)!, first); Actor(Faction.Enemy).Player.ForceKill(first.Player);
		Check(first.Player.Movement.ExtraAirJumps == 1, "新实例重新订阅，本人真实击杀立即补充跳数");
		first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(first.Build, jumpRow.Id); Actor(Faction.Enemy).Player.ForceKill(first.Player);
		Check(first.Player.Movement.ExtraAirJumps == 1 && first.Build.Triggers.Count == 0, "撤销后不再补跳，已获得资源按原规则保留");
		var pulseRow = new RewardConfig { Id = "probe_jump_pulse", Effects = new RewardEffectDefinition[] { new JumpPulseRewardEffect() } };
		first.Build.Add(RewardEffectRegistry.Compile(pulseRow)!, first); var pulse = first.Player.GetChildren().OfType<JumpPulseRule>().Single(); pulse.Stop(); first.Player.Movement.BeginJump(1);
		Check(pulse.TriggerTally == 0, "停止起跳效果会当帧退订运动事件");
		first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(first.Build);
		Check(new HappyRunnerRewardEffect { PushCooldown = 0 }.Validate().Any() && new KillAirJumpRewardEffect { JumpCap = 0 }.Validate().Any(), "持续和资源补充效果参数在制作期拒绝错误值");
		var legacy = RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_legacy_jumps", ApplyHandler = "KillAirJumps", HandlerParams = new[] { new ParamEntry { Key = "ExtraAirJumps", Value = 1 }, new ParamEntry { Key = "JumpCap", Value = 3 } } })!;
		first.Build.Add(legacy, first); Actor(Faction.Enemy).Player.ForceKill(first.Player); Check(first.Player.Movement.ExtraAirJumps == 2, "旧逐杀处理器仍通过同一托管实例生效");
		first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(first.Build);
		Behaviors();
	}
	private void Behaviors()
	{
		Check(ConfigRows.ById<RewardConfig>("evo_bloodthirst")!.SpecializationBehavior is BloodthirstBehaviorDefinition
			&& ConfigRows.ById<RewardConfig>("mut_death_duel")!.SpecializationBehavior is DeathDuelBehaviorDefinition, "嗜血与死斗生产行引用各自的强类型定义");
		var first = Actor(); var second = Actor(); var enemy = Actor(Faction.Enemy).Player; Hit(enemy, first.Player, 500); Hit(enemy, second.Player, 500);
		var source = new BloodthirstBehaviorDefinition { Duration = 2, HealFraction = .1f, CritChance = .2f };
		var thirst = source.Create(first.Player); var other = source.Create(second.Player); source.HealFraction = .9f;
		thirst.OnKill(); thirst.Tick(1); Near(first.Player.Health, 550, "独立嗜血实例冻结获得时的恢复比例"); Near(second.Player.Health, 500, "嗜血实例之间没有共享恢复队列");
		Near(first.Player.Stats.Get("CritChanceAdd"), .2f, "嗜血仅增加本实例暴击来源"); thirst.Dispose(); thirst.Dispose(); thirst.Tick(2);
		Near(first.Player.Stats.Get("CritChanceAdd"), 0, "幂等撤销嗜血移除自己的暴击来源"); Near(first.Player.Health, 550, "撤销后不会继续结算剩余恢复"); other.Dispose();
		var duel = new DeathDuelBehaviorDefinition { Duration = 1, Cooldown = 3, RequiredKills = 2 }.Create(first.Player);
		Check(!duel.TryAvoidDeath(second.Player) && duel.TryAvoidDeath(first.Player), "死斗只接自己持有者的致命伤"); duel.Dispose(); Check(!duel.TryAvoidDeath(first.Player), "撤销死斗后不能再次触发死亡避免");
		var runtime = SpecializationRuntime.Ensure(first.Player); runtime.SetPhysicsProcess(false);
		runtime.Apply(ConfigRows.ById<RewardConfig>("evo_bloodthirst")!); Actor(Faction.Enemy).Player.ForceKill(first.Player); Near(first.Player.Stats.Get("CritChanceAdd"), .1f, "真实击杀通过协调器分发到独立嗜血组件");
		first.Player.ForceKill(enemy); Near(first.Player.Stats.Get("CritChanceAdd"), 0, "真实死亡立即撤销新组件的临时修正器");
		Check(!runtime.ActiveStatusVisuals().Any(), "死亡后持续提示同步清除");
		var legacyOwner = Actor(); var legacyRuntime = SpecializationRuntime.Ensure(legacyOwner.Player); legacyRuntime.SetPhysicsProcess(false);
		legacyRuntime.Apply(new RewardConfig { Id = "probe_legacy_thirst", Specialization = SpecializationKind.Bloodthirst, HandlerParams = new[] { new ParamEntry { Key = "Duration", Value = 2 }, new ParamEntry { Key = "HealFraction", Value = .1f }, new ParamEntry { Key = "CritChance", Value = .2f } } });
		Actor(Faction.Enemy).Player.ForceKill(legacyOwner.Player); Near(legacyOwner.Player.Stats.Get("CritChanceAdd"), .2f, "旧特化参数通过兼容层建立相同独立组件");
		legacyOwner.Player.RemoveChild(legacyRuntime); Near(legacyOwner.Player.Stats.Get("CritChanceAdd"), 0, "协调器退树成对撤销组件"); legacyRuntime.QueueFree();
		SpecializationLeases();
	}
	private void SpecializationLeases()
	{
		var context = MobileActor(); var enemy = Actor(Faction.Enemy).Player;
		context.Build.Add(RewardCatalog.Find("mut_thunder_mark")!, context); context.Build.Add(RewardCatalog.Find("evo_thunder_heart")!, context);
		var runtime = SpecializationRuntime.Ensure(context.Player); runtime.SetPhysicsProcess(false);
		Hit(context.Player, enemy, 1); runtime._PhysicsProcess(.01);
		Check(enemy.HasStatus("ThunderMark") && runtime.Has(SpecializationKind.ThunderMark), "生产特化由托管效果创建，真实命中仍能施加引雷");
		context.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(context.Build, "mut_thunder_mark");
		Check(!runtime.Has(SpecializationKind.ThunderMark) && !enemy.HasStatus("ThunderMark") && runtime.Has(SpecializationKind.ThunderHeart), "逐项撤销引雷会清理自有标记，保留另一条闪电链");
		context.Build.Add(RewardCatalog.Find("mut_wind_heart")!, context); context.Player.GetNode<SprintController>("Sprint").DebugSprintOverride = true; runtime._PhysicsProcess(.01);
		var field = runtime.GetChildren().OfType<SpecializationField>().Single();
		context.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(context.Build, "mut_wind_heart");
		Check(field.IsQueuedForDeletion() && field.ProcessMode == ProcessModeEnum.Disabled && !runtime.Has(SpecializationKind.WindHeart), "逐项撤销风心当帧停止并回收自有区域");
		context.Build.Add(RewardCatalog.Find("evo_bloodthirst")!, context); context.Build.Add(RewardCatalog.Find("evo_bloodthirst")!, context);
		Actor(Faction.Enemy).Player.ForceKill(context.Player); Near(context.Player.Stats.Get("CritChanceAdd"), .1f, "同类型多来源共享一个行为，不重复添加暴击");
		context.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(context.Build);
		Check(!runtime.Has(SpecializationKind.Bloodthirst) && !runtime.Has(SpecializationKind.ThunderHeart) && context.Player.Stats.Get("CritChanceAdd") == 0, "来源清理释放全部租约和临时修正，不保留特化状态");
		Check(ConfigIndex.Settings.Upgrades.Where(r => r.Specialization != SpecializationKind.None).All(r => r.Effects.OfType<SpecializationRewardEffect>().Count() == 1 && r.SpecializationBehavior != null), "23项生产特化全部通过统一效果入口安装");
	}
}
