using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实资源迁移、组合、实例隔离、即时退订、撤销和失败回滚。</summary>
public partial class RewardEffectsProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[RewardEffects] {(ok ? "PASS" : "FAIL")} {message}"); }
	private void Near(float value, float expected, string message) => Check(Mathf.IsEqualApprox(value, expected), $"{message}：{value} / {expected}");
	private RewardContext Actor(Faction faction = Faction.Player)
	{
		var unit = new Unit { Faction = faction, MaxHealth = 1000 }; var weapon = new Weapon(); unit.AddChild(weapon); AddChild(unit);
		return new RewardContext { Player = unit, Weapon = weapon, Build = new RunBuild() };
	}
	private static void Hit(Unit attacker, Unit victim, float damage, string ability = "EffectProbe") => victim.ReceiveHit(new AttackInstance { Attacker = attacker, BaseDamage = damage, Ability = ability }.RollHit(victim, Vector3.Right, victim.GlobalPosition));
	private static StatRewardEffect Stats(float value) => new() { Modifiers = new[] { new StatOverride { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = value } } };
	public override async void _Ready()
	{
		try
		{
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
			var first = Actor(); var second = Actor(); var enemy = Actor(Faction.Enemy).Player;
			var power = RewardCatalog.Find("upg_power")!;
			Check(ConfigRows.ById<RewardConfig>(power.Id)!.Effects.Single() is StatRewardEffect, "生产力量行已迁组合效果资源");
			Check(ConfigRows.ById<RewardConfig>("mut_burn")!.Effects.Single() is StatusOnHitRewardEffect, "生产灼烧弹已迁强类型状态引用");
			first.Build.Add(power, first); second.Build.Add(power, second); second.Build.Add(power, second);
			Near(first.Player.Stats.Get("DamageMultiplier"), 1.15f, "第一持有者只应用一次");
			Near(second.Player.Stats.Get("DamageMultiplier"), 1.3f, "第二持有者独立重复获取");
			var host = first.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName);
			Check(host.InstanceCount == 1 && second.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).InstanceCount == 2, "定义共享但三次应用各有独立实例");
			host.ReleaseReward(first.Build, power.Id); host.ReleaseReward(first.Build, power.Id);
			Near(first.Player.Stats.Get("DamageMultiplier"), 1, "重复撤销幂等"); Near(second.Player.Stats.Get("DamageMultiplier"), 1.3f, "撤销不影响其他持有者");
			var combined = new RewardConfig { Id = "probe_combined", Effects = new RewardEffectDefinition[] { Stats(1.3f), new LifestealRewardEffect { Ratio = .1f }, new StatusOnHitRewardEffect { Status = ConfigRows.ById<EffectConfig>("Burn"), Duration = 3 } } };
			var definition = RewardEffectRegistry.Compile(combined)!; first.Build.Add(definition, first);
			Check(host.InstanceCount == 3 && first.Build.Triggers.Count == 2, "单增幅组合三个效果，触发规则只登记一次");
			Hit(enemy, first.Player, 100); float before = first.Player.Health; Hit(first.Player, enemy, 20);
			Near(first.Player.Health - before, 2, "组合吸血使用真实伤害事实"); Check(enemy.HasStatus("Burn"), "组合状态通过真实命中生效");
			enemy.RemoveStatus("Burn"); host.ReleaseReward(first.Build, definition.Id); before = first.Player.Health; Hit(first.Player, enemy, 20);
			Near(first.Player.Health, before, "释放后同帧不再吸血"); Check(!enemy.HasStatus("Burn") && first.Build.Triggers.Count == 0, "释放后同帧不再加状态，计时登记清除");
			Near(first.Player.Stats.Get("DamageMultiplier"), 1, "组合属性一并撤销");
			var surviving = new RewardConfig { Id = "probe_surviving", Effects = new RewardEffectDefinition[] { Stats(1.1f) } };
			first.Build.Add(RewardEffectRegistry.Compile(surviving)!, first);
			var failing = new RewardConfig { Id = "probe_failing", Effects = new RewardEffectDefinition[] { Stats(2), new FailingEffectDefinition() } };
			bool failed = false; try { first.Build.Add(RewardEffectRegistry.Compile(failing)!, first); } catch (InvalidOperationException) { failed = true; }
			Check(failed && !first.Build.Has(failing.Id), "后一个效果失败时撤回构筑获取记录"); Near(first.Player.Stats.Get("DamageMultiplier"), 1.1f, "失败回滚只撤本次效果，不吞其他增幅");
			var runeBuild = new RunBuild(); var runeContext = new RewardContext { Player = first.Player, Weapon = first.Weapon, Build = runeBuild };
			var rune = new RuneConfig { Id = "probe_rune", Effects = new RewardEffectDefinition[] { Stats(1.4f) } };
			RewardEffectRegistry.Compile(rune)!.Apply(runeContext); host.ReleaseForLoadout();
			Near(first.Player.Stats.Get("DamageMultiplier"), 1.4f, "换装释放普通效果，符文按自身携带周期保留");
			host.ReleaseBuild(runeBuild); Near(first.Player.Stats.Get("DamageMultiplier"), 1, "换符文按独立构筑来源释放");
			var legacy = Actor();
			legacy.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_legacy_stat", ApplyHandler = "StatSet", Modifiers = Stats(1.2f).Modifiers })!, legacy);
			legacy.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_legacy_life", ApplyHandler = "Lifesteal", HandlerParams = new[] { new ParamEntry { Key = "Ratio", Value = .1f } } })!, legacy);
			legacy.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_legacy_status", ApplyHandler = "BurnOnHit" })!, legacy);
			var legacyHost = legacy.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName);
			Check(legacyHost.InstanceCount == 3 && legacy.Build.Triggers.Count == 2, "三类旧处理器均适配为独立托管实例");
			enemy.RemoveStatus("Burn"); Hit(enemy, legacy.Player, 100); before = legacy.Player.Health;
			Near(legacy.Player.Stats.Get("DamageMultiplier"), 1.2f, "旧属性处理器保留倍率");
			// 与正式攻击入口相同：先把属性倍率写入伤害快照，再命中；RollHit 不重复乘单位属性。
			Hit(legacy.Player, enemy, 20 * legacy.Player.Stats.Get("DamageMultiplier"));
			// 千位单精度生命相减约有一个 ULP 的误差，仍以万分之一精度验证恢复量。
			Check(Mathf.Abs(legacy.Player.Health - before - 2.4f) <= .0001f, $"旧吸血与属性效果保持原结算：{legacy.Player.Health - before} / 2.4"); Check(enemy.HasStatus("Burn"), "旧状态字符串适配仍能施加状态");
			enemy.RemoveStatus("Burn"); legacy.Player.Stats.RemoveBySource(legacy.Build); before = legacy.Player.Health; Hit(legacy.Player, enemy, 20);
			Check(legacyHost.InstanceCount == 0 && legacy.Build.Triggers.Count == 0 && !enemy.HasStatus("Burn") && Mathf.IsEqualApprox(before, legacy.Player.Health), "仅调用旧来源清理入口也会当帧释放属性与事件");
			first.Build.Add(definition, first); first.Player.ForceKill(enemy);
			Check(host.InstanceCount == 0 && first.Build.Triggers.Count == 0, "死亡释放全部托管实例和事件规则");
			Near(first.Player.Stats.Get("DamageMultiplier"), 1, "死亡撤销属性");
			second.Player.Stats.RemoveBySource(second.Build); second.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(second.Build);
			Near(second.Player.Stats.Get("DamageMultiplier"), 1, "旧Source成组清理后再次释放实例保持安全");
			Check(new StatusOnHitRewardEffect().Validate().Any() && Stats(float.NaN).Validate().Any(), "缺引用及非有限数值在制作期可校验");
			second.Player.Free(); Check(true, "退树清理不遗留实例或重复释放错误");
			MarkedAndLightning();
			Dedicated();
			RemainingEffects();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[RewardEffects] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void MarkedAndLightning()
	{
		Check(ConfigRows.ById<RewardConfig>("evo_divergence")!.Effects.Single() is MarkedBonusRewardEffect
			&& ConfigRows.ById<RewardConfig>("evo_mindseye")!.Effects.Single() is MarkedBonusRewardEffect, "发散与心眼已使用同一强类型效果组件");
		var actor = Actor(); var victim = Actor(Faction.Enemy).Player; var filter = new AbilityConfig { Id = "EffectProbe" };
		var marked = new RewardConfig { Id = "probe_marked", Effects = new RewardEffectDefinition[] { new MarkedBonusRewardEffect { Status = ConfigRows.ById<EffectConfig>("Mark"), MarkAbility = filter, BonusAbility = filter, PerStackBonus = .1f } } };
		actor.Build.Add(RewardEffectRegistry.Compile(marked)!, actor); float health = victim.Health;
		Hit(actor.Player, victim, 10); Hit(actor.Player, victim, 10);
		Near(health - victim.Health, 21, "标记按本次命中前的层数加伤，首击不加伤"); Check(victim.GetStatusStacks("Mark") == 2, "匹配来源正常叠标");
		health = victim.Health; Hit(actor.Player, victim, 10, "OtherAbility");
		Check(Mathf.IsEqualApprox(health - victim.Health, 10) && victim.GetStatusStacks("Mark") == 2, "不匹配来源既不叠标也不吃限定加伤");
		actor.Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(actor.Build, marked.Id);
		health = victim.Health; Hit(actor.Player, victim, 10);
		Check(Mathf.IsEqualApprox(health - victim.Health, 10) && victim.GetStatusStacks("Mark") == 2 && actor.Build.Triggers.Count == 0, "移除当帧注销命中订阅和加伤接口，已有标记按原期限保留");
		victim.RemoveStatus("Mark");
		actor.Build.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_legacy_mark", ApplyHandler = "MarkedBonus", RequiredEventAbility = filter })!, actor);
		Hit(actor.Player, victim, 10); Check(victim.GetStatusStacks("Mark") == 1, "旧标记参数仍可适配到托管实例"); actor.Player.Stats.RemoveBySource(actor.Build);
		var chain = Actor(); var dead = Actor(Faction.Enemy).Player; var next = Actor(Faction.Enemy).Player;
		chain.Player.GlobalPosition = new Vector3(50, 0, 0); dead.GlobalPosition = chain.Player.GlobalPosition; next.GlobalPosition = new Vector3(52, 0, 0);
		var lightning = new RewardConfig { Id = "probe_chain", Effects = new RewardEffectDefinition[] { new ChainLightningRewardEffect { Damage = 7, Range = 4, Jumps = 1, Cooldown = 0 } } };
		chain.Build.Add(RewardEffectRegistry.Compile(lightning)!, chain); health = next.Health; Hit(chain.Player, dead, 2000);
		Near(health - next.Health, 7, "连锁闪电通过真实击杀事件触发，保持参数结算");
		chain.Player.Stats.RemoveBySource(chain.Build); var another = Actor(Faction.Enemy).Player; another.GlobalPosition = dead.GlobalPosition; health = next.Health; Hit(chain.Player, another, 2000);
		Near(next.Health, health, "撤销闪电后不再响应新的击杀");
	}
}

internal partial class FailingEffectDefinition : RewardEffectDefinition
{
	public override IEnumerable<string> Validate() => Array.Empty<string>();
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new FailingInstance(context, rewardId);
	private sealed class FailingInstance : RewardEffectInstance
	{
		public FailingInstance(RewardContext context, string id) : base(context, id) { }
		protected override void ApplyCore() => throw new InvalidOperationException("预期的事务失败");
		protected override void RemoveCore() { }
	}
}
