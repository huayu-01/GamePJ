using System;
using System.Threading.Tasks;
using ActRoguelike.AI;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

public partial class NavigationFoundationProbe : Node3D
{
	private int _pass, _fail;
	private MapManager _map = null!;
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[Navigation] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Wait(int frames) { for (int i = 0; i < frames; i++) await Frame(); }
	private void Solid(Vector3 position, Vector3 size)
	{
		var node = new StaticBody3D { Position = position, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		node.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); AddChild(node);
	}
	private Unit Actor(Vector3 position, Faction faction = Faction.Enemy)
	{
		var unit = new Unit { Faction = faction };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f } });
		unit.AddChild(new CharacterMovement { Name = "Movement", FaceMovementDirection = false });
		unit.AddChild(new UnitNavigation { Name = "Navigation" }); AddChild(unit); unit.GlobalPosition = position;
		return unit;
	}
	public override async void _Ready()
	{
		try { await Run(); }
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Navigation] === 导航基础验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Run()
	{
		Solid(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
		Solid(new Vector3(0, 2, 0), new Vector3(1, 4, 12));
		Solid(new Vector3(35, -0.5f, 0), new Vector3(6, 1, 6));
		_map = new MapManager { Name = "MapManager", BakeDelaySeconds = 0.1f }; AddChild(_map);
		Check(!_map.TryRingPoint(Vector3.Zero, 2, 4, out _), "地图未同步时严格选点失败，不伪造玩家脚下位置");
		for (int i = 0; i < 120 && !_map.IsReady; i++) await Frame();
		Check(_map.IsReady && _map.PolygonCount > 0, $"真实静态几何烘焙并同步，多边形={_map.PolygonCount}");
		Check(_map.TryPath(new Vector3(-6, 0.05f, 0), new Vector3(6, 0.05f, 0), out var path), "墙体两侧有完整导航路径");
		float length = 0, maxZ = 0;
		for (int i = 0; i < path.Length; i++) { maxZ = Mathf.Max(maxZ, Mathf.Abs(path[i].Z)); if (i > 0) length += path[i].DistanceTo(path[i - 1]); }
		Check(length > 15 && maxZ > 5, $"路径真实绕过长墙，长度={length:0.##}／绕行z={maxZ:0.##}");
		Check(!_map.TryPath(new Vector3(6, 0.05f, 0), new Vector3(35, 0.05f, 0), out _), "隔离导航岛不能因最近点存在而判可达");
		Check(!_map.TryValidPoint(Vector3.Zero, new Vector3(-6, 0.05f, 0), out _, snapTolerance: 0.1f), "墙内候选点投影超差时拒绝");
		Check(!_map.TryRingPoint(new Vector3(35, 0.05f, 0), 10, 12, out _), "范围内没有连通可站点时返回失败");
		var occupant = Actor(new Vector3(-5, 0.05f, 5)); await Wait(3);
		Check(!_map.HasBodyClearance(new Vector3(-5, 0, 5)), "有效位置排除被单位占用的身体空间");
		occupant.QueueFree(); await Wait(2);
		Check(_map.HasBodyClearance(new Vector3(-5, 0, 5)), "占用单位离开后可重新选择该点");
		bool radii = true;
		for (int i = 0; i < 12; i++)
		{
			if (!_map.TryRingPoint(new Vector3(17, 0.05f, 12), 3, 6, out var point)) { radii = false; break; }
			float d = new Vector2(point.X - 17, point.Z - 12).Length(); radii &= d >= 2.95f && d <= 6.05f;
		}
		Check(radii, "导航投影后的候选仍满足要求距离，边界不偷偷放宽");
		var walker = Actor(new Vector3(-6, 0.05f, 0)); var navigation = walker.GetNode<UnitNavigation>("Navigation");
		for (int i = 0; i < 420 && walker.GlobalPosition.DistanceTo(new Vector3(6, 0.05f, 0)) > 0.7f; i++)
		{
			navigation.MoveTo(new Vector3(6, 0.05f, 0), 6); await Frame();
		}
		Check(walker.GlobalPosition.X > 5 && Mathf.Abs(walker.GlobalPosition.Z) < 1, $"真实MoveAndSlide沿路径绕墙抵达：{walker.GlobalPosition}");
		walker.Scale = Vector3.One * 1.6f; await Wait(3);
		_map.TryPath(walker.GlobalPosition, new Vector3(10, 0.05f, 10), out _, walker); await Wait(5);
		Check(_map.TryPath(walker.GlobalPosition, new Vector3(10, 0.05f, 10), out _, walker), "大型单位使用独立净空导航档案");
		walker.QueueFree(); await Wait(2);
		Pressure(); await Budget();
		int revision = _map.NavigationRevision;
		Solid(new Vector3(26, -0.5f, 0), new Vector3(18, 1, 5)); await Wait(2); _map.Rebuild();
		Check(!_map.IsReady && _map.NavigationRevision > revision, "重烘失效旧路径，等待导航同步后才标记可用");
		for (int i = 0; i < 60 && !_map.IsReady; i++) await Frame();
		Check(_map.TryPath(new Vector3(6, 0.05f, 0), new Vector3(35, 0.05f, 0), out _), "添加桥梁并重烘后原隔离岛真实连通");
	}
	private void Pressure()
	{
		Check(EnemyBehaviorPressure.SpawnCost(new[] { "BasicMelee", "FrontalGuard" }, 99) == 3, "生成成本来自行为压力之和，不读随机抽取权重");
		Check(EnemyBehaviorPressure.SpawnCost(new[] { "BasicMelee", "BasicMelee" }, 99) == 2, "同一行为标记去重，避免重复收取资源");
		Check(EnemyBehaviorPressure.SpawnCost(new[] { "MissingBehavior" }, 1) == int.MaxValue, "未知行为不允许按便宜回退价生成");
		Check(EnemyBehaviorPressure.SpawnCost(new[] { "BasicMelee" }, 1, 1.5f) == 4, "预留精英附加压力按同一公式计费，目前生产附加值为0");
		foreach (var row in ConfigIndex.Settings.Enemies)
			if (!row.IsGlobal && row.Weight > 0) Check(row.SpawnResourceCost == row.Cost, $"现有敌种行为压力初始预设保留原成本：{row.Id}");
	}
	private async Task Budget()
	{
		var player = Actor(new Vector3(35, 0.05f, 0), Faction.Player); player.IsInvulnerable = true;
		var encounter = new EncounterController { AutoStart = false, WaveMode = false, TotalBudget = 10, BudgetCap = 10, SpawnInterval = 0.1f, MinSpawnDistance = 10, SpawnRadius = 12 };
		encounter.AddChild(new EnemySpawnEntry { Scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn"), Cost = 2, EliteChance = 0 });
		AddChild(encounter); encounter.ApplyEntryFilter(new StageDefinition { AllowedEnemyIds = new[] { "enemy_chaser" } }); encounter.Start(); await Wait(45);
		Check(encounter.TotalSpawned == 0 && encounter.RemainingBudget == 10, "选点失败没有生成单位、扣资源或推进生成计数");
		player.GlobalPosition = new Vector3(-6, 0.05f, -10); encounter.MinSpawnDistance = 3; encounter.SpawnRadius = 5; await Wait(55);
		Check(encounter.TotalSpawned > 0 && Mathf.Abs(encounter.RemainingBudget - (10 - encounter.TotalSpawned * 2)) < 0.001f, "有效点恢复后真实生成，预算严格按行为成本扣除");
		encounter.QueueFree(); player.QueueFree(); await Wait(3);
	}
}
