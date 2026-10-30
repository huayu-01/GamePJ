using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>最终生命比例加甲、受伤后不回甲，以及正式双模式生成和真实弹道的定向回归。</summary>
public partial class BalanceTuningProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "BalanceTuning";
	private async Task Until(Func<bool> predicate, string label)
	{
		for (int frame = 0; frame < 600; frame++) { if (predicate()) return; await Wait(.02); }
		throw new InvalidOperationException("等待超时：" + label);
	}
	private static float ExpectedArmor(Unit unit) => unit.StartingArmor + Mathf.Max(120, unit.MaxHealth * .3f);
	private Unit Spawn(string id, bool elite)
	{
		var row = ConfigRows.ById<EnemyConfig>(id)!;
		var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit);
		if (elite) unit.AddChild(new EliteModifier());
		unit.ProcessMode = ProcessModeEnum.Disabled; Mode.AddChild(unit);
		ConfigIndex.ScaleEnemy(unit, this, false); return unit;
	}
	protected override async Task RunChecks()
	{
		var light = Spawn("enemy_exploder", true);
		Check(Mathf.IsEqualApprox(light.Armor, 120), "低生命精英额外护甲保持最低120");
		var heavy = Spawn("enemy_heavy_slammer", true);
		Check(heavy.MaxHealth > 400 && Mathf.IsEqualApprox(heavy.Armor, ExpectedArmor(heavy)), $"重型精英按全局倍率后的最终生命{heavy.MaxHealth}授甲{heavy.Armor}");
		object phase = new(); heavy.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = .65f, Source = phase });
		heavy.RefreshStats(); heavy.FillVitals();
		Check(Mathf.IsEqualApprox(heavy.Armor, ExpectedArmor(heavy)), "较低阶段生命重新初始化时比例护甲同步下降，不残留中间倍率的多授护甲");
		heavy.Stats.RemoveBySource(phase); heavy.RefreshStats(); heavy.FillVitals();
		float fullArmor = heavy.Armor; heavy.ApplyDirectDamage(50, Player); float damagedArmor = heavy.Armor;
		heavy.RefreshStats(); heavy.RefreshStats();
		Check(damagedArmor < fullArmor && Mathf.IsEqualApprox(heavy.Armor, damagedArmor), "受伤后重复属性刷新不补甲，不把比例授甲变成永久回甲");
		heavy.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = 1.3f, Source = phase });
		heavy.RefreshStats();
		Check(Mathf.IsEqualApprox(heavy.Armor, damagedArmor), "战斗中只改变最大生命不会自动恢复已损失护甲");
		heavy.FillVitals(); fullArmor = heavy.Armor; heavy.FillVitals();
		Check(Mathf.IsEqualApprox(fullArmor, ExpectedArmor(heavy)) && Mathf.IsEqualApprox(heavy.Armor, fullArmor), "显式回满按成长后的最大生命重算且多次回满不重复累加");
		var normal = Spawn("enemy_chaser", false); var guard = Spawn("enemy_skill_elite", false);
		Check(normal.Armor == normal.StartingArmor && guard.Armor == guard.StartingArmor, "普通敌人和原有技能护卫基础护甲不额外套用比例规则");
		Check(Player.Armor == 0, "玩家护甲获取规则未受精英初始化影响");
		light.QueueFree(); heavy.QueueFree(); normal.QueueFree(); guard.QueueFree(); await Wait(.1);
		await FormalSpawns();
		await Ballistics();
	}
	private async Task FormalSpawns()
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_heavy_slammer")!;
		var trait = Definition("bruiser"); var catalog = new EliteTraitCatalog { Traits = new Godot.Collections.Array<EliteTraitDefinition> { trait } };
		var encounter = new EncounterController { AutoStart = false, TotalBudget = 50, BudgetCap = 50, BudgetRegenPerSecond = 0,
			WaveMode = true, WaveCount = 1, WaveMinEnemies = 1, WaveMaxEnemies = 1, WaveMaxTypes = 1, EliteChanceScale = 100,
			EnemyHealthScale = 1.3f, TraitCatalog = catalog, MinSpawnDistance = 7, SpawnRadius = 16, EliteBossScene = null };
		encounter.AddChild(new EnemySpawnEntry { Scene = RuntimeResources.Load<PackedScene>(row.ScenePath), Cost = row.SpawnResourceCost, Weight = 1, EliteChance = 1 });
		Mode.AddChild(encounter); encounter.Start();
		await Until(() => encounter.CurrentTraitWave?.Members.Count == 1, "主线正式生成比例护甲精英");
		var unit = encounter.CurrentTraitWave!.Members.Single();
		Check(EliteTraitRuntime.Find(unit) != null && Mathf.IsEqualApprox(unit.Armor, ExpectedArmor(unit)), $"主线最终生命含关卡与巨壮{unit.MaxHealth}，护甲{unit.Armor}符合比例");
		unit.ForceKill(Player); encounter.QueueFree(); await Wait(.1);

		Director.Stop(); var settings = Director.Definition!;
		foreach (var phase in settings.Phases) { phase.BudgetPerSecond = 100; phase.MaxAlive = 1; phase.MaxAlivePressure = 100;
			phase.EliteChance = 1; phase.HealthScale = 1.3f; phase.Enemies = new[] { row }; }
		settings.SpawnInterval = .1f; settings.SpawnWarningSeconds = .1f;
		Director.EnableEliteTraits = true; Director.TraitCatalog = catalog;
		Director.Begin(Mode.GetNode<RunController>("RunController"), Player); Director.SetPhysicsProcess(true);
		await Until(() => Director.LivingEnemies.Length == 1, "死战正式生成比例护甲精英"); Director.SetPhysicsProcess(false);
		unit = Director.LivingEnemies.Single();
		Check(EliteTraitRuntime.Find(unit) != null && Mathf.IsEqualApprox(unit.Armor, ExpectedArmor(unit)), $"死战最终生命含阶段与巨壮{unit.MaxHealth}，护甲{unit.Armor}符合比例");
		Director.Stop(); await Wait(.1);
	}
	private async Task Ballistics()
	{
		var owner = MakeEnemy(Player.GlobalPosition + Vector3.Left * 10); owner.Stats.SetBase("DamageMultiplier", 1);
		foreach (var entry in new[] { ("burrow_hunter", 0f), ("arc_thrower", 1.1666667f), ("arc_bomber", 1.2833333f) })
		{
			var plan = RuntimeResources.Load<EnemyActionPlan>($"res://Config/EnemyPlans/{entry.Item1}.tres");
			var step = EnemyActionState.From(plan.Far[0], owner.GlobalPosition);
			var motion = EnemyProjectileMotion.Resolve(step, owner, Player);
			if (entry.Item2 == 0)
				Check(Mathf.IsEqualApprox(motion.Velocity.Length(), 13.2f) && step.TravelDistance == 20 && step.Damage == 10, "追踪弹速度13.2，航程20和伤害10保留");
			else
			{
				float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * motion.GravityScale;
				Vector3 end = motion.Origin + motion.Velocity * step.FlightSeconds - Vector3.Up * gravity * step.FlightSeconds * step.FlightSeconds * .5f;
				Check(Mathf.IsEqualApprox(step.FlightSeconds, entry.Item2) && end.DistanceTo(Player.GlobalPosition + Vector3.Up * .8f) < .002f,
					$"{entry.Item1} 飞行时间缩至70%，重解抛物线仍抵达目标中心");
			}
			var execution = step.Action!.Create(new EnemyActionContext(owner, () => Player, _ => { }, step, "balance_tuning", () => Vector3.Forward, _ => { })); execution.Begin();
			var projectile = GetTree().GetNodesInGroup("projectiles").OfType<Projectile>().Single(p => p.CurrentAttack?.Attacker == owner);
			Check(projectile.LaunchDirection.DistanceTo(motion.Velocity.Normalized()) < .001f && Mathf.IsEqualApprox(projectile.Speed, motion.Velocity.Length()),
				$"{entry.Item1} 真实发射与预警共用当前弹道初值");
			execution.Dispose(); projectile.QueueFree(); await Wait(.05);
		}
		owner.QueueFree();
	}
}
