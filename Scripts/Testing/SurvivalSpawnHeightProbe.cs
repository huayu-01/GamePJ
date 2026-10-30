using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>高处生成测试使用真实导演、导航、入场预警与预算，不绕过生成入口。</summary>
public partial class SurvivalSpawnHeightProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "SurvivalSpawnHeight";
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true;
		var definition = Director.Definition!;
		definition.SpawnWarningSeconds = .35f; definition.SpawnInterval = .06f;
		foreach (var phase in definition.Phases) { phase.BudgetPerSecond = 100; phase.MaxAlive = 40; phase.MaxAlivePressure = 500; }
		Vector3 entry = Player.GlobalPosition;
		Check(MapManager.Instance!.TryNavigationAnchor(entry, out var anchor), "真实导航生成锚点有效");
		Check(!MapManager.Instance.TryPath(entry + Vector3.Up * 20, anchor, out _), "通用严格寻路仍拒绝高空起点，未全局放宽合法性");
		Director.SetPhysicsProcess(true);
		await Until(() => Director.PendingSpawns > 0);
		Check(Director.PendingSpawns > 0, "真实导演已排入场预警");
		Player.GlobalPosition = entry + Vector3.Up * 20; Player.ResetPhysicsInterpolation();
		int before = Director.TotalSpawned;
		await Until(() => Director.TotalSpawned >= before + 3);
		Check(Director.TotalSpawned >= before + 3, "预警后玩家升到高处，待入场敌人仍能生成");
		before = Director.TotalSpawned;
		Player.GlobalPosition = new Vector3(-18, 12, -15); Player.ResetPhysicsInterpolation();
		await Until(() => Director.TotalSpawned >= before + 3);
		Check(Director.TotalSpawned >= before + 3, "持续处于建筑高度仍有新一批敌人入场");
		Check(Director.LivingEnemies.All(u => MapManager.Instance.IsWalkable(u.GlobalPosition, 1.5f)), "所有实际生成敌人处于导航地面");
		Check(Director.AliveCount <= 40 && Director.AlivePressure <= 500, "高处刷怪仍遵守人数与压力预算");
		GetTree().Paused = true; before = Director.TotalSpawned; float time = Director.ElapsedSeconds;
		await Wait(.4);
		Check(Director.TotalSpawned == before && Mathf.IsEqualApprox(time, Director.ElapsedSeconds), "奖励／菜单暂停仍冻结生成和模式计时");
		GetTree().Paused = false;
		Player.IsInvulnerable = false; Player.ForceKill(); before = Director.TotalSpawned;
		await Wait(.5);
		Check(Director.TotalSpawned == before, "玩家死亡后仍停止生成");
	}
	private async Task Until(System.Func<bool> condition)
	{
		for (int i = 0; i < 100 && !condition(); i++) await Wait(.04);
	}
}
