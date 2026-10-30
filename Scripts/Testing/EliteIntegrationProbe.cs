using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Run;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>同时走原遭遇与生存导演的正式生成、预算、清场和单特性入口。</summary>
public partial class EliteIntegrationProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "EliteIntegration";
	private async Task Until(Func<bool> predicate, string message, double seconds = 8)
	{
		for (int i = 0; i < seconds * 50; i++) { if (predicate()) return; await Wait(.02); }
		throw new InvalidOperationException("等待超时：" + message);
	}
	private EliteTraitCatalog Single(string key, int maxCarriers = 1)
	{
		var row = Definition(key); row.MaxCarriers = maxCarriers;
		return new EliteTraitCatalog { Traits = new Godot.Collections.Array<EliteTraitDefinition> { row } };
	}
	private void Freeze(Unit unit)
	{
		foreach (Node child in unit.GetChildren())
			if (child is Ability ability) ability.Enabled = false;
			else if (child.GetType().Name == "EnemyBrain") child.ProcessMode = ProcessModeEnum.Disabled;
		unit.Movement?.SetPhysicsProcess(false); unit.Velocity = Vector3.Zero;
	}
	private EncounterController Encounter(EliteTraitCatalog catalog, int waves = 1)
	{
		var encounter = new EncounterController
		{
			AutoStart = false, TotalBudget = 100, BudgetCap = 100, BudgetRegenPerSecond = 0,
			WaveMode = true, WaveMinEnemies = 4, WaveMaxEnemies = 4, WaveMaxTypes = 1,
			WaveCount = waves, EliteChanceScale = 100, TraitCatalog = catalog,
			MinSpawnDistance = 7, SpawnRadius = 14, EliteBossScene = null
		};
		encounter.AddChild(new EnemySpawnEntry { Scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn"), Cost = 2, Weight = 1, EliteChance = 1, DisplayName = "集成测试普通近战" });
		Mode.AddChild(encounter); encounter.Start(); return encounter;
	}
	protected override async Task RunChecks()
	{
		var catalog = RuntimeResources.Load<EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres");
		Check(catalog.Traits.Count == 33 && !catalog.Validate().Any(), "显式目录33项且参数全部有效");
		Check(catalog.Traits.Select(t => t.Id).Distinct().Count() == 33, "33个稳定身份唯一");
		Check(catalog.Traits.All(t => !new[] { "elite_wormhole", "elite_waller", "elite_missile_dampening" }.Contains(t.Id)), "三个暂缓项目未进入目录");
		Check(catalog.Traits.All(t => ContentText.Get(t.NameKey, "") == t.Title && !string.IsNullOrEmpty(t.DescriptionKey)), "名称和描述声明稳定本地化键");
		var prototype = MakeEnemy(Player.GlobalPosition + Vector3.Left * 4);
		Check(Single("seeker").Pick(new[] { prototype }) == null, "没有可改造射击行为时不抽追踪");
		prototype.QueueFree(); await Wait(.05);

		var encounter = Encounter(Single("slugger", 2), 2);
		await Until(() => encounter.CurrentTraitWave?.Members.Count == 4, "原关卡正式首波四只出场");
		var first = encounter.CurrentTraitWave!; foreach (var unit in first.Members) Freeze(unit);
		Check(first.IsSealed && first.CarriersCommitted == 2 && first.CarriersReserved == 0, "原遭遇同波最多两名同特性承载者，预留全部提交");
		Check(first.Members.Select(EliteTraitRuntime.Find).Where(r => r != null).All(r => r!.Definition.Id == first.Definition!.Id), "原关卡同波仅一种特性");
		Check(Mathf.IsEqualApprox(encounter.RemainingBudget, 88), "原关卡四只基础费用8加两次特性费用4，只扣一次");
		Check(first.Members.All(u => u.IsInGroup("elite_units")), "未携带词缀的精英仍保留基础身份");
		using (var duplicate = first.Reserve(first.Members[0], true, 100)) Check(duplicate == null, "同一精英不能再次安装第二个特性");
		foreach (var unit in first.LiveMembers()) unit.ForceKill(Player);
		await Until(() => encounter.WaveIndex == 2 && encounter.CurrentTraitWave?.Members.Count == 4, "原关卡正式第二波接入");
		var second = encounter.CurrentTraitWave!; foreach (var unit in second.Members) Freeze(unit);
		Check(second != first && second.CarriersCommitted == 2, "新波有独立承载上限，不继承前波消耗");
		Check(second.LiveMembers().All(u => !first.Members.Contains(u)), "新旧波保护组与成员相互隔离");
		var oldWorld = encounter.TraitWorld!;
		oldWorld.Circle(second.Members[0], Player.GlobalPosition, 3, 1, .8f, 4);
		Check(oldWorld.ActiveCount > 0, "原关卡存在真实待触发危险");
		foreach (var unit in second.LiveMembers()) unit.ForceKill(Player);
		await Until(() => encounter.IsCompleted, "原关卡实际清场完成");
		Check(oldWorld.ActiveCount == 0 && second.IsStopped, "原关卡成功清场撤销行为和遗留危险");
		encounter.QueueFree(); await Wait(.1);

		encounter = Encounter(Single("summoner"));
		await Until(() => encounter.CurrentTraitWave?.Members.Count == 4, "召唤遭遇首波出场");
		var summonWave = encounter.CurrentTraitWave!; foreach (var unit in summonWave.Members) Freeze(unit);
		float paid = encounter.RemainingBudget;
		await Until(() => summonWave.SummonedCount == 3, "有限一批实际随从生成");
		Check(Mathf.IsEqualApprox(paid, encounter.RemainingBudget), "召唤三只随从不扣额外生成费用");
		Check(encounter.AliveCount == 7 && summonWave.Members.Count == 4, "免费随从计入清场，不加入原波保护组");
		var summoned = encounter.GetChildren().OfType<Unit>().Where(u => u.HasMeta("EliteSummoned")).ToArray();
		Check(summoned.Length == 3 && summoned.All(u => EliteTraitRuntime.Find(u) == null && !u.IsInGroup("elite_units")), "召唤物不继承精英或特性");
		foreach (var unit in summoned) unit.ForceKill(Player);
		await Wait(1.2);
		Check(summonWave.SummonedCount == 3 && Mathf.IsEqualApprox(paid, encounter.RemainingBudget), "随从死亡不补员、不再次收费");
		encounter.Stop(); Check(summonWave.IsStopped && encounter.TraitWorld!.ActiveCount == 0, "原关卡停止立即撤销召唤与危险");
		encounter.QueueFree(); await Wait(.1);

		var settings = Director.Definition!;
		foreach (var phase in settings.Phases) { phase.BudgetPerSecond = 100; phase.MaxAlive = 10; phase.MaxAlivePressure = 100; phase.EliteChance = 1; phase.Enemies = new[] { ConfigRows.ById<EnemyConfig>("enemy_chaser")! }; }
		settings.SpawnInterval = .08f; settings.SpawnWarningSeconds = .1f;
		Director.TraitCatalog = Single("summoner"); Director.EnableEliteTraits = true; Director.EliteTraitBatchSize = 4; Director.SetPhysicsProcess(true);
		EliteTraitWave? survivalWave = null;
		await Until(() => (survivalWave = Director.GetChildren().OfType<EliteTraitWave>().FirstOrDefault(w => w.IsSealed && w.CarriersCommitted == 1)) != null, "生存模式有限四只批次已封组");
		Director.SetPhysicsProcess(false);
		foreach (var unit in Director.LivingEnemies) Freeze(unit);
		float bank = Director.SpendableBudget; int regular = Director.TotalSpawned;
		await Until(() => survivalWave!.SummonedCount == 3, "生存模式实际免费单批召唤");
		Check(Director.TotalSpawned == regular && Mathf.IsEqualApprox(Director.SpendableBudget, bank), "生存随从不占生成费用、不冒充付费出生");
		Check(Director.AliveCount == regular + 3 && Director.AlivePressure <= 100, "生存随从计入实际存活和压力上限");
		Check(survivalWave!.CarriersCommitted == 1 && survivalWave.Members.Count(u => EliteTraitRuntime.Find(u) != null) == 1, "生存同波强特性只由一名精英承载");
		var hunt = new TimedHuntRule(Director, Player, .15f, 3, 1.2f, 5);
		Director.BuildRuntime!.AddChild(hunt);
		await Wait(.4);
		Check(Director.LivingEnemies.Any(u => u.HasMeta("EliteSummoned")) && hunt.Target == null, "猎令排除免费随从，不发布无法结算的合约");
		hunt.Stop(); hunt.QueueFree();
		int xp = Director.ExperienceField!.StoredExperience, kills = Director.TotalKills;
		foreach (var unit in Director.LivingEnemies.Where(u => u.HasMeta("EliteSummoned"))) unit.ForceKill(Player);
		Check(Director.ExperienceField.StoredExperience == xp && Director.TotalKills == kills, "免费随从不循环产出经验或猎令收益");
		await Wait(1.1); Check(survivalWave.SummonedCount == 3, "生存随从死亡不补员");
		Director.TraitWorld!.Circle(survivalWave.Members[0], Player.GlobalPosition, 3, 1, .8f, 4);
		await Shot("integration-summoner");
		Director.Stop();
		Check(survivalWave.IsStopped && Director.TraitWorld.ActiveCount == 0 && Director.AliveCount == 0 && Director.PendingSpawns == 0, "生存停止撤销危险、行为、待生成与所有敌人");
		Check(!Player.HasActionLock, "退出未残留玩家动作控制锁");
		await Wait(.1);

		var baseCost = ConfigRows.ById<EnemyConfig>("enemy_chaser")!.SpawnResourceCost;
		foreach (var phase in settings.Phases) { phase.MaxAlivePressure = baseCost; phase.MaxAlive = 1; }
		Director.TraitCatalog = Single("slugger");
		Director.Begin(Mode.GetNode<RunController>("RunController"), Player); Director.SetPhysicsProcess(true);
		await Until(() => Director.AliveCount == 1, "压力硬上限仅容纳基础精英时仍能入场");
		Director.SetPhysicsProcess(false);
		var fallback = Director.LivingEnemies.Single(); Freeze(fallback);
		Check(fallback.IsInGroup("elite_units") && EliteTraitRuntime.Find(fallback) == null, "硬上限买不起附加特性时保留基础精英，不无限等待");
		Check(Mathf.IsEqualApprox(Director.AlivePressure, baseCost) && Director.PendingSpawns == 0, "降级生成只占实际基础压力，不透支附加费用");
		Director.Stop();
	}
}
