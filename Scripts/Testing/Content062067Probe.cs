using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>跟发生命周期、符文互斥与死战战利品的真实流程回归；视觉项由专业对话验收。</summary>
public partial class Content062067Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content062067";
	private readonly List<(Projectile Shot, ulong Frame)> _shots = new();
	private void Added(Node node) { if (node is Projectile p) _shots.Add((p, Engine.GetPhysicsFrames())); }
	private async Task<bool> Until(Func<bool> predicate, double seconds = 3)
	{
		for (int i = 0; i < seconds * 100; i++) { if (predicate()) return true; await Wait(.01); }
		return predicate();
	}
	private RewardContext Context(RunBuild build) => new() { Player = Player, Weapon = NodeSearch.FindInSubtree<Weapon>(Player)!, Build = build };
	protected override async Task RunChecks()
	{
		GetTree().NodeAdded += Added;
		try { await Followups(); LifeExclusion(); await Shards(); }
		finally { GetTree().NodeAdded -= Added; }
	}
	private async Task Followups()
	{
		var build = new RunBuild(); build.Add(RewardCatalog.Find("mut_growth")!, Context(build));
		var wave = Player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		Check(Player.Stats.Get("ProjectileFollowupDelayFrames") == 3, "062正式增生配置采用三个游戏物理帧间隔");
		// 走真正的技能启动和动画ATK，不直接调用射击函数。
		Check(wave.TryPress(), "062真实能量掌武技可启动");
		Check(await Until(() => _shots.Count >= 2), "062真实ATK后主弹与增生跟发均出手");
		Check(_shots.Count == 2 && _shots[1].Frame - _shots[0].Frame == 3, "062主弹后恰隔三个物理帧跟发，不同帧齐射");
		Check(_shots.All(s => s.Shot.CurrentAttack?.BaseDamage == wave.LastShotDamage), "062跟发沿用已冻结攻击伤害，不重新触发技能");
		RewardEffectHost.Ensure(Player).ReleaseBuild(build);
		await Until(() => !wave.IsActive);
		var attack = new AttackInstance { Attacker = Player, BaseDamage = 17, CanCrit = false };
		Vector3 direction = new Vector3(.3f, .2f, -1).Normalized();
		var queued = new Projectile { Speed = 8, GravityScale = 0 }; int count = _shots.Count;
		ProjectileDispatch.Submit(wave, Player, queued, attack, Player.GlobalPosition + Vector3.Up, direction, 3);
		GetTree().Paused = true; await Wait(.12);
		Check(_shots.Count == count && !queued.IsInsideTree(), "062暂停期间待发弹体不入场，游戏帧倒计时冻结");
		Player.GlobalPosition += Vector3.Right; var source = new object();
		Player.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = 9, Source = source });
		GetTree().Paused = false;
		Check(await Until(() => queued.IsInsideTree()) && queued.CurrentAttack == attack && queued.CurrentAttack.BaseDamage == 17 && queued.LaunchDirection.IsEqualApprox(direction),
			"062等待期间属性或位置变化不改已承诺的伤害与瞄准方向");
		Player.Stats.RemoveBySource(source);
		var canceled = new Projectile(); ProjectileDispatch.Submit(wave, Player, canceled, attack, Player.GlobalPosition, direction, 30);
		new PlayerLoadout().Apply(Player, Mode, PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow);
		Check(!IsInstanceValid(canceled) && Player.GetNode<ProjectileDispatch>("ProjectileDispatch").PendingCount == 0, "062换装同步释放未入树弹体，不向新技能串发");
		Check(!Player.GetNode<ProjectileDispatch>("ProjectileDispatch").IsPhysicsProcessing(), "062空队列停止物理更新");
		var owner = MakeEnemy(Player.GlobalPosition + Vector3.Right * 8); var deadShot = new Projectile();
		ProjectileDispatch.Submit(owner, owner, deadShot, new AttackInstance { Attacker = owner }, owner.GlobalPosition, direction, 30);
		owner.ApplyDirectDamage(100000, Player);
		Check(!IsInstanceValid(deadShot) && owner.GetNode<ProjectileDispatch>("ProjectileDispatch").PendingCount == 0, "062拥有者死亡同步清空跟发队列");
	}
	private void LifeExclusion()
	{
		var weapon = NodeSearch.FindInSubtree<Weapon>(Player)!;
		using var pool = new RewardPool(); pool.Initialize(weapon, "chr_hunter", "survival_twilight");
		var build = new RunBuild(); var life = RewardCatalog.Find("upg_life")!;
		Check(pool.Available(build).Any(r => r.Id == life.Id), "063无符文时生命强化仍可正常获取");
		float before = Player.MaxHealth; build.Add(life, Context(build)); build.Add(life, Context(build));
		Check(Player.MaxHealth == before + 40, "063无雷之印时两次生命强化仍实际增加20+20生命");
		RewardEffectHost.Ensure(Player).ReleaseBuild(build);
		build = new RunBuild(); var rune = ConfigRows.ById<RuneConfig>("rune_lei_seal")!;
		build.SetRuneUnlocks(rune.UnlockList().Append(rune.Id));
		Check(!pool.Available(build).Any(r => r.Id == life.Id) && pool.Available(build).Any(r => r.Id == "mut_thunder_mark"),
			"063雷之印排除生命强化，同时保留其雷系前置解锁");
		foreach (string id in new[] { "upg_life", "upg_life_plus", "upg_life_plus2" })
			Check(ConfigRows.ById<RewardConfig>(id)!.Excludes.Contains(rune.Id), "063生命三档一致声明符文互斥：" + id);
		var run = Mode.GetNode<RunController>("RunController"); run.Build.SetRuneUnlocks(rune.UnlockList().Append(rune.Id));
		before = Player.MaxHealth;
		Check(!run.TryAcquireReward(life.Id) && Player.MaxHealth == before, "063正式GM授予入口也拒绝互斥项，不只是隐藏列表");
		run.Build.SetRuneUnlocks(null); build.SetRuneUnlocks(new[] { "rune_war_seal" });
		Check(pool.Available(build).Any(r => r.Id == life.Id), "063其它符文不会误禁生命强化");
	}
	private async Task Shards()
	{
		int stored = MetaProgress.Shards;
		Vector3 origin = Player.GlobalPosition + Vector3.Forward * 10;
		Unit Victim(string tag = "", bool summoned = false)
		{
			var enemy = MakeEnemy(origin); if (tag.Length > 0) enemy.Tags = enemy.Tags.Append(tag).ToArray();
			if (summoned) enemy.SetMeta("EliteSummoned", true);
			Director._PhysicsProcess(.51); Check(Director.OwnsEnemy(enemy), "067正式导演登记死亡回调");
			enemy.ApplyDirectDamage(1000000, Player); return enemy;
		}
		Check(Director.Definition!.EliteKillShardDrop == 10 && Director.Definition.BossKillShardDrop == 50, "067正式死战配置精英10、首领50碎片");
		Victim(); Check(Director.DroppedCombatShards == 0, "067普通敌人不掉战利品碎片");
		var elite = Victim("Elite"); Check(Director.DroppedCombatShards == 10 && Director.CollectedCombatShards == 0, "067精英死亡只生成10碎片，未拾取不入账");
		elite.ApplyDirectDamage(1000000, Player); Check(Director.DroppedCombatShards == 10, "067重复处理同一死者不会重复掉落");
		Victim("Elite", true); Check(Director.DroppedCombatShards == 10, "067免费召唤的精英不产生碎片");
		var dual = MakeEnemy(origin + Vector3.Right * 4); dual.Tags = dual.Tags.Concat(new[] { "Boss", "Elite" }).ToArray();
		Director._PhysicsProcess(.51); dual.ApplyDirectDamage(1000000, Player);
		Check(Director.DroppedCombatShards == 60 && Director.PendingCombatShards == 60, "067同时标记精英和首领只掉50，不叠加成60");
		var pickup = Director.GetChildren().OfType<RunShardPickup>().First(p => p.Amount == 10);
		Player.GlobalPosition = pickup.GlobalPosition; Player.ResetPhysicsInterpolation();
		Check(await Until(() => Director.CollectedCombatShards == 10) && Director.PendingCombatShards == 50, "067实际靠近拾取一次，账本从待拾取转为本局已收集");
		await Wait(.1); Check(Director.CollectedCombatShards == 10, "067停留原处不会重复拾取");
		int baseline = RunSettlement.ShardsFor(0, Director.CaptureStatistics().ChallengesStarted, false);
		Check(Director.SettlementShards(false) == baseline + 10 && Director.SettlementShards(false) == baseline + 10, "067反复读取结算只包含已拾取碎片一次，未拾取50不计");
		// 真实终局首领须经导航预警生成，验证胜利后仍可捡取战利品。
		Player.GlobalPosition = new Vector3(0, .1f, 38); Player.ResetPhysicsInterpolation();
		Director._PhysicsProcess(1801);
		for (int i = 0; i < 80 && Director.Boss == null; i++) { Director._PhysicsProcess(.1); await Wait(.01); }
		Check(Director.Boss != null, "067真实终局首领经导演与导航生成");
		if (Director.Boss is { } boss)
		{
			boss.ApplyDirectDamage(10000000, Player);
			Check(Director.State == SurvivalDirector.PhaseState.AwaitingExit && Director.DroppedCombatShards == 110, "067终局胜利转撤离时生成50碎片");
			var bossDrop = Director.GetChildren().OfType<RunShardPickup>().Last(); Player.GlobalPosition = bossDrop.GlobalPosition; Player.ResetPhysicsInterpolation();
			Check(await Until(() => Director.CollectedCombatShards >= 60), "067撤离阶段导演停止刷怪后仍能实际拾取首领碎片");
		}
		int collected = Director.CollectedCombatShards;
		Check(MetaProgress.Shards == stored, "067击杀与拾取只增加本局账本，未结算不写局外碎片");
		var run = Mode.GetNode<RunController>("RunController"); int earned = Director.SettlementShards(true);
		Check(run.CompleteMode(Director, true) && MetaProgress.Shards == stored + earned && run.SettlementEarnedShards == earned,
			"067真实结算入口将基础收益与已拾取战利品恰好发放一次");
		Check(!run.CompleteMode(Director, true) && MetaProgress.Shards == stored + earned, "067重复结束请求不会再次发放碎片");
		await Wait(.05);
		Check(!Director.GetChildren().OfType<RunShardPickup>().Any() && Director.PendingCombatShards == 0 && Director.CollectedCombatShards == collected,
			"067停止模式清理未拾取物，已拾取账本保留供单次结算");
	}
}
