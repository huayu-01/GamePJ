using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>独立真实物理场：身体阻挡、阵营命中、弹道、奔跑轻推与三维高度隔离。</summary>
public partial class BodyCollisionProbe : Node3D
{
	private int _pass, _fail;
	private readonly List<Unit> _units = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Body] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private Unit Spawn(Faction faction, Vector3 position)
	{
		var unit = new Unit { Faction = faction, MaxHealth = 100 };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f } });
		unit.AddChild(new CharacterMovement { Name = "Movement", FaceMovementDirection = false });
		unit.AddChild(new SprintController { Name = "Sprint" });
		AddChild(unit); unit.GlobalPosition = position; unit.SetPhysicsProcess(false); unit.Movement!.SetPhysicsProcess(false);
		_units.Add(unit); return unit;
	}
	private async Task Drive(Unit unit, Vector3 direction, int frames, HappyRunnerRule? happy = null, Unit? pushed = null)
	{
		unit.Movement!.SetIntent(direction, 6);
		for (int i = 0; i < frames; i++) { await Frame(); unit.Movement._PhysicsProcess(1d / 60d); happy?._PhysicsProcess(1d / 60d); pushed?.Movement!._PhysicsProcess(1d / 60d); }
		unit.Movement.Stop();
	}
	private async Task Clear()
	{
		foreach (var unit in _units) if (IsInstanceValid(unit)) unit.QueueFree(); _units.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("projectiles")) node.QueueFree();
		await Frame(); await Frame();
	}
	private static void Damage(Unit unit, float amount) => unit.ApplyDirectDamage(amount);
	private HappyRunnerRule Happy(Unit unit)
	{
		var weapon = new Weapon(); unit.AddChild(weapon);
		RewardEffectRegistry.Compile(ConfigRows.ById<RewardConfig>("mut_happy_runner")!)!.Apply(new RewardContext { Player = unit, Weapon = weapon, Build = new RunBuild() });
		var rule = unit.GetChild<HappyRunnerRule>(unit.GetChildCount() - 1); rule.SetPhysicsProcess(false); return rule;
	}
	public override async void _Ready()
	{
		try
		{
			await Frame(); await Frame();
			Check(ProjectSettings.GetSetting("layer_names/3d_physics/layer_10").AsString() == "UnitBody", "编辑器第10层命名为单位身体");
			foreach (Faction from in new[] { Faction.Player, Faction.Enemy })
			foreach (Faction to in new[] { Faction.Player, Faction.Enemy })
			{
				var mover = Spawn(from, new Vector3(0, 0.05f, 0)); var body = Spawn(to, new Vector3(2, 0.05f, 0));
				Check(mover.CollisionLayer == CombatLayers.BodyLayerOf(from) && body.CollisionMask == CombatLayers.BodyMask, $"身体共层但保留阵营身份 {from}/{to}");
				await Drive(mover, Vector3.Right, 45);
				Check(mover.GlobalPosition.X < body.GlobalPosition.X - 0.7f && mover.GlobalPosition.X > 0.5f, $"真实移动被{to}身体挡住 {from} x={mover.GlobalPosition.X:0.###}");
				await Clear();
			}
			await Height(); await Clear();
			await Attacks(); await Clear();
			await Bullets(); await Clear();
			await HappyContact(); await Clear();
			await Regeneration(); await Clear();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Body] === 身体分层验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Height()
	{
		var mover = Spawn(Faction.Player, new Vector3(0, 0.05f, 0)); Spawn(Faction.Enemy, new Vector3(2, 4, 0));
		await Drive(mover, Vector3.Right, 45); Check(mover.GlobalPosition.X > 3, "高空单位不因共用身体层挡住地面路径");
		await Clear(); mover = Spawn(Faction.Player, new Vector3(0, 4, 0)); var airborne = Spawn(Faction.Enemy, new Vector3(2, 4, 0));
		// 只隔离本测试的重力；相同三维高度依然走真实身体查询。
		await Frame(); await Frame();
		mover.Movement!.GravityScale = 0; airborne.Movement!.GravityScale = 0;
		await Drive(mover, Vector3.Right, 45); Check(mover.GlobalPosition.X < 1.3f, "同高度空中单位仍可实体阻挡");
		Vector3 received = Vector3.Zero; airborne.KnockbackReceived += impulse => received = impulse;
		airborne.ReceiveKnockback(new Vector3(1, -3, 2)); Check(received.IsEqualApprox(new Vector3(1, -3, 2)), "受力入口保留三维分量，可供以后压低高度／打落使用");
	}
	private async Task Attacks()
	{
		foreach (Faction faction in new[] { Faction.Player, Faction.Enemy })
		{
			var owner = Spawn(faction, new Vector3(0, 0.05f, 0));
			var ally = Spawn(faction, new Vector3(1, 0.05f, 0)); var hostile = Spawn(faction == Faction.Player ? Faction.Enemy : Faction.Player, new Vector3(-1, 0.05f, 0));
			var box = new AttackHitbox(); box.AddChild(new CollisionShape3D { Name = "Shape", Shape = new SphereShape3D { Radius = 2 } }); owner.AddChild(box);
			await Frame(); await Frame(); box.Activate(owner, new AttackInstance { Attacker = owner, BaseDamage = 10 }); box.SweepSphere(box.GlobalPosition, box.GlobalPosition);
			Check(hostile.Health == 90 && ally.Health == 100 && owner.Health == 100, $"{faction}攻击只命中敌对阵营");
			Check(box.CollisionMask == CombatLayers.HostileTo(faction) && (box.CollisionMask & CombatLayers.UnitBody) == 0, "攻击盒不查询共用身体层");
			box.Deactivate(); await Clear();
		}
	}
	private async Task Bullets()
	{
		foreach (Faction faction in new[] { Faction.Player, Faction.Enemy })
		{
			var owner = Spawn(faction, new Vector3(0, 0.05f, 0)); var ally = Spawn(faction, new Vector3(2, 0.05f, 0));
			var enemy = Spawn(faction == Faction.Player ? Faction.Enemy : Faction.Player, new Vector3(4, 0.05f, 0));
			await Frame(); await Frame();
			var bullet = new Projectile { Speed = 30 }; AddChild(bullet); bullet.GlobalPosition = new Vector3(0.6f, 0.8f, 0);
			bullet.Launch(new AttackInstance { Attacker = owner, BaseDamage = 10 }, Vector3.Right);
			Check(bullet.HitMask == (CombatLayers.World | CombatLayers.HostileTo(faction)), $"{faction}子弹查询敌对单位＋地形");
			for (int i = 0; i < 16; i++) await Frame();
			Check(enemy.Health == 90 && ally.Health == 100, "真实子弹穿过友军并命中敌对单位"); await Clear();
		}
		var player = Spawn(Faction.Player, new Vector3(6, 0.05f, 0)); var behind = Spawn(Faction.Enemy, new Vector3(10, 0.05f, 0));
		await Frame(); await Frame(); var shot = new Projectile { Speed = 60 }; AddChild(shot); shot.GlobalPosition = new Vector3(6.6f, 0.8f, 0); shot.Launch(new AttackInstance { Attacker = player, BaseDamage = 10 }, Vector3.Right);
		for (int i = 0; i < 12; i++) await Frame(); Check(behind.Health == 100 && !IsInstanceValid(shot), "地形挡住子弹，墙后敌人不受伤");
	}
	private async Task HappyContact()
	{
		var player = Spawn(Faction.Player, new Vector3(0, 0.05f, 0)); var enemy = Spawn(Faction.Enemy, new Vector3(1.2f, 0.05f, 0));
		var rule = Happy(player); player.GetNode<SprintController>("Sprint").DebugSprintOverride = true;
		await Drive(player, Vector3.Right, 40, rule, enemy);
		Check(rule.PushCount is > 0 and <= 3 && enemy.GlobalPosition.X > 1.4f, "奔跑真实身体接触轻推敌人，有重复触发间隔");
		Check(enemy.Health == 100 && player.Health == 100 && player.CollisionMask == CombatLayers.BodyMask, "轻推无伤害，不改身体阻挡、不赋予无敌");
		int pushes = rule.PushCount; rule._PhysicsProcess(0.001); rule._PhysicsProcess(0.001); Check(rule.PushCount == pushes, "同一接触不会每帧重复施力");
		await Clear(); player = Spawn(Faction.Player, new Vector3(0, 0.05f, 0)); enemy = Spawn(Faction.Enemy, new Vector3(1.2f, 0.05f, 0)); enemy.Tags = new[] { "Boss" };
		rule = Happy(player); player.GetNode<SprintController>("Sprint").DebugSprintOverride = true; await Drive(player, Vector3.Right, 30, rule, enemy);
		Check(rule.PushCount == 0 && enemy.GlobalPosition.X < 1.3f, "奔跑轻推不移动首领");
	}
	private async Task Regeneration()
	{
		var player = Spawn(Faction.Player, new Vector3(0, 0.05f, 0)); var rule = Happy(player); var sprint = player.GetNode<SprintController>("Sprint"); sprint.DebugSprintOverride = true;
		Damage(player, 50); rule._PhysicsProcess(9.9); Check(player.Health == 50, "10秒未到不回血");
		rule._PhysicsProcess(0.3); Check(Mathf.Abs(player.Health - 50.2f) < 0.01f, "跨过10秒只按超出部分每秒1点回血");
		sprint.DebugSprintOverride = false; float before = player.Health; rule._PhysicsProcess(2); Check(player.Health == before, "退出奔跑停止回血");
		sprint.DebugSprintOverride = true; rule._PhysicsProcess(1); Check(Mathf.Abs(player.Health - before - 1) < 0.01f, "安全状态重新奔跑继续回血");
		Damage(player, 1); before = player.Health; rule._PhysicsProcess(9.9); Check(player.Health == before, "实际受伤重新等待10秒");
		player.Stats.AddModifier(new StatModifier { Stat = "HealMultiplier", Operation = ModifierOperation.Override, Value = 0 }); rule._PhysicsProcess(2); Check(player.Health == before, "回血复用治疗入口，尊重死铠禁疗");
		await Frame();
	}
}
