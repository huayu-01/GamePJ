using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.AI;
using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实导航与物理上的避让、碰撞分层、暂停／死亡回收和首领同阵营推挤验收。</summary>
public partial class CrowdAvoidanceProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "CrowdAvoidance";
	private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	protected override async Task RunChecks()
	{
		var settings = EnemyCrowdConfig.Current; var original = settings.Mode;
		try
		{
			settings.Mode = EnemyCrowdMode.Avoidance;
			Vector3 center = Player.GlobalPosition + new Vector3(9, 0, -8);
			Check(MapManager.Instance!.TryValidPoint(center, Player.GlobalPosition, out center, Player), "交叉行走测试位置位于真实可达导航网格");
			var left = MakeEnemy(center + new Vector3(-3, .05f, -.18f));
			var right = MakeEnemy(center + new Vector3(3, .05f, .18f));
			left.Movement!.SetPhysicsProcess(true); right.Movement!.SetPhysicsProcess(true);
			await Frames(3);
			Check(left.Navigation!.HasAvoidanceAgent && right.Navigation!.HasAvoidanceAgent, "地面敌人注册真实Server避让代理");
			Check((left.CollisionMask & CombatLayers.Enemy) == 0 && (left.CollisionMask & CombatLayers.UnitBody) == 0
				&& (left.CollisionMask & CombatLayers.Player) != 0 && (left.CollisionMask & CombatLayers.World) != 0,
				"只取消敌敌身体阻挡，仍保留玩家和地形阻挡");
			Check(Player.CollisionMask == CombatLayers.BodyMask, "玩家身体规则保持原样");
			Vector3 leftStart = left.GlobalPosition, rightStart = right.GlobalPosition;
			Vector3 leftGoal = center + new Vector3(3, 0, -.18f), rightGoal = center + new Vector3(-3, 0, .18f);
			float closest = float.MaxValue;
			for (int i = 0; i < 240; i++)
			{
				left.Navigation.MoveTo(leftGoal, 3); right.Navigation!.MoveTo(rightGoal, 3);
				Vector3 difference = left.GlobalPosition - right.GlobalPosition; difference.Y = 0; closest = Mathf.Min(closest, difference.Length());
				await Frames(1);
			}
			Check(left.GlobalPosition.X > leftStart.X + 3 && right.GlobalPosition.X < rightStart.X - 3, "对向路径有实际通过进展，不在交会处永久僵住");
			Check(closest >= (MapManager.BodySize(left).Radius + MapManager.BodySize(right).Radius) * .7f, $"对向通过未深度穿叠，最小中心距{closest:0.###}m");
			var large = MakeEnemy(center + new Vector3(0, .05f, -4)); large.Scale = Vector3.One * 1.8f;
			await Frames(25);
			Check(large.Navigation!.AvoidanceMap == left.Navigation.AvoidanceMap && large.Navigation.AvoidanceRadius > left.Navigation.AvoidanceRadius * 1.5f,
				"不同体型保留不同半径并共享避让模拟");
			GetTree().Paused = true; await Wait(.05);
			Vector3 pausedPosition = left.GlobalPosition; await Wait(.08);
			Check(left.GlobalPosition == pausedPosition, "暂停时Server回调不会自行推动角色");
			GetTree().Paused = false;
			settings.Mode = EnemyCrowdMode.Physical; await Frames(2);
			Check(left.CollisionMask == CombatLayers.BodyMask && !left.Navigation.HasAvoidanceAgent, "切回原碰撞立即恢复身体掩码并释放避让代理");
			settings.Mode = EnemyCrowdMode.PassThrough; await Frames(2);
			Check(left.CollisionMask == (CombatLayers.World | CombatLayers.Player) && !left.Navigation.HasAvoidanceAgent, "无碰撞无避让对照模式独立有效");
			settings.Mode = EnemyCrowdMode.Avoidance; await Frames(2);
			left.ReceiveHit(new HitInstance { Target = left, Attacker = Player, Context = EventContext.Create(Player, left), Damage = 100000, Position = left.GlobalPosition });
			Check(left.IsDead && !left.Navigation.HasAvoidanceAgent, "死亡立即移除避让代理，不留下尸体障碍");
			right.QueueFree(); large.QueueFree(); await Frames(2);
			await CheckBossPush(center);
		}
		finally { GetTree().Paused = false; settings.Mode = original; }
	}
	private async Task CheckBossPush(Vector3 center)
	{
		var boss = MakeEnemy(center + new Vector3(-3, .1f, -3)); boss.Tags = new[] { "Boss" };
		var target = MakeEnemy(center + new Vector3(.2f, .1f, -3)); target.GrantArmor(25);
		target.Movement!.SetPhysicsProcess(true); await Frames(2);
		Vector3 start = target.GlobalPosition; float health = target.Health, armor = target.Armor;
		object source = new(); target.Movement.BeginVelocityOverride(source, Vector3.Forward, 1);
		var pushed = new HashSet<ulong>();
		BossCrowdPush.Sweep(boss, boss.GlobalPosition, center + new Vector3(2, .1f, -3), Vector3.Right, pushed);
		BossCrowdPush.Sweep(boss, boss.GlobalPosition, center + new Vector3(2, .1f, -3), Vector3.Right, pushed);
		await Frames(10);
		Check(pushed.Count == 1 && target.GlobalPosition.X > start.X + .1f, "首领扫过小怪产生推挤，普通技能位移覆盖期间仍立即生效且每目标只推一次");
		Check(target.Health == health && target.Armor == armor && !target.Reactions!.IsStaggered, "同阵营推挤不制造伤害、破甲或受击硬直");
		Check(!boss.Movement!.ReceiveCrowdImpulse(Vector3.Right * 20), "首领自身控制免疫保持有效");
		target.Movement.EndVelocityOverride(source); boss.QueueFree(); target.QueueFree();
	}
}
