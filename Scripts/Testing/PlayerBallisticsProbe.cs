using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>玩家弹道真实物理验收：先行距离、身体中心、曲线制导、连续重力和旧斩弹兼容。</summary>
public partial class PlayerBallisticsProbe : Node3D
{
	private int _pass, _fail;
	private Unit _owner = null!;
	private readonly List<Node> _created = new();
	private readonly List<Vector3> _hitPoints = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[PlayerBallistics] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Sync() { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	private T Add<T>(T node) where T : Node { AddChild(node); _created.Add(node); return node; }
	private async Task Clear()
	{
		foreach (Node node in _created) if (IsInstanceValid(node) && !node.IsQueuedForDeletion()) node.QueueFree();
		_created.Clear(); _hitPoints.Clear(); await Sync();
	}
	private Unit Target(string name, Vector3 position, bool mark = true, Faction faction = Faction.Enemy)
	{
		var target = new Unit { Name = name, Faction = faction, MaxHealth = 10000, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Position = Vector3.Up * 1.2f, Shape = new SphereShape3D { Radius = .5f } });
		Add(target); target.GlobalPosition = position;
		if (mark) target.ApplyStatus(new StatusEffect { Id = "Mark", Duration = 60 }, _owner);
		target.Damaged += (_, hit) => _hitPoints.Add(hit.Position);
		return target;
	}
	private Projectile Shot(Vector3 origin, Vector3 direction, float speed = 45, bool homing = true, int pierce = 0)
	{
		var shot = Add(new Projectile
		{
			Speed = speed, GravityScale = 1, HomingStatusId = homing ? "Mark" : "",
			HomingTurnRateDegrees = 360, HomingRange = 45, HomingStartDistance = 2.5f, BezierHoming = true,
		});
		shot.SetPhysicsProcess(false); shot.GlobalPosition = origin;
		shot.Launch(new AttackInstance { Attacker = _owner, Ability = "PlayerBallistics", BaseDamage = 10, Lifetime = 24, Pierce = pierce }, direction);
		return shot;
	}
	private static bool Alive(Projectile shot) => IsInstanceValid(shot) && !shot.IsQueuedForDeletion();
	private static void Tick(Projectile shot, float delta = 1f / 60f) { if (Alive(shot)) shot._PhysicsProcess(delta); }
	private static void Advance(Projectile shot, int frames, float delta = 1f / 60f) { for (int i = 0; i < frames && Alive(shot); i++) Tick(shot, delta); }
	public override async void _Ready()
	{
		try
		{
			_owner = new Unit { Faction = Faction.Player }; AddChild(_owner); _owner.GlobalPosition = Vector3.Right * 1000;
			await Sync();
			CheckRows();
			await Parabolas();
			await Guidance();
			await MovingAndLostTargets();
			await WallAndPierce();
			await SceneIdentity();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		await Clear();
		GD.Print($"[PlayerBallistics] === 玩家弹道验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void CheckRows()
	{
		foreach (var sample in new[] { (Id: "skl_a1_w1_1", Speed: 45f), (Id: "skl_a1_w1_2", Speed: 60f), (Id: "skl_a2_w1_1", Speed: 36f), (Id: "skl_a2_w1_2", Speed: 32f), (Id: "skl_a3_w1_1", Speed: 45f) })
		{
			var row = ConfigRows.ById<AbilityConfig>(sample.Id)!;
			Check(row != null && Mathf.IsEqualApprox(row.Param("Speed", 0f), sample.Speed), $"{sample.Id}正式表初速{sample.Speed}米/秒");
		}
		var bow = new ArrowVolleyAbility(); var bowRow = ConfigRows.ById<AbilityConfig>("skl_a3_w1_1")!; bowRow.ApplyTo(bow);
		Check(bow.GravityScale == 1 && bow.GravityStartDistance == 0 && bow.BezierHoming && bow.HomingStartDistance == 2.5f,
			"弓正式行套到真实技能：连续重力、2.5米先行、贝塞尔追踪");
		Check(bow.HomingStatusId == "Mark" && bow.HomingRange == 45 && bow.SpeedPerChargeStep == 6, "弓仍需标记，射程45米内索敌，每档+6弹速");
		Check(bow.MaxLifetime >= 2 * (bow.Speed + 8 * bow.SpeedPerChargeStep) / 9.8f, "弓生命周期足以完成龙之箭近竖直升降");
		bow.Free();
		var knife = new ArrowVolleyAbility(); ConfigRows.ById<AbilityConfig>("skl_a2_w1_2")!.ApplyTo(knife);
		Check(knife.SpreadAngleDegrees == 60 && knife.GravityScale == 1 && knife.GravityStartDistance == 0, "飞刀仍是固定60度扇面，出手即进入连续抛物线");
		Check(knife.HomingStatusId == "" && knife.HomingStartDistance == 2.5f && knife.BezierHoming, "普通飞刀没有无标记追踪授权；花心授予后才启用曲线");
		Check(knife.ProjectileScenePath.EndsWith("ThrowingKnife.tscn") && knife.KnockbackMinSpeed == 36 && knife.SpeedPerChargeStep == 4,
			"独立飞刀场景及新弹速下的击退门槛接线正确");
		knife.Free();
	}
	private async Task Parabolas()
	{
		float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		Vector3 origin = new(0, 50, 0);
		var coarse = Shot(origin, Vector3.Forward, homing: false); var fine = Shot(origin, Vector3.Forward, homing: false);
		Advance(coarse, 30, 1f / 30); Advance(fine, 120, 1f / 120);
		Vector3 expected = origin + Vector3.Forward * 45 + Vector3.Down * (.5f * gravity);
		Check(coarse.GlobalPosition.DistanceTo(expected) < .002f, "平射一秒真实位移符合初速×时间−半重力×时间平方");
		Check(coarse.GlobalPosition.DistanceTo(fine.GlobalPosition) < .002f, "30与120物理步长落在同一条抛物线上");
		Check(coarse.VelocityDirection.Y < 0 && coarse.ActualSpeed > 45, "重力改变竖直速度而非恒速弧线假下坠");
		await Clear();
		var ground = Add(new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 });
		ground.AddChild(new CollisionShape3D { Position = new Vector3(0, -.5f, 0), Shape = new BoxShape3D { Size = new Vector3(1000, 1, 1000) } });
		await Sync();
		foreach (float speed in new[] { 32f, 45f, 93f })
		{
			var flat = Shot(Vector3.Up, Vector3.Forward, speed, false);
			var up = Shot(Vector3.Up, new Vector3(0, .9999f, -.01414f).Normalized(), speed, false);
			float flatTime = 0, upTime = 0, peak = 0;
			while (Alive(flat) && flatTime < 24) { Tick(flat); flatTime += 1f / 60; }
			while (Alive(up) && upTime < 24) { Tick(up); upTime += 1f / 60; peak = Mathf.Max(peak, up.GlobalPosition.Y); }
			Check(flat.IsQueuedForDeletion() && up.IsQueuedForDeletion(), $"速度{speed}平射与近竖直上射均撞真实地面结束");
			Check(Mathf.Abs(flat.GlobalPosition.Z) > Mathf.Abs(up.GlobalPosition.Z) && upTime > flatTime * 5 && peak > 20,
				$"速度{speed}平射落点{Mathf.Abs(flat.GlobalPosition.Z):0.##}米/{flatTime:0.##}秒；近竖直落点{Mathf.Abs(up.GlobalPosition.Z):0.##}米/{upTime:0.##}秒");
		}
		await Clear();
	}
	private async Task Guidance()
	{
		var unmarked = Target("Unmarked", new Vector3(6, 0, -14), false); await Sync();
		var shot = Shot(Vector3.Up * 1.2f, Vector3.Forward); Advance(shot, 15);
		Check(shot.HomingTargetName == "" && !shot.IsHomingCurveActive && Mathf.Abs(shot.GlobalPosition.X) < .001f && shot.GlobalPosition.Y < 1.2f,
			"弓不能自动追踪未标记敌人，无标记时沿原方向抛物线下坠");
		await Clear();
		var target = Target("MarkedBody", new Vector3(6, 0, -14)); await Sync();
		shot = Shot(Vector3.Up * 1.2f, Vector3.Forward);
		bool initiallyStraight = true;
		while (shot.TravelledDistance < 2.5f)
		{
			Tick(shot); initiallyStraight &= shot.HomingTargetName == "" && Mathf.Abs(shot.GlobalPosition.X) < .001f;
		}
		Check(initiallyStraight && shot.HomingStartedAtDistance < 0, "前2.5米保留初始方向，即使目标已标记也不立即拐弯");
		Tick(shot);
		Check(shot.IsHomingCurveActive && shot.HomingTargetName == target.Name && shot.HomingStartedAtDistance >= 2.5f, "先行结束后才开启三次贝塞尔制导");
		Check(shot.HomingAimPoint.DistanceTo(target.GlobalPosition + Vector3.Up * 1.2f) < .001f, "追踪锚点是脚底原点之上的真实身体中心");
		Check(shot.VelocityDirection.X > 0 && shot.ActualSpeed <= 45.1f, "曲线从出手切线连续转向，初次转弯不瞬移加速");
		Advance(shot, 180);
		Check(target.Health < target.MaxHealth && shot.IsQueuedForDeletion() && _hitPoints.Count == 1, "曲线箭通过真实碰撞命中标记目标一次");
		Check(_hitPoints.Count == 1 && _hitPoints[0].Y > .6f, "实际命中点在身体中部，未向脚底扎地");
		await Clear();
	}
	private async Task MovingAndLostTargets()
	{
		var target = Target("Moving", new Vector3(5, 0, -18)); await Sync();
		var shot = Shot(Vector3.Up * 1.2f, Vector3.Forward); Advance(shot, 8);
		target.GlobalPosition += Vector3.Right * 3; await Sync(); Advance(shot, 180);
		Check(target.Health < target.MaxHealth && _hitPoints.Count == 1, "移动目标更新贝塞尔终点，依旧真实命中身体");
		await Clear();
		target = Target("Released", new Vector3(5, 0, -18)); await Sync();
		shot = Shot(Vector3.Up * 1.2f, Vector3.Forward); Advance(shot, 8);
		Check(shot.IsHomingCurveActive, "移除前曲线已锁定目标");
		target.QueueFree(); await Sync();
		float beforeY = shot.VelocityDirection.Y * shot.ActualSpeed;
		Tick(shot);
		Check(!shot.IsHomingCurveActive && shot.HomingTargetName == "" && shot.VelocityDirection.Y * shot.ActualSpeed < beforeY,
			"目标释放后清理曲线，保留当前速度并恢复重力");
		var replacement = Target("Replacement", new Vector3(-3, 0, -20)); await Sync(); Advance(shot, 180);
		Check(replacement.Health < replacement.MaxHealth, "丢失目标后可追踪新的有效标记，未读已释放对象");
		await Clear();
	}
	private async Task WallAndPierce()
	{
		var target = Target("BehindWall", new Vector3(5, 0, -15));
		var wall = Add(new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 });
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 12, .1f) } });
		wall.GlobalPosition = new Vector3(0, 3, -6); await Sync();
		var shot = Shot(Vector3.Up * 1.2f, Vector3.Forward); Advance(shot, 180);
		Check(shot.IsQueuedForDeletion() && target.Health == target.MaxHealth && _hitPoints.Count == 0, "贝塞尔路径也逐帧查World，墙后标记不能穿墙命中");
		await Clear();
		var near = Target("PierceNear", new Vector3(0, 0, -8));
		var far = Target("PierceFar", new Vector3(0, 0, -12)); await Sync();
		shot = Shot(Vector3.Up * 1.2f, Vector3.Forward, pierce: 1); Advance(shot, 180);
		Check(near.Health == near.MaxHealth - 10 && far.Health == far.MaxHealth - 10 && _hitPoints.Count == 2,
			"追踪穿透排除已命中靶，依次命中两个标记目标且每个一次");
		await Clear();
	}
	private async Task SceneIdentity()
	{
		var arrow = Add(RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Arrow.tscn").Instantiate<Projectile>());
		var knife = Add(RuntimeResources.Load<PackedScene>("res://Scenes/Combat/ThrowingKnife.tscn").Instantiate<Projectile>());
		var wave = Add(RuntimeResources.Load<PackedScene>("res://Scenes/Combat/EnergyWave.tscn").Instantiate<Projectile>());
		Check(arrow.HasNode("FeatherHorizontal") && arrow.HasNode("Tip") && !knife.HasNode("FeatherHorizontal") && knife.HasNode("Blade"),
			"箭有箭杆/箭头/羽毛，飞刀独立金属刀体，没有共用箭羽");
		Check(knife.SourceSceneName == "ThrowingKnife" && knife.EffectiveCutSceneClass == "Arrow", "诊断保留飞刀真实场景名，斩弹分类兼容旧Arrow白名单");
		int cut = Projectile.CutBySceneName("Arrow", Vector3.Zero, 2);
		Check(cut == 2 && knife.IsQueuedForDeletion() && arrow.IsQueuedForDeletion() && !wave.IsQueuedForDeletion(), "旧斩弹入口可斩断独立飞刀与箭，能量波仍保留");
		await Clear();
		knife = Add(RuntimeResources.Load<PackedScene>("res://Scenes/Combat/ThrowingKnife.tscn").Instantiate<Projectile>());
		Check(Projectile.CutBySceneName("Arrow", Vector3.Zero, 2, new[] { "Arrow" }) == 0 && !knife.IsQueuedForDeletion(), "斩弹排除名单仍对飞刀分类生效");
		await Clear();
	}
}
