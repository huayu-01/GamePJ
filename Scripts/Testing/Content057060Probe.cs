using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实投影、死亡掉落、动画事件位移及招架反馈的回归；不写玩家存档。</summary>
public partial class Content057060Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content057060";
	private async Task<bool> Until(Func<bool> test, double seconds = 3)
	{
		for (int i = 0; i < seconds * 50; i++) { if (test()) return true; await Wait(.02); }
		return test();
	}
	private Camera3D View(Vector3 point)
	{
		var camera = new Camera3D { Current = true }; Mode.AddChild(camera);
		camera.GlobalPosition = point + new Vector3(4, 3, 9); camera.LookAt(point + Vector3.Up);
		return camera;
	}
	protected override async Task RunChecks()
	{
		await Numbers(); await Drops(); await Backdash(); await Parry();
	}
	private async Task Numbers()
	{
		var target = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 7);
		var camera = View(target.GlobalPosition); var layer = new Control(); Mode.GetNode<PlayerHud>("Hud").AddChild(layer);
		DamageNumbers.Spawn(layer, camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 1.5f), DamageNumberKind.Damage, 23, target, true, 28, 30, 1.6f);
		var label = layer.GetChildren().OfType<DamageNumberLabel>().Single(); await Wait(.08);
		camera.GlobalPosition += Vector3.Right * 5; await Wait(.06);
		Vector2 projected = camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 1.5f);
		Check(label.Visible && label.Position.DistanceTo(projected) < 12, "057大幅移动相机后跳字仍围绕实时目标投影");
		target.GlobalPosition += Vector3.Right * 2; await Wait(.06);
		Check(Mathf.Abs(label.Position.X - camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 1.5f).X) < 1,
			"057目标移动时数字同步跟随，上飘只改变局部偏移");
		await Shot("057-following-damage-number");
		Vector3 last = target.GlobalPosition + Vector3.Up * 1.5f; target.QueueFree(); await Wait(.04);
		camera.GlobalPosition += Vector3.Left * 3; camera.LookAt(last); await Wait(.04);
		Check(Mathf.Abs(label.Position.X - camera.UnprojectPosition(last).X) < 1, "057目标释放后仍投影最后世界位置，不访问已释放对象");
		camera.LookAt(camera.GlobalPosition + Vector3.Back); await Wait(.05);
		Check(!label.Visible, "057锚点在镜头背后时不反投影到屏幕");
		camera.LookAt(last); await Wait(.04);
		await Wait(1.6); Check(!IsInstanceValid(label), "057跟随数字按时回收");
		layer.QueueFree(); camera.QueueFree();
	}
	private async Task Drops()
	{
		var field = Director.ExperienceField!; field.Clear();
		Vector3 spot = Player.GlobalPosition + Vector3.Right * 6 + Vector3.Forward * 10;
		if (ActRoguelike.World.MapManager.Instance!.TrySnap(spot, out var ground)) spot = ground;
		Unit Victim(Vector3? position = null)
		{
			var enemy = MakeEnemy(position ?? spot); Director._PhysicsProcess(.51);
			Check(Director.OwnsEnemy(enemy), "058由正式导演登记真实死亡回调"); return enemy;
		}
		var first = Victim(spot + Vector3.Right * 3); first.ApplyDirectDamage(100000, Player); await Wait(.05);
		Check(field.Count == 1 && field.StoredExperience > 0, "058无灵魂收割时真实击杀掉经验");
		var build = new RunBuild(); var definition = RewardEffectRegistry.Compile(ConfigRows.ById<RewardConfig>("mut_soul_harvest")!)!;
		build.Add(definition, new RewardContext { Player = Player, Weapon = NodeSearch.FindInSubtree<Weapon>(Player)!, Build = build });
		var rule = Player.GetChildren().OfType<SoulHarvestRule>().Single(); rule.Chance = 1;
		var second = Victim(); second.ApplyDirectDamage(100000, Player); await Wait(.05);
		var soul = GetTree().CurrentScene.GetChildren().OfType<SoulPickup>().Single();
		Check(field.Count == 2 && rule.DropCount == 1, "058真实授予灵魂收割后经验与灵魂同时产生，无掉落种类互斥");
		var mesh = field.GetChildren().OfType<MultiMeshInstance3D>().First().Multimesh;
		Vector3 experiencePosition = field.ToGlobal(mesh.GetInstanceTransform(1).Origin) with { Y = 0 };
		Check(experiencePosition.DistanceTo(soul.GlobalPosition with { Y = 0 }) > .65f, "058同次死亡经验与灵魂落点摊开，不重叠");
		Vector3 third = PickupDropLayout.Claim(second, spot) with { Y = 0 };
		Check(third.DistanceTo(experiencePosition) > .65f && third.DistanceTo(soul.GlobalPosition with { Y = 0 }) > .65f,
			"058第三种掉落可通过同一摆放接口独立领取空位");
		rule.Chance = 0; var missed = Victim(spot + Vector3.Left * 3); missed.ApplyDirectDamage(100000, Player); await Wait(.05);
		Check(field.Count == 3 && rule.DropCount == 1, "058灵魂概率未命中也不影响经验掉落");
		missed.ApplyDirectDamage(100000, Player); Check(field.Count == 3 && rule.DropCount == 1, "058同一死者不会重复发放");
		await Wait(1.25);
		var camera = View(spot); await Shot("058-independent-spread-drops"); camera.QueueFree();
		Player.ApplyDirectDamage(50); float health = Player.Health; Player.GlobalPosition = spot; Player.ResetPhysicsInterpolation();
		Check(await Until(() => field.Count == 0 && !IsInstanceValid(soul)) && Director.Experience > 0 && Player.Health > health,
			"058经验正常计入等级且灵魂恢复生命，两类掉落均可实际拾取");
		Player.Stats.RemoveBySource(build);
		foreach (var child in Player.GetChildren().OfType<SoulHarvestRule>().ToArray()) { child.Stop(); child.QueueFree(); }
	}
	private async Task Backdash()
	{
		var floor = new StaticBody3D { CollisionLayer = CombatLayers.World };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) } });
		floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(40, 1, 40), Material = new StandardMaterial3D { AlbedoColor = new Color(.18f, .24f, .28f) } } });
		Mode.AddChild(floor); floor.GlobalPosition = new Vector3(0, 24.5f, 0);
		new PlayerLoadout().Apply(Player, Mode, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Player.Movement!.SetPhysicsProcess(true); Player.GlobalPosition = new Vector3(0, 25.05f, 0); Player.Rotation = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(.15);
		var dash = Player.GetNode<BackdashAbility>("AbilitySkill1");
		Check(dash.BackwardDistance == 12 && dash.TravelSeconds == .3f, "066覆盖059，正式技能行预设12米与0.3秒");
		dash.BackwardDistance = 0; Vector3 start = Player.GlobalPosition; dash.RefreshCooldown();
		Check(dash.TryPress(), "059旧冲量对照同样经真实Dodge动画事件启动"); await Wait(1.5);
		float baseline = (Player.GlobalPosition - start).Length();
		dash.ResetForLoadout(); dash.BackwardDistance = 12; Player.GlobalPosition = new Vector3(0, 25.05f, 0); Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(.1);
		start = Player.GlobalPosition; float yaw = Player.Rotation.Y; Check(dash.TryPress(), "059正式后撤可发动");
		Check(await Until(() => Player.Movement.HasVelocityOverride), "059真实Dodge事件开启定距位移"); ulong began = Engine.GetPhysicsFrames();
		Check(await Until(() => !Player.Movement.HasVelocityOverride), "059有限位移结束释放速度覆盖");
		float distance = (Player.GlobalPosition - start).Length(); float seconds = (Engine.GetPhysicsFrames() - began) / (float)Engine.PhysicsTicksPerSecond;
		Check(Mathf.Abs(distance - 12) < .4f && Math.Abs(distance / baseline - 4) < .2f, $"066空地实移{distance:0.00}m，旧冲量{baseline:0.00}m，比059的6米再翻倍");
		Check(seconds >= .24f && seconds <= .34f && Mathf.Abs(Player.Rotation.Y - yaw) < .001f, $"066在{seconds:0.00}s内完成且不改变朝向");
		dash.ResetForLoadout(); Player.GlobalPosition = new Vector3(0, 25.05f, 0); Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(.08);
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World }; wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 4, .3f) } });
		Mode.AddChild(wall); wall.GlobalPosition = new Vector3(0, 26.5f, 2); await Wait(.05);
		start = Player.GlobalPosition; dash.TryPress(); await Wait(.65);
		Check(Player.GlobalPosition.DistanceTo(start) < 2 && !Player.Movement.HasVelocityOverride, "059后撤被真实地形挡住，不穿墙且不会留下覆盖"); wall.QueueFree();
		dash.ResetForLoadout(); Player.GlobalPosition = new Vector3(0, 30, 0); Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(.04);
		float height = Player.GlobalPosition.Y; dash.TryPress(); await Wait(.6);
		Check(Player.GlobalPosition.Y < height - .5f, "059空中后撤保持正常重力，不锁住垂直高度");
		dash.ResetForLoadout(); Player.GlobalPosition = new Vector3(0, 25.05f, 0); Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(.1);
		dash.TryPress(); await Until(() => Player.Movement.HasVelocityOverride); dash.ResetForLoadout();
		Check(!Player.Movement.HasVelocityOverride && !Player.IsInvulnerable, "059取消或换装清理自身位移和无敌");
	}
	private async Task Parry()
	{
		new PlayerLoadout().Apply(Player, Mode, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		Player.IsInvulnerable = false; var enemy = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 4);
		var camera = View(Player.GlobalPosition); var guard = Player.GetNode<GuardStanceAbility>("AbilityBSkill2");
		camera.GlobalPosition = Player.GlobalPosition + new Vector3(3, 2, 4.5f); camera.LookAt(Targeting.AimPoint(Player));
		Check(guard.TryPress() && await Until(() => guard.StanceActive), "060真实GuardOn开启正式招架窗口");
		await Wait(.08); int presentations = CombatFeedback.PresentedCount, sounds = CombatFeedback.SoundCount; float health = Player.Health;
		var hit = new AttackInstance { Attacker = enemy, BaseDamage = 10 }.RollHit(Player, Vector3.Back, Targeting.AimPoint(Player));
		Player.ReceiveHit(hit);
		Check(hit.Parried && Player.Health == health && CombatFeedback.PresentedCount == presentations + 1 && CombatFeedback.LastKind == ImpactKind.Parry,
			"060实际近战成功招架只请求一次反馈且不受伤");
		Check(CombatFeedback.SoundCount == sounds + 1 && GetTree().CurrentScene.GetChildren().OfType<CombatImpactFx>().Any(fx => fx.Kind == ImpactKind.Parry),
			"060成功招架产生独立金色冲击环/火花及格挡音效");
		CombatFeedback.Present(Player, hit, new DamageResult { Hit = true, Invulnerable = true });
		Check(CombatFeedback.PresentedCount == presentations + 1, "060同一命中的再次路由不会重复播放");
		await Wait(.035); await Shot("060-parry-success");
		presentations = CombatFeedback.PresentedCount; sounds = CombatFeedback.SoundCount;
		var projectile = new Projectile { Speed = 16, SeekAnyHostile = false }; Mode.AddChild(projectile); projectile.GlobalPosition = Targeting.AimPoint(enemy);
		projectile.Launch(new AttackInstance { Attacker = enemy, BaseDamage = 10, Lifetime = 3 }, (Targeting.AimPoint(Player) - projectile.GlobalPosition).Normalized());
		Check(await Until(() => projectile.ReflectionCount > 0) && CombatFeedback.PresentedCount == presentations + 1 && CombatFeedback.SoundCount > sounds && Player.Health == health,
			"060真实弹体在ReceiveHit前反射也有且只有一次招架特效和音效");
		projectile.QueueFree(); guard.ResetForLoadout(); await Wait(.4); presentations = CombatFeedback.PresentedCount;
		var normal = new AttackInstance { Attacker = enemy, BaseDamage = 10 }.RollHit(Player, Vector3.Back, Targeting.AimPoint(Player));
		Player.ReceiveHit(normal);
		Check(!normal.Parried && CombatFeedback.LastKind != ImpactKind.Parry && CombatFeedback.PresentedCount == presentations + 1,
			"060未处于招架窗口时不会伪造招架反馈"); camera.QueueFree();
	}
}
