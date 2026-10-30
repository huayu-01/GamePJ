using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Boss;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实召唤与压力增援：无导航不落地、失败重试、死亡撤销、配置倍率和同批净空。</summary>
public partial class SpawnContinuityProbe : Node3D
{
	private int _pass, _fail;
	private MapManager _map = null!;
	private Unit _caster = null!;
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[SpawnContinuity] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Wait(int frames = 3) { for (int i = 0; i < frames; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	private void Solid(Vector3 position, Vector3 size)
	{
		var body = new StaticBody3D { Position = position, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); AddChild(body);
	}
	private static Unit Template(string name, bool summon = false)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy, Tags = summon ? new[] { "Summon" } : Array.Empty<string>() };
		var collider = new CollisionShape3D { Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .45f, Height = 1.8f } };
		unit.AddChild(collider); collider.Owner = unit;
		return unit;
	}
	private Unit[] Spawned() => GetChildren().OfType<Unit>().Where(u => u != _caster && !u.IsQueuedForDeletion()).ToArray();
	private async Task ClearSpawned()
	{
		foreach (Unit unit in Spawned()) unit.QueueFree();
		await Wait();
	}
	private void VerifyUnits(Unit[] units, float minRadius, float maxRadius)
	{
		foreach (Unit unit in units)
		{
			unit.ProcessMode = ProcessModeEnum.Disabled;
			float distance = new Vector2(unit.GlobalPosition.X - _caster.GlobalPosition.X, unit.GlobalPosition.Z - _caster.GlobalPosition.Z).Length();
			Check(distance >= minRadius - .1f && distance <= maxRadius + .1f, $"真实单位落点在指定环带：{distance:0.##}m");
			Check(_map.TryPath(_caster.GlobalPosition, unit.GlobalPosition, out _, unit), "真实单位与施法者处于同一可达区域");
			Check(_map.HasBodyClearance(unit.GlobalPosition - Vector3.Up * .05f, unit), "真实身体未与世界或其他单位重叠");
		}
		bool separated = true;
		for (int i = 0; i < units.Length; i++)
			for (int j = i + 1; j < units.Length; j++)
				separated &= units[i].GlobalPosition.DistanceTo(units[j].GlobalPosition) >= MapManager.BodySize(units[i]).Radius + MapManager.BodySize(units[j]).Radius + .05f;
		Check(separated, "同一物理帧生成的同批单位保持身体间距");
	}
	public override async void _Ready()
	{
		try { await Run(); }
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[SpawnContinuity] === 增援与召唤验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Run()
	{
		Solid(new Vector3(0, -.5f, 0), new Vector3(48, 1, 48));
		Solid(new Vector3(0, 2, 0), new Vector3(2, 4, 14));
		Solid(new Vector3(40, -.5f, 0), new Vector3(6, 1, 6));
		_map = new MapManager { Name = "MapManager", BakeDelaySeconds = .1f }; AddChild(_map);
		_caster = Template("Caster"); AddChild(_caster); _caster.GlobalPosition = new Vector3(40, .05f, 0);
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn");
		var summon = new SummonAbility { Name = "Summon", SummonScene = scene, Count = 3, Radius = 10, Cooldown = 0 };
		_caster.AddChild(summon);
		Check(summon.TryActivate() && summon.Summoned == 0 && Spawned().Length == 0, "地图未就绪时施法不会伪造落点或增加成功计数");
		for (int i = 0; i < 120 && !_map.IsReady; i++) await Wait(1);
		Check(_map.IsReady, "真实静态几何完成导航烘焙与同步");
		Check(summon.TryActivate() && summon.Summoned == 0 && Spawned().Length == 0, "孤岛外环没有可达地面，不向另一导航岛或虚空召唤");
		_caster.GlobalPosition = new Vector3(-10, .05f, -10); summon.Radius = 6;
		Check(summon.TryActivate() && summon.Summoned == 3, "合法位置恢复后一次真实召唤3只，只累计成功数量");
		Unit[] summoned = Spawned(); VerifyUnits(summoned, 3, 6);
		EnemyConfig row = ConfigRows.ByScenePath<EnemyConfig>(scene.ResourcePath)!;
		float healthScale = ConfigRows.ById<EnemyConfig>("enemy_global")?.HealthMultiplier ?? ConfigIndex.Settings.EnemyHealthMultiplier;
		Check(summoned.Length == 3 && summoned.All(u => Mathf.IsEqualApprox(u.Health, row.MaxHealth * healthScale)), "首领技能召唤的敌人先套单位表，再套一次全局生命倍率");
		await ClearSpawned();
		summon.MaxAlive = 2; summon.Count = 5; int enemyBefore = summon.Summoned;
		Check(summon.TryActivate() && summon.Summoned - enemyBefore == 2 && Spawned().Length == 2, "召唤普通敌人即使没有Summon标签，也不能越过正数存活上限");
		Check(!summon.TryActivate(), "存活上限跨越多次施法持续生效，不只截断单次请求");
		Spawned()[0].ForceKill(); await Wait(); enemyBefore = summon.Summoned;
		Check(summon.TryActivate() && summon.Summoned - enemyBefore == 1, "本技能普通敌人死亡释放名额，补位后仍不越过上限");
		await ClearSpawned(); summon.MaxAlive = 0; summon.Count = 3;

		_caster.GlobalPosition = new Vector3(40, .05f, 0);
		var pressure = new BossPressure { Name = "Pressure", AddScene = scene, Interval = 1, MaxLevel = 1, AddSpawnLevel = 1, AddsPerLevel = 2, AddSpawnRadius = 10 };
		_caster.AddChild(pressure); pressure.SetPhysicsProcess(false);
		pressure._PhysicsProcess(1.1);
		Check(pressure.Level == 1 && pressure.PendingAdds == 2 && Spawned().Length == 0, "压力升级但无合法位置时保留两只增援，不虚增场上单位");
		pressure._PhysicsProcess(.31);
		Check(pressure.PendingAdds == 2 && Spawned().Length == 0, "同一压力等级下选点失败仍可重试，不丢失也不重复入队");
		_caster.GlobalPosition = new Vector3(10, .05f, -10); pressure.AddSpawnRadius = 6;
		pressure._PhysicsProcess(.31);
		Unit[] adds = Spawned();
		Check(pressure.PendingAdds == 0 && adds.Length == 2, "无需再次升级，位置恢复后待生成增援真实入场");
		VerifyUnits(adds, 3, 6);
		Check(adds.All(u => Mathf.IsEqualApprox(u.Health, row.MaxHealth * healthScale)), "压力增援保留单位配置及全局倍率顺序，不重复放大");
		pressure._PhysicsProcess(.31);
		Check(Spawned().Length == 2, "待生成数归零后重试不会重复生成");
		await ClearSpawned(); pressure.QueueFree(); await Wait();

		var template = Template("Minion", true); var minionScene = new PackedScene();
		Check(minionScene.Pack(template) == Error.Ok, "建立带真实身体和Summon标签的上限夹具"); template.Free();
		_caster.GlobalPosition = new Vector3(-10, .05f, 10);
		var existing = Template("ExistingMinion", true); AddChild(existing); existing.GlobalPosition = new Vector3(-14, .05f, 10); await Wait();
		summon.SummonScene = minionScene; summon.MaxAlive = 3; summon.Count = 5; summon.Radius = 6;
		int before = summon.Summoned;
		Check(summon.TryActivate() && summon.Summoned - before == 2 && Spawned().Length == 3, "上限剩2个名额时，一次请求5只也只补2只");
		Check(!summon.TryActivate() && Spawned().Length == 3, "已达存活上限拒绝再次施法");
		existing.ForceKill(); await Wait(); before = summon.Summoned;
		Check(summon.TryActivate() && summon.Summoned - before == 1, "死亡召唤物释放一个名额，只补回一个");
		Check(!SpawnPlanner.TryFindSpawnPoint(_caster.GlobalPosition, 0, 0, out _, reserved: new[] { _caster.GlobalPosition }), "预留点排除同批碰撞未同步的重复落点");
		await ClearSpawned();

		_caster.GlobalPosition = new Vector3(40, .05f, 0);
		var deathPressure = new BossPressure { Name = "DeathPressure", AddScene = scene, Interval = 1, MaxLevel = 1, AddSpawnLevel = 1, AddsPerLevel = 2, AddSpawnRadius = 10 };
		_caster.AddChild(deathPressure); deathPressure.SetPhysicsProcess(false); deathPressure._PhysicsProcess(1.1);
		Check(deathPressure.PendingAdds == 2, "死亡撤销夹具先形成真实待生成增援");
		_caster.ForceKill(); deathPressure._PhysicsProcess(.31);
		Check(deathPressure.PendingAdds == 0 && Spawned().Length == 0, "首领死亡清空待生成增援，不在收尾后补刷");
	}
}
