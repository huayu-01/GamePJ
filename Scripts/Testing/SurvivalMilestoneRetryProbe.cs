using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>强制让阶段精英预警落点失效，确认保留名额并在新位置真实重试。</summary>
public partial class SurvivalMilestoneRetryProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "SurvivalMilestoneRetry";
	protected override async Task RunChecks()
	{
		var definition = Director.Definition!;
		definition.MilestoneFractions = new[] { .001f }; definition.SpawnWarningSeconds = 2;
		foreach (var phase in definition.Phases) { phase.MaxAlive = 1; phase.MaxAlivePressure = 32; phase.BudgetPerSecond = 100; }
		// 第一次编排直接跨过阶段时点，避免先塞入普通补怪占据唯一名额。
		Director._PhysicsProcess(2);
		for (int i = 0; i < 30 && Director.PendingSpawns == 0; i++) { Director._PhysicsProcess(.2); await Wait(.01); }
		Check(Director.PendingSpawns == 1, "阶段强敌采用真实导航与预警队列");
		var warning = Director.GetChildren().OfType<Node3D>().Single(n => n.Name.ToString().StartsWith("SpawnWarning", StringComparison.Ordinal));
		Player.GlobalPosition = warning.GlobalPosition; Player.ResetPhysicsInterpolation(); await Wait(.03);
		int spawned = Director.TotalSpawned;
		Director._PhysicsProcess(2.1);
		Check(Director.TotalSpawned == spawned && Director.PendingSpawns == 1, "玩家进入旧预警落点后撤销该次生成，同帧为未消耗的阶段名额重新选点");
		Director._PhysicsProcess(2.1); await Wait(.03);
		var elite = Director.LivingEnemies.SingleOrDefault();
		Check(elite?.GetChildren().OfType<EliteModifier>().Any() == true && elite.Armor >= 120,
			"重试成功入场的仍是带护甲阶段精英，未被普通补怪替代");
		if (elite != null)
		{
			elite.ApplyDirectDamage(1000000, Player);
			Check(Director.CompletedObjectives == 1 && Director.Objectives.Count(o => o.Kind == SurvivalObjective.ObjectiveKind.Relic) == 1
				&& Director.DroppedCombatShards == 10, "阶段精英真实死亡仍发一份遗物与10碎片");
		}
		Director._PhysicsProcess(.51);
		Check(Director.CompletedObjectives == 1 && Director.DroppedCombatShards == 10, "确认出场后只消耗一次阶段名额，不重复发遗物或碎片");
	}
}
