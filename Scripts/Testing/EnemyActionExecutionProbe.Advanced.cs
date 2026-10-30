using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.World;
using Godot;
using Mode = ActRoguelike.Config.ProjectileEnemyAction.ProjectileMode;

namespace ActRoguelike.Testing;

public partial class EnemyActionExecutionProbe
{
	private async Task Advanced()
	{
		foreach (string name in new[] { "burrow_hunter", "hover_diver", "arc_thrower", "arc_bomber", "bomb_lobber", "meteor_mage" })
		{
			var plan = RuntimeResources.Load<EnemyActionPlan>($"res://Config/EnemyPlans/{name}.tres");
			var steps = plan.Opening.Concat(plan.Near).Concat(plan.Far).Concat(plan.Closing).ToArray();
			Check(steps.Length > 0 && steps.All(s => s.Action != null), $"{name} 的生产动作均有独立定义");
		}
		Check(new ProjectileEnemyAction { Mode = (Mode)999 }.Validate().Any() && new JumpEnemyAction { Speed = float.NaN }.Validate().Any()
			&& new DiveEnemyAction { LandingSeconds = 0 }.Validate().Any() && new BurrowEnemyAction { MinDistance = 8, MaxDistance = 4 }.Validate().Any(), "新动作的非法模式、非有限速度、窗口与出土环在制作期拒绝");
		await Projectiles();
		await NavigationActions();
		await DiveLanding();
	}
	private async Task Projectiles()
	{
		var owner = Actor(Faction.Enemy, new Vector3(-10, .05f, 10)); var target = Actor(Faction.Player, new Vector3(-10, .05f, 4));
		owner.Stats.SetBase("DamageMultiplier", 2);
		foreach (Mode mode in Enum.GetValues<Mode>())
		{
			var definition = new ProjectileEnemyAction { Mode = mode, Damage = 7, Speed = 11, Radius = 2.4f, FlightSeconds = 1.1f, TravelDistance = 22 };
			var state = EnemyActionState.From(new EnemyActionStep { Action = definition, ActiveSeconds = .05f }, owner.GlobalPosition);
			var motion = EnemyProjectileMotion.Resolve(state, owner, target);
			definition.Damage = 999; definition.Speed = 999;
			var execution = definition.Create(Context(owner, target, state)); execution.Begin(); execution.Begin();
			var bullets = GetTree().GetNodesInGroup("projectiles").OfType<Projectile>().Where(p => p.CurrentAttack?.Attacker == owner).ToArray();
			Check(bullets.Length == 1, $"{mode} 同一执行实例只发射一次");
			var bullet = bullets.Single(); bool bomb = mode is Mode.Bomb or Mode.ArcBomb or Mode.Meteor;
			Check(Mathf.IsEqualApprox(bomb ? bullet.CurrentAttack!.ExplosionDamage : bullet.CurrentAttack!.BaseDamage, 14)
				&& bullet.GravityScale == motion.GravityScale && bullet.MaxTravelDistance == 22 && bullet.ExplosionOnly == bomb
				&& bullet.LaunchDirection.DistanceTo(motion.Velocity.Normalized()) < .001f
				&& bullet.SeekAnyHostile == (mode == Mode.HomingShot), $"{mode} 发射与预警使用相同冻结初值，保留乘区和追踪／爆炸语义");
			Vector3 origin = bullet.GlobalPosition; var warning = bullet.GetNodeOrNull<EnemyAttackWarning>("AttackWarning"); execution.Dispose();
			await Frame(3);
			Check(IsInstanceValid(bullet) && !bullet.IsQueuedForDeletion() && bullet.GlobalPosition.DistanceTo(origin) > .1f
				&& (!bomb || warning?.Indicator.IsShowing == true), $"{mode} 动作结束后弹体继续飞行，爆炸预警随弹体保留");
			bullet.QueueFree(); await Frame(2);
			if (bomb) Check(!IsInstanceValid(warning), $"{mode} 弹体释放时一并撤销落点预警");
		}
		var custom = new Projectile { SeekAnyHostile = true, HomingTurnRateDegrees = 999, HomingStatusId = "Mark", HomingStartDistance = 30, BezierHoming = true, GravityStartDistance = 30 };
		var body = new MeshInstance3D { Name = "CustomBody", Mesh = new BoxMesh() }; custom.AddChild(body); body.Owner = custom;
		var scene = new PackedScene(); Check(scene.Pack(custom) == Error.Ok, "制作可替换弹体场景样件"); custom.Free();
		var configured = new ProjectileEnemyAction { ProjectileScene = scene, Mode = Mode.ArcShot, Speed = 11 };
		var configuredState = EnemyActionState.From(new EnemyActionStep { Action = configured }, owner.GlobalPosition); configured.ProjectileScene = null;
		var customExecution = configured.Create(Context(owner, target, configuredState)); customExecution.Begin();
		var customBullet = GetTree().GetNodesInGroup("projectiles").OfType<Projectile>().Single(p => p.CurrentAttack?.Attacker == owner);
		Check(customBullet.HasNode("CustomBody") && customBullet.GetChildren().OfType<MeshInstance3D>().Count() == 1, "弹体使用进入时指定的场景，不叠加默认球形模型");
		Check(!customBullet.SeekAnyHostile && customBullet.HomingTurnRateDegrees == 0 && customBullet.HomingStatusId == string.Empty && !customBullet.BezierHoming
			&& customBullet.HomingStartDistance == 0 && customBullet.GravityStartDistance == 0 && customBullet.GravityScale == 1.5f, "换弹体场景不会混入其预设追踪或延迟重力，发射模式仍由定义决定");
		customExecution.Dispose(); customBullet.QueueFree(); await Frame(2);
		var wrongRoot = new Node3D(); var wrongScene = new PackedScene(); wrongScene.Pack(wrongRoot); wrongRoot.Free();
		Check(new ProjectileEnemyAction { ProjectileScene = wrongScene }.Validate().Any(error => error.Contains("必须使用 Projectile")), "弹体场景根类型错误在制作期报告");
		owner.QueueFree(); target.QueueFree(); await Frame(2);
	}
	private async Task NavigationActions()
	{
		var owner = Actor(Faction.Enemy, new Vector3(-10, .05f, 10)); var target = Actor(Faction.Player, new Vector3(-10, .05f, -2));
		var visual = new Node3D { Name = "Model", Position = new Vector3(.2f, .3f, .1f) }; owner.AddChild(visual); Vector3 rest = visual.Position;
		var hop = new BackHopEnemyAction { Speed = 7 }; var hopState = EnemyActionState.From(new EnemyActionStep { Action = hop, ActiveSeconds = .35f }, owner.GlobalPosition);
		var hopExecution = hop.Create(Context(owner, target, hopState)); hopExecution.Begin();
		Check(hopExecution.IsComplete && !owner.Movement!.HasVelocityOverride && owner.Velocity == Vector3.Zero, "没有导航地图时后跳拒绝移动，不靠射线或裸坐标放行"); hopExecution.Dispose();
		AddToGroup("nav_geometry"); var map = new MapManager { BakeDelaySeconds = .05f }; AddChild(map);
		for (int i = 0; i < 120 && !map.IsReady; i++) await Frame(); Check(map.IsReady, "动作专项使用真实烘焙导航网格");
		hopExecution = hop.Create(Context(owner, target, hopState)); hopExecution.Begin();
		Check(!hopExecution.IsComplete && owner.Movement!.HasVelocityOverride, "后跳在导航位置有效时取得自己的速度覆盖"); hopExecution.Dispose();
		Check(!owner.Movement!.HasVelocityOverride, "取消后跳归还自己的移动覆盖");
		object other = new(); owner.Movement.BeginVelocityOverride(other, Vector3.Right); Vector3 priorVelocity = owner.Velocity;
		hopExecution = hop.Create(Context(owner, target, hopState)); hopExecution.Begin(); hopExecution.Dispose();
		Check(owner.Movement.HasVelocityOverride && owner.Velocity == priorVelocity, "后跳无法抢占其他来源，也不先写入垂直起跳速度"); owner.Movement.EndVelocityOverride(other);
		var burrow = new BurrowEnemyAction(); var burrowState = EnemyActionState.From(new EnemyActionStep { Action = burrow, ActiveSeconds = .5f }, owner.GlobalPosition);
		Vector3 start = owner.GlobalPosition; var execution = burrow.Create(Context(owner, target, burrowState)); execution.Begin(); execution.Tick(.2f);
		Check(visual.Position.Y < rest.Y - 1.4f && owner.GlobalPosition == start, "039规则下遁地换位前只下沉表现根，不依赖骨骼名");
		execution.Dispose(); execution.Tick(1);
		Check(visual.Position == rest && owner.GlobalPosition == start, "中途取消遁地恢复表现且不会迟到传送");
		execution = burrow.Create(Context(owner, target, burrowState)); execution.Begin(); execution.Tick(.5f); execution.Dispose();
		float distance = owner.GlobalPosition.DistanceTo(target.GlobalPosition);
		Check(owner.GlobalPosition.DistanceTo(start) > 3 && distance >= 3.8f && distance <= 8.2f
			&& map.HasBodyClearance(owner.GlobalPosition, owner) && map.TryPath(target.GlobalPosition, owner.GlobalPosition, out _, owner), "完成遁地在4至8米出土环内选可达且有身体净空的位置");
		start = owner.GlobalPosition; burrow.MinDistance = 200; burrow.MaxDistance = 201;
		execution = burrow.Create(Context(owner, target, EnemyActionState.From(new EnemyActionStep { Action = burrow, ActiveSeconds = .5f }, start)));
		execution.Begin(); execution.Tick(.5f); execution.Dispose(); Check(owner.GlobalPosition == start && visual.Position == rest, "出土环没有合法导航点时留在原位并恢复表现");
		owner.QueueFree(); target.QueueFree(); await Frame(2);
	}
	private async Task DiveLanding()
	{
		var owner = Actor(Faction.Enemy, new Vector3(10, 3, 8)); var target = Actor(Faction.Player, new Vector3(10, .05f, 8));
		owner.Movement!.SetPhysicsProcess(true);
		var definition = new DiveEnemyAction { Damage = 13, Speed = 18, Radius = 2.3f };
		var state = EnemyActionState.From(new EnemyActionStep { Action = definition, ActiveSeconds = .3f }, owner.GlobalPosition);
		var execution = definition.Create(Context(owner, target, state)); float health = target.Health; int hits = 0; target.Damaged += (_, _) => hits++;
		execution.Begin(); execution.Tick(.01f);
		Check(target.Health == health && owner.Movement.HasVelocityOverride && execution.WarningVelocity?.Y < 0, "俯冲在空中只有向下速度，落地前没有伤害");
		for (int i = 0; i < 100 && !execution.SuppressWarning && !execution.IsComplete; i++) { await Frame(); execution.Tick(1f / 60); }
		Check(execution.SuppressWarning && Mathf.IsEqualApprox(health - target.Health, 13) && hits == 1 && !owner.Movement.HasVelocityOverride, "真实碰地后只开启一次落地命中体并归还俯冲速度");
		definition.LandingSeconds = 5; execution.Tick(.1f);
		Check(!execution.IsComplete && hits == 1, "落地伤害窗口独立计时，同段扫掠不重复命中");
		execution.Tick(.021f); Check(execution.IsComplete, "落地按进入时冻结的0.12秒窗口结束"); execution.Dispose();
		var fresh = Actor(Faction.Player, target.GlobalPosition); float freshHealth = fresh.Health; await Frame(3);
		Check(fresh.Health == freshHealth, "俯冲结束后新进范围的目标不受残留伤害"); fresh.QueueFree(); owner.QueueFree(); target.QueueFree(); await Frame(2);
		var row = ConfigRows.ById<EnemyConfig>("enemy_hover_diver")!;
		var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit); AddChild(unit); unit.GetNode("Brain").SetPhysicsProcess(false); unit.GlobalPosition = new Vector3(5, .05f, -10);
		target = Actor(Faction.Player, new Vector3(5, .05f, -16)); await Frame(3); var ability = unit.GetNode<EnemyPlanAbility>("AbilityPlan");
		var indicator = ability.GetNode<EnemyAttackWarning>("AttackWarning").Indicator;
		Check(ability.TryActivate(target) && indicator.IsShowing && Mathf.Abs(indicator.LastRadius - 2.3f * unit.Scale.X) < .001f, "生产跳起预警读取后续俯冲定义的实际半径，避免退回旧字段默认值");
		ability.ResetForLoadout(); unit.QueueFree(); target.QueueFree(); await Frame(3);
		// 在极短飞行时限附近碰地，仍必须让协调器保留完整落地伤害窗口。
		row = ConfigRows.ById<EnemyConfig>("enemy_tracking_charger")!;
		unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit); AddChild(unit); unit.GetNode("Brain").SetPhysicsProcess(false); unit.GlobalPosition = new Vector3(0, .05f, -5);
		target = Actor(Faction.Player, new Vector3(0, .05f, -6)); await Frame(5);
		ability = unit.GetNode<EnemyPlanAbility>("AbilityPlan");
		ability.Plan = new EnemyActionPlan { Opening = new[] { new EnemyActionStep { Action = new DiveEnemyAction { Damage = 13, Speed = 18, Radius = 2.3f }, WindupSeconds = .15f, ActiveSeconds = .04f } } };
		health = target.Health; Check(ability.TryActivate(target), "临近飞行时限的俯冲通过真实动画起手");
		for (int i = 0; i < 100 && target.Health == health; i++) await Frame();
		await Frame(2); fresh = Actor(Faction.Player, target.GlobalPosition + Vector3.Right * .2f); freshHealth = fresh.Health;
		await Frame(3);
		Check(target.Health < health && fresh.Health < freshHealth, "协调器没有用飞行时限截断落地窗口，稍后进入的另一目标仍能被命中");
		ability.ResetForLoadout(); fresh.QueueFree(); unit.QueueFree(); target.QueueFree(); await Frame(3);
	}
}
