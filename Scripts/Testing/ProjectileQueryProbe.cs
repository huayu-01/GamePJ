using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实物理空间验证逐帧弹道，不以静态算法替代命中。</summary>
public partial class ProjectileQueryProbe : Node3D
{
	private int _pass, _fail;
	private Unit _owner = null!;
	private readonly List<string> _hits = new();
	private readonly List<ulong> _frames = new();
	private readonly List<string> _sources = new();
	private readonly Vector3 _origin = new(0f, 10f, 0f);
	private async Task Wait(double seconds = 0.1) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Check(bool ok, string text)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[ProjectileQuery] {(ok ? "PASS" : "FAIL")} {text}");
	}
	private Unit Target(string name, Vector3 position, float radius = 0.2f)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy, MaxHealth = 10000f, KnockbackResistance = 1f };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = radius } });
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.Damaged += (_, hit) => { _hits.Add(name); _frames.Add(Engine.GetPhysicsFrames()); _sources.Add(hit.Attack?.Ability ?? ""); };
		return unit;
	}
	private Projectile Shot(int budget = 0, int pierce = 10, int geometry = 0, int ricochet = 0, bool explosive = false)
	{
		var projectile = new Projectile { Speed = 200f, SweepMaxHits = budget, QueryGeometry = geometry };
		AddChild(projectile);
		projectile.SetPhysicsProcess(false);
		projectile.GlobalPosition = _origin;
		projectile.Launch(new AttackInstance
		{
			Attacker = _owner, Ability = "QueryProbe", BaseDamage = 10f, Lifetime = 2f, Pierce = pierce,
			Ricochet = ricochet, ExplosionRadius = explosive ? 1.2f : 0f, ExplosionDamage = explosive ? 7f : 0f,
		}, Vector3.Forward);
		return projectile;
	}
	private StaticBody3D Wall(Vector3 position, Vector3 size)
	{
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		AddChild(wall); wall.GlobalPosition = position;
		return wall;
	}
	private void Tick(Projectile shot) => shot._PhysicsProcess(1d / Engine.PhysicsTicksPerSecond);
	private async Task Clear(params Node[] nodes)
	{
		foreach (var node in nodes) if (IsInstanceValid(node) && !node.IsQueuedForDeletion()) node.QueueFree();
		await Wait(); _hits.Clear(); _frames.Clear(); _sources.Clear();
	}
	public override async void _Ready()
	{
		try
		{
			_owner = new Unit { Faction = Faction.Player };
			AddChild(_owner);
			_owner.GlobalPosition = _origin + Vector3.Right * 30f;
			var near = Target("Near", _origin + Vector3.Forward);
			var far = Target("Far", _origin + Vector3.Forward * 2.5f);
			await Wait();
			var shot = Shot();
			shot.SetPhysicsProcess(false);
			shot._PhysicsProcess(1d / 60d);
			Check(_hits.Count == 2 && _hits[0] == "Near" && _hits[1] == "Far" && _frames[0] == _frames[1],
				$"射线默认预算同帧穿透双靶：命中 {string.Join(" → ", _hits)}");
			Check(Mathf.Abs(shot.TravelledDistance - 200f / 60f) < 0.001f,
				$"命中帧累计完整实际位移：{shot.TravelledDistance:0.######}，期望 {200f / 60f:0.######}");
			shot.QueueFree(); near.QueueFree(); far.QueueFree();
			await Wait();
			_hits.Clear(); _frames.Clear(); _sources.Clear();
			foreach (int geometry in new[] { 0, 1 })
			{
				await Budgets(geometry);
				await Obstacles(geometry);
				await Flight(geometry);
				await RicochetAndExplosion(geometry);
			}
			await GeometryConfiguration();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[ProjectileQuery] === 弹道查询验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Budgets(int geometry)
	{
		var far = Target("Far", _origin + Vector3.Forward * 2.5f);
		var near = Target("Near", _origin + Vector3.Forward);
		near.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.18f } });
		await Wait();
		var shot = Shot(budget: 1, geometry: geometry);
		Tick(shot);
		Check(string.Join(",", _hits) == "Near" && shot.GlobalPosition.Z > -1.01f, $"几何{geometry}预算1停在近靶，不跳到帧末");
		Check(Mathf.Abs(shot.TravelledDistance - shot.GlobalPosition.DistanceTo(_origin)) < 0.001f, $"几何{geometry}预算停点只累计实际已飞距离");
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Tick(shot);
		Check(string.Join(",", _hits) == "Near,Far" && _frames[1] > _frames[0], $"几何{geometry}下一物理帧续飞命中余程靶");
		Tick(shot);
		Check(_hits.Count == 2, $"几何{geometry}同一身体多个形状及后续帧不重复命中");
		await Clear(shot);
		shot = Shot(pierce: 0, geometry: geometry);
		Tick(shot);
		Check(_hits.Count == 1 && _hits[0] == "Near" && shot.IsQueuedForDeletion(), $"几何{geometry}穿透0命中即结束");
		Check(shot.TravelledDistance > 0f && shot.TravelledDistance < 1.01f, $"几何{geometry}终止命中也累计距离");
		await Clear(shot);
		var third = Target("Third", _origin + Vector3.Forward * 3.1f, 0.1f);
		await Wait();
		shot = Shot(pierce: 1, geometry: geometry);
		Tick(shot);
		Check(_hits.Count == 2 && string.Join(",", _hits) == "Near,Far" && shot.IsQueuedForDeletion(), $"几何{geometry}穿透1只命中两靶，第三靶不受伤");
		await Clear(shot, near, far, third);
		var overlap = Target("Overlap", _origin, 0.5f);
		await Wait();
		shot = Shot(geometry: geometry); Tick(shot); Tick(shot);
		Check(_hits.Count == 1 && _hits[0] == "Overlap", $"几何{geometry}枪口出生在敌人内部命中且仅一次");
		Check(shot.TravelledDistance < 6.667f && shot.TravelledDistance > 6.666f, $"几何{geometry}重叠命中无额外偏移距离");
		await Clear(shot, overlap);
	}
	private async Task Obstacles(int geometry)
	{
		var front = Target("Front", _origin + Vector3.Forward * 0.6f, 0.15f);
		var behind = Target("Behind", _origin + Vector3.Forward * 2.5f);
		var wall = Wall(_origin + Vector3.Forward * 1.6f, new Vector3(3f, 3f, 0.05f));
		await Wait();
		var shot = Shot(geometry: geometry); Tick(shot);
		Check(string.Join(",", _hits) == "Front" && shot.IsQueuedForDeletion(), $"几何{geometry}同帧穿透前靶后被薄墙阻挡");
		Check(shot.TravelledDistance > 1.4f && shot.TravelledDistance < 1.61f, $"几何{geometry}撞墙距离停在近面：{shot.TravelledDistance:0.#####}");
		await Clear(shot, front, wall);
		wall = Wall(_origin, new Vector3(1f, 1f, 1f)); await Wait();
		shot = Shot(geometry: geometry); Tick(shot);
		Check(_hits.Count == 0 && shot.IsQueuedForDeletion() && shot.TravelledDistance < 0.001f, $"几何{geometry}出生在墙内立即结束，墙后靶零命中");
		await Clear(shot, wall);
		wall = Wall(_origin + Vector3.Forward * 4f, new Vector3(3f, 3f, 6f)); await Wait();
		shot = Shot(geometry: geometry); Tick(shot);
		Check(_hits.Count == 0 && shot.IsQueuedForDeletion() && shot.TravelledDistance < 1.01f, $"几何{geometry}长墙近面优先，中心在靶后也不漏挡");
		await Clear(shot, wall, behind);
	}
	private async Task Flight(int geometry)
	{
		var near = Target("GravityNear", _origin + Vector3.Forward);
		await Wait();
		var shot = Shot(geometry: geometry);
		shot.GravityScale = 1f; shot.GravityStartDistance = 3f;
		Tick(shot);
		Check(_hits.Count == 1 && Mathf.Abs(shot.GlobalPosition.Y - _origin.Y) < 0.0001f && shot.TravelledDistance > 3f,
			$"几何{geometry}穿透命中帧累计距离，尚未提前下坠");
		Tick(shot);
		Check(shot.VelocityDirection.Y < 0f && shot.GlobalPosition.Y < _origin.Y && shot.ActualSpeed <= 200.001f,
			$"几何{geometry}达到距离阈值后下坠并保留全局限速");
		await Clear(shot, near);
		var side = Target("Homing", _origin + Vector3.Right * 5f + Vector3.Forward * 8f);
		await Wait();
		shot = Shot(geometry: geometry);
		shot.SeekAnyHostile = true; shot.AssignedHomingTarget = side; shot.HomingRange = 20f; shot.HomingTurnRateDegrees = 360f;
		Tick(shot);
		Check(shot.HomingTargetName == "Homing" && shot.VelocityDirection.X > 0f && shot.VelocityDirection.Z < 0f,
			$"几何{geometry}逐帧追踪转向仍生效");
		Check(shot.ActualSpeed <= 200.001f && shot.TravelledDistance > 3.32f && shot.TravelledDistance <= 3.334f,
			$"几何{geometry}追踪保留飞行位移和速度上限");
		await Clear(shot, side);
	}
	private async Task RicochetAndExplosion(int geometry)
	{
		var near = Target("BounceNear", _origin + Vector3.Forward);
		var side = Target("BounceSide", _origin + Vector3.Right * 2f + Vector3.Forward);
		await Wait();
		var shot = Shot(pierce: 0, geometry: geometry, ricochet: 1); Tick(shot);
		Check(_hits.Count == 1 && !shot.IsQueuedForDeletion() && shot.VelocityDirection.X > 0.98f,
			$"几何{geometry}命中后弹射改向下一目标");
		Check(shot.GlobalPosition.X == 0f && shot.GlobalPosition.Z > -1.01f && shot.TravelledDistance < 1.01f,
			$"几何{geometry}弹射停在真实命中点，不瞬移0.6米");
		Tick(shot);
		Check(string.Join(",", _hits) == "BounceNear,BounceSide" && shot.IsQueuedForDeletion(), $"几何{geometry}弹射下一帧真实命中侧靶，各一次");
		await Clear(shot);
		var wall = Wall(_origin + new Vector3(0.3f, 0f, -1f), new Vector3(0.04f, 2f, 2f)); await Wait();
		shot = Shot(pierce: 0, geometry: geometry, ricochet: 1); Tick(shot); Tick(shot);
		Check(string.Join(",", _hits) == "BounceNear" && shot.IsQueuedForDeletion(), $"几何{geometry}弹射路径近墙拦截，不能瞬移穿墙");
		await Clear(shot, wall, near, side);
		near = Target("BlastNear", _origin + Vector3.Forward);
		side = Target("BlastSide", _origin + Vector3.Forward + Vector3.Right * 0.7f);
		var far = Target("BlastFar", _origin + Vector3.Forward * 2.5f);
		int explosions = 0;
		void Added(Node node) { if (node is Explosion) explosions++; }
		GetTree().NodeAdded += Added;
		try
		{
			await Wait(); shot = Shot(geometry: geometry, explosive: true); Tick(shot);
			Check(explosions == 1 && _hits.Count == 2, $"几何{geometry}同帧穿透多靶只生成一次爆炸");
			await Wait(0.25);
			Check(_hits.Contains("BlastSide") && _sources.Contains("Explosion") && explosions == 1,
				$"几何{geometry}爆炸真实伤害命中侧靶，继续穿透不重复引爆");
		}
		finally { GetTree().NodeAdded -= Added; }
		await Clear(shot, near, side, far);
	}
	private async Task GeometryConfiguration()
	{
		foreach (string name in new[] { "Bullet", "Arrow", "EnergyWave", "SwordQi" })
		{
			var scene = RuntimeResources.Load<PackedScene>($"res://Scenes/Combat/{name}.tscn").Instantiate<Projectile>();
			int expected = name == "Bullet" || name == "Arrow" ? 0 : 1;
			Check(scene.QueryGeometry == expected && scene.FrameHitBudget == 8, $"场景{name}显式几何{expected}，预算缺省8");
			if (name == "EnergyWave") Check(Mathf.IsEqualApprox(scene.SweepRadius, 0.14f), "能量波判定半径0.14世界米");
			scene.Free();
		}
		var target = Target("OffAxis", _origin + Vector3.Forward + Vector3.Right * 0.3f);
		await Wait();
		var shot = Shot(geometry: 0); shot.SweepRadius = 0.5f; shot.Scale = Vector3.One * 4f; Tick(shot);
		Check(_hits.Count == 0, "细弹射线不受预算、视觉放大或SweepRadius影响");
		await Clear(shot);
		shot = Shot(geometry: 1); shot.SweepRadius = 0.05f; shot.Scale = Vector3.One * 4f; Tick(shot);
		Check(_hits.Count == 0, "粗弹纯视觉放大不自动放大判定半径");
		await Clear(shot);
		shot = Shot(geometry: 1); shot.SweepRadius = 0.14f; Tick(shot);
		Check(_hits.Count == 1 && _hits[0] == "OffAxis", "同一偏轴靶由表配半径0.14命中");
		await Clear(shot, target);
	}
}
