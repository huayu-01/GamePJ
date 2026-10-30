using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

public partial class Builds20GeneralProbe : Builds20ProbeSupport
{
	protected override string ProbeTag => "Builds20General";
	protected override async Task RunChecks()
	{
		Prepare(); Player.GlobalPosition += Vector3.Up * 25; Player.ResetPhysicsInterpolation();
		Check(ConfigIndex.Settings.Upgrades.Length == 120 && ConfigIndex.Settings.Effects.Length == 18,
			"069正式索引新增13构筑与3补给状态，旧23特化数量不变");
		Check(ConfigRows.ById<RewardConfig>("evo_bloodthirst")!.LocalizedName == "嗜血" && RewardCatalog.Find("1344")!.Name == "汲血", "069同名冲突按用户决定拆分，旧嗜血身份保留");
		foreach (int n in Enumerable.Range(1343, 13)) Check(RewardCatalog.Find(n.ToString()) is { } row && !row.Description.Contains('{'), "069真实数值展开：" + n);
		Check(!Run.AvailableRewards().Any(r => r.Id is "1353" or "1354"), "069磁化与超级空投未满足前置时不出现");
		Check(Run.TryAcquireReward("1343") && Run.TryAcquireReward("1353"), "069电磁圈及磁化经正式GM入口依次授予");
		var ring = Player.GetChildren().OfType<ElectromagneticRingRule>().Single();
		var giant = new RuneMaxHealthDamageRule { Ratio = .1f }; Player.AddChild(giant);
		var ringPower = new object(); Player.Stats.AddModifier(new StatModifier { Stat = "AttackPowerAdd", Value = 30, Source = ringPower });
		Check(Mathf.Abs(ring.EffectiveRadius - 3.6f) < .001f, "069磁化将3米电圈增加至3.6米");
		var first = MakeEnemy(Player.GlobalPosition + Vector3.Right * 2); var second = MakeEnemy(Player.GlobalPosition + new Vector3(5, 0, .3f));
		float h1 = first.Health, h2 = second.Health, multiplier = AttackOffense.Capture(Player).DamageMultiplier;
		await Wait(.22);
		Check(first.Health < h1 && second.Health < h2 && ring.ChainCount == 1 && ring.HitCount == 2, "069活目标接触起链并连到圈外第二目标，未排除起点");
		float after = first.Health; await Wait(.4); Check(first.Health == after, "069同一目标1秒内不重复受电圈伤害");
		Check(Mathf.Abs(h1 - after - 3 * multiplier) < .1f, "069电链沿原稿基础3伤，仅吃出手倍率，不复制巨人附伤或药剂固定攻击");
		giant.Stop(); giant.QueueFree(); Player.Stats.RemoveBySource(ringPower);
		first.QueueFree(); second.QueueFree(); RewardEffectHost.Ensure(Player).ReleaseReward(Run.Build, "1343"); await Wait(.05);
		Check(!IsInstanceValid(ring), "069撤销电圈同步停止规则并回收视觉");
		Check(Run.TryAcquireReward("1344"), "069汲血正常获取");
		var target = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 6); Player.ApplyDirectDamage(100); float health = Player.Health;
		var attack = new AttackInstance { Attacker = Player, BaseDamage = 50, IsManualWeaponAttack = true, AbilityKind = "skl_a1_w1_1" };
		target.ReceiveHit(attack.RollHit(target, Vector3.Zero, Targeting.AimPoint(target)));
		Check(Mathf.Abs(Player.Health - health - .5f) < .01f, "069汲血按武技1实际伤害恢复1%");
		health = Player.Health; var auto = new AttackInstance { Attacker = Player, BaseDamage = 50, IsSecondary = true, AbilityKind = "skl_a1_w1_1" };
		target.ReceiveHit(auto.RollHit(target, Vector3.Zero, Targeting.AimPoint(target)));
		Check(Player.Health == health, "069自动衍生伤害不冒充武技1吸血");
		var other = new object(); Player.Stats.AddModifier(new StatModifier { Stat = "HealMultiplier", Operation = ModifierOperation.Override, Value = 0, Source = other });
		target.ReceiveHit(attack.RollHit(target, Vector3.Zero, Targeting.AimPoint(target))); Check(Player.Health == health, "069汲血遵守死铠同路禁疗"); Player.Stats.RemoveBySource(other);
		Check(Run.TryAcquireReward("1345"), "069金蝉脱壳正常获取"); var blink = Player.GetChildren().OfType<DamageBlinkRule>().Single();
		var enemy = MakeEnemy(Player.GlobalPosition + Vector3.Right * 10); Vector3 origin = Player.GlobalPosition;
		Player.ApplyDirectDamage(19, enemy); Check(blink.BlinkCount == 0 && blink.AccumulatedDamage == 19, "069未满20点生命损失不瞬移");
		Player.ApplyDirectDamage(1, enemy);
		float distance = new Vector2(Player.GlobalPosition.X - origin.X, Player.GlobalPosition.Z - origin.Z).Length();
		Check(blink.BlinkCount == 1 && blink.AccumulatedDamage == 0 && distance >= 2.95f && distance <= 6.05f, $"069满20后在合法3～6米环内瞬移且清空累计，距离{distance:0.00}、次数{blink.BlinkCount}、累计{blink.AccumulatedDamage}");
		Player.ApplyDirectDamage(50, enemy); Check(blink.BlinkCount == 1 && blink.AccumulatedDamage == 20, "069冷却期最多保留一个门槛，不同帧连跳");
		blink._PhysicsProcess(2.1); Vector3 legalPosition = Player.GlobalPosition;
		Player.GlobalPosition = new Vector3(500000, 50, 500000); Player.ResetPhysicsInterpolation(); Player.ApplyDirectDamage(1, enemy);
		Check(blink.BlinkCount == 1 && blink.AccumulatedDamage == 20, "069无有效导航落点时不伪造瞬移，也不清累计");
		Player.GlobalPosition = legalPosition; Player.ResetPhysicsInterpolation(); Player.ApplyDirectDamage(1, enemy);
		Check(blink.BlinkCount == 2 && blink.AccumulatedDamage == 0, "069回到合法区域后下一次受伤可重试，无需重新累计20点");
		await Supply();
	}
	private async Task Supply()
	{
		Check(Run.TryAcquireReward("1346") && Run.TryAcquireReward("1354"), "069天降空投和超级空投依次正常获取");
		var rule = Player.GetChildren().OfType<SupplyDropRule>().Single(); Check(rule.EffectiveInterval == 7, "069超级空投保持单份，间隔10降到7秒");
		Check(Enumerable.Range(0, 100).Select(_ => rule.RollOption(false)).All(o => o?.Kind != SupplyKind.Experience), "069普通冒险补给池不产生经验药剂");
		int before = MetaProgress.Shards; Check(Run.TryAcquireReward("1355") && Run.TryAcquireReward("1355") && Run.CollectedBonusShards == 40 && MetaProgress.Shards == before,
			"069碎片强化可重复，每次20只记本局账本，不提前写局外");
		int drops = rule.DropCount; rule._PhysicsProcess(6.9); Check(rule.DropCount == drops, "069不到7秒不提前生成空投");
		rule._PhysicsProcess(.11); Check(rule.DropCount == drops + 1 && rule.Pickups.Count == 1, "069到期通过导航有效环生成一份空投");
		var existing = rule.Pickups.Single(); Check(!existing.Landed, "069空投先有预告和落下过程");
		var option = rule.Options.Single(o => o.Kind == SupplyKind.Shards);
		var pickup = new SupplyPickup { OwnerUnit = Player, Option = option, GroundPoint = Player.GlobalPosition + Vector3.Right * 4 };
		Mode.AddChild(pickup); pickup.SetPhysicsProcess(false); pickup._PhysicsProcess(1.1);
		Check(pickup.Landed && pickup.TryCollect() && !pickup.TryCollect() && Run.CollectedBonusShards == 45, "069补给碎片包5仅领取一次");
		var power = rule.Options.Single(o => o.Status?.Id == "1356");
		var shortPower = new SupplyOptionDefinition { Kind = SupplyKind.Status, Status = power.Status, Duration = .2f, DisplayName = "测试力量" };
		var buff = new SupplyPickup { OwnerUnit = Player, Option = shortPower, GroundPoint = Player.GlobalPosition + Vector3.Left * 4 };
		Mode.AddChild(buff); buff.SetPhysicsProcess(false); buff._PhysicsProcess(1.1); Check(buff.TryCollect() && Player.Stats.Get("AttackPowerAdd") == 30, "069力量药剂通过统一状态系统增加30基础攻击");
		var a = new AttackInstance { Attacker = Player, IsManualWeaponAttack = true, ProjectileCount = 3, BaseDamage = 12, CanCrit = false };
		var target = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 8);
		float sum = 0; for (int i = 0; i < 3; i++) sum += a.RollHit(target, Vector3.Zero, target.GlobalPosition).Damage;
		float multiplier = AttackOffense.Capture(Player).DamageMultiplier;
		Check(Mathf.Abs(sum - (36 + 30 * multiplier)) < .01f, "069三弹共享一份30攻击预算，不变成每弹+30");
		await Wait(.1);
		var repeat = new SupplyPickup { OwnerUnit = Player, Option = shortPower, GroundPoint = Player.GlobalPosition + Vector3.Right * 4 };
		Mode.AddChild(repeat); repeat.SetPhysicsProcess(false); repeat._PhysicsProcess(1.1);
		Check(repeat.TryCollect() && Player.Stats.Get("AttackPowerAdd") == 30, "069同类药剂刷新持续时间，不叠成60攻击");
		var unrelated = new object(); Player.Stats.AddModifier(new StatModifier { Stat = "AttackPowerAdd", Value = 7, Source = unrelated });
		await Wait(.12); Check(Player.HasStatus("1356") && Player.Stats.Get("AttackPowerAdd") == 37, "069刷新后越过原到期点仍保留药剂与其它来源");
		await Wait(.16); Check(Player.Stats.Get("AttackPowerAdd") == 7 && !Player.HasStatus("1356"), "069临时力量到期仅撤自身30，不误清其它来源7");
		Player.Stats.RemoveBySource(unrelated);
		var lateTarget = MakeEnemy(Player.GlobalPosition + Vector3.Right * 8);
		Check(a.RollHit(lateTarget, Vector3.Zero, lateTarget.GlobalPosition).Damage > 12, "069已经出手的攻击保留药剂快照，不随状态到期改变");
		var expired = new SupplyPickup { OwnerUnit = Player, Option = option, Lifetime = .05f, GroundPoint = Player.GlobalPosition + Vector3.Right * 4 };
		Mode.AddChild(expired); expired.SetPhysicsProcess(false); expired._PhysicsProcess(1.1);
		Check(expired.IsQueuedForDeletion() && !expired.TryCollect() && !expired.Consumed && Run.CollectedBonusShards == 45, "069过期未领取的补给回收，迟到领取也不产生资源收益");
		RewardEffectHost.Ensure(Player).ReleaseReward(Run.Build, "1346"); await Wait(.05);
		Check(!IsInstanceValid(rule) && !IsInstanceValid(existing), "069撤销空投停止计时并回收尚未领取补给");
		int baseReward = Director.SettlementShards(false), pending = Run.CollectedBonusShards;
		Check(Run.CompleteMode(Director, false) && Run.SettlementEarnedShards == baseReward + pending && MetaProgress.Shards == before + baseReward + pending,
			"069正式结算将本局45碎片合并一次，未领取空投不计入");
		Check(!Run.CompleteMode(Director, false) && MetaProgress.Shards == before + baseReward + pending, "069重复结束请求不重复结算碎片");
	}
}
