using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实敌人动作驱动范围预警；核对几何、前摇、锁点、多实例和取消生命周期。</summary>
public partial class EnemyWarningProbe : Node3D
{
	private readonly List<Unit> _units = new();
	private int _pass, _fail;
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[Warning] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Wait(int count) { for (int i = 0; i < count; i++) await Frame(); }
	private async Task Until(Func<bool> predicate, int frames = 120) { for (int i = 0; i < frames && !predicate(); i++) await Frame(); }
	private Unit Target(Vector3? position = null)
	{
		var unit = new Unit { Faction = Faction.Player, MaxHealth = 10000 };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .45f, Height = 1.8f } });
		AddChild(unit); unit.GlobalPosition = position ?? new Vector3(0, .05f, -6); _units.Add(unit); return unit;
	}
	private Unit Enemy(string id, Vector3? position = null)
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_" + id)!;
		var enemy = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(enemy);
		AddChild(enemy); enemy.GlobalPosition = position ?? Vector3.Up * .05f;
		enemy.GetNode("Brain").SetPhysicsProcess(false); _units.Add(enemy); return enemy;
	}
	private static SkillIndicator Indicator(Node ability) => ability.GetNode<EnemyAttackWarning>("AttackWarning").Indicator;
	private Projectile? Missile(Unit owner)
	{
		foreach (Node node in GetTree().GetNodesInGroup("projectiles"))
			if (node is Projectile p && p.CurrentAttack?.Attacker == owner) return p;
		return null;
	}
	private async Task Clear()
	{
		foreach (var unit in _units) if (IsInstanceValid(unit)) unit.QueueFree(); _units.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("projectiles")) node.QueueFree(); await Wait(3);
	}
	public override async void _Ready()
	{
		try
		{
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80, 1, 80) } });
			floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(80, 1, 80), Material = new StandardMaterial3D { AlbedoColor = new Color(.12f, .16f, .22f) } } }); AddChild(floor);
			await Wait(3);
			await Instances(); await Clear();
			await Areas(); await Clear();
			await Charges(); await Clear();
			await Meteor(); await Clear();
			await Bombs(); await Clear();
			await Jumps(); await Clear();
			if (DisplayServer.GetName() != "headless") { await Capture(); await Clear(); }
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Warning] === 敌人预警验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Instances()
	{
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/SkillIndicator.tscn");
		var one = scene.Instantiate<SkillIndicator>(); var two = scene.Instantiate<SkillIndicator>(); AddChild(one); AddChild(two);
		one.IsHostile = true; two.IsHostile = true;
		one.ShowLineArea(new Vector3(-8, 0, 0), Vector3.Forward, 7, 2); two.ShowLineArea(new Vector3(8, 0, 0), Vector3.Right, 4, 3);
		var firstMesh = one.GetNode<MeshInstance3D>("DrawRoot/Edge").Mesh!;
		var secondMesh = two.GetNode<MeshInstance3D>("DrawRoot/Edge").Mesh!;
		Check(firstMesh != secondMesh && firstMesh.GetSurfaceCount() > 0 && secondMesh.GetSurfaceCount() > 0, "同时绘制的指示器各自独立ImmediateMesh");
		two.HideAll(); Check(one.IsShowing && firstMesh.GetSurfaceCount() > 0, "隐藏另一个敌人的矩形，不清除本敌人的描边");
		Check(one.DebugState().Contains("碰撞体=0") && one.DebugState().Contains("单位=0") && one.GetGroups().Count == 0, "指示器纯视觉，没有碰撞体、单位或分组");
		one.QueueFree(); two.QueueFree(); await Wait(2);
	}
	private async Task Areas()
	{
		var target = Target(new Vector3(0, .05f, -1.5f)); var enemy = Enemy("exploder"); await Wait(3);
		var ability = enemy.GetNode<AreaAttackAbility>("AbilityExplode"); var indicator = Indicator(ability);
		Check(ability.TryActivate(target) && indicator.IsShowing && indicator.CurrentStyle == SkillIndicator.IndicatorStyle.AreaPoint, "自爆前摇显示范围圈");
		Check(Mathf.IsEqualApprox(indicator.LastRadius, 4) && Mathf.Abs(indicator.LastWorldPoint.Y - .05f) < .03f, $"自爆显示实际伤害半径4米、贴地；不是触发距离{ability.TriggerRadius}");
		float health = target.Health; await Wait(18); Check(target.Health == health && !enemy.IsDead && indicator.IsShowing, "自爆预警在可反应前摇存在，未提前造成伤害");
		enemy.ApplyDirectDamage(1); Check(!ability.IsBusy && !indicator.IsShowing, "无甲自爆受击打断立即清理范围圈");
		await Clear(); target = Target(new Vector3(0, .05f, -1.5f)); enemy = Enemy("exploder"); enemy.AddChild(new EliteModifier()); await Wait(3);
		ability = enemy.GetNode<AreaAttackAbility>("AbilityExplode"); ability.TryActivate(target); indicator = Indicator(ability);
		Check(Mathf.IsEqualApprox(indicator.LastRadius, 5.4f), "精英自爆按真实1.35身体／命中体缩放显示5.4米");
		var ringMaterial = indicator.GetNode<MeshInstance3D>("DrawRoot/Area/Ring").MaterialOverride as StandardMaterial3D;
		Check(ringMaterial != null && ringMaterial.AlbedoColor.R > .9f && ringMaterial.AlbedoColor.B < .4f, "身体攻击闪色与精英底材不覆盖预警红色");
		enemy.ForceKill(); await Wait(2); Check(!indicator.IsShowing, "死亡时尚未释放的自爆预警清理");
		await Clear(); target = Target(new Vector3(0, .05f, -2)); enemy = Enemy("heavy_slammer"); await Wait(3);
		var plan = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); plan.TryActivate(target); indicator = Indicator(plan);
		Check(indicator.IsShowing && Mathf.IsEqualApprox(indicator.LastRadius, 3), "重击蓄势按实际球形命中体显示3米范围");
		await Until(() => plan.ExecutedAttackCount > 0);
		Check(plan.ExecutedAttackCount == 1 && !indicator.IsShowing, "真实ATK执行重击时结束前摇预警");
		await Clear(); target = Target(); enemy = Enemy("screamer"); await Wait(3);
		ability = enemy.GetNode<AreaAttackAbility>("AbilityScream"); ability.TryActivate(target);
		Check(Indicator(ability).IsShowing && Indicator(ability).LastRadius > 5, "既有尖啸范围能力复用同一预警入口");
	}
	private async Task Charges()
	{
		var target = Target(); var enemy = Enemy("charger"); await Wait(3);
		var ability = enemy.GetNode<ChargeAttackAbility>("AbilityCharge"); ability.TryActivate(target);
		var indicator = Indicator(ability); Vector3 locked = indicator.LastDirection;
		Check(indicator.IsShowing && indicator.CurrentStyle == SkillIndicator.IndicatorStyle.LineArea && indicator.LastLength > ability.MaxDistance && indicator.LastWidth > 2, "直线冲锋前摇显示含命中体宽度／前后端的路径区域");
		target.GlobalPosition += Vector3.Right * 3; await Wait(5);
		Check(indicator.LastDirection.Dot(locked) > .999f, "直线冲锋的预警与实际起手锁定方向一致");
		ability.InterruptByControl(); Check(!indicator.IsShowing && !enemy.Movement!.IsDashing, "取消冲锋后隐藏路径并释放冲刺");
		await Clear(); target = Target(); enemy = Enemy("tracking_charger"); await Wait(3);
		var plan = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); plan.TryActivate(target); indicator = Indicator(plan);
		Check(indicator.IsShowing && indicator.CurrentStyle == SkillIndicator.IndicatorStyle.LineArea && Mathf.IsEqualApprox(indicator.LastWidth, 3.6f), "追踪冲锋按实际半径1.8显示路径宽3.6米");
		target.GlobalPosition += Vector3.Right * 3; await Wait(8);
		Check(indicator.LastDirection.X > .2f, "追踪冲锋蓄势朝玩家更新时，预警跟随真实当前方向");
		plan.InterruptByControl(); Check(!indicator.IsShowing, "追踪冲锋打断后不残留路径");
		await Clear(); target = Target(); enemy = Enemy("spear_guard"); await Wait(3);
		plan = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); plan.TryActivate(target); indicator = Indicator(plan);
		Check(!indicator.IsShowing, "纯举盾阶段不误画冲锋预警");
		await Until(() => indicator.IsShowing);
		Check(indicator.IsShowing && indicator.CurrentStyle == SkillIndicator.IndicatorStyle.LineArea && enemy.Block!.Blocking, "举盾后选择持盾冲锋分支才出现路径预警");
	}
	private async Task Meteor()
	{
		var target = Target(); var enemy = Enemy("meteor_mage"); await Wait(3);
		var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); ability.TryActivate(target); var indicator = Indicator(ability);
		Check(indicator.IsShowing && Mathf.IsEqualApprox(indicator.LastRadius, 2.6f), "陨石蓄力显示实际爆炸半径2.6米");
		target.GlobalPosition += Vector3.Right * 2; await Wait(8);
		Check(Mathf.Abs(indicator.LastWorldPoint.X - 2) < .1f && Mathf.Abs(indicator.LastWorldPoint.Z + 6) < .1f, "蓄力期间预警跟随将被采样的目标位置");
		await Until(() => Missile(enemy) != null);
		var projectile = Missile(enemy)!; var landing = projectile.GetNode<EnemyAttackWarning>("AttackWarning").Indicator;
		Check(landing.IsShowing && !indicator.IsShowing, "真实ATK后预警交给独立陨石弹体，施法圈结束");
		Vector3 locked = landing.LastWorldPoint; target.GlobalPosition += Vector3.Right * 5; await Wait(16);
		Check(!ability.IsBusy && IsInstanceValid(projectile) && landing.IsShowing && landing.LastWorldPoint.DistanceTo(locked) < .15f, "施法收招后圈仍存在，已发射陨石不追随移动后的玩家");
		enemy.ForceKill(); await Frame();
		Check(IsInstanceValid(projectile) && landing.IsShowing, "已经发出的落弹预警不被施法者死亡提前清掉");
		await Wait(65); Check(!IsInstanceValid(projectile) && !IsInstanceValid(landing), "陨石命中或失效后同步清理落点预警");
	}
	private async Task Bombs()
	{
		foreach (string key in new[] { "bomb_lobber", "arc_bomber" })
		{
			var target = Target(); var enemy = Enemy(key); await Wait(3);
			var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); ability.TryActivate(target);
			Check(Indicator(ability).IsShowing && Mathf.IsEqualApprox(Indicator(ability).LastRadius, 2.4f), $"{key}前摇显示预估爆炸区");
			await Until(() => Missile(enemy) != null);
			var projectile = Missile(enemy)!; var indicator = projectile.GetNode<EnemyAttackWarning>("AttackWarning").Indicator;
			Check(indicator.IsShowing && Mathf.IsEqualApprox(indicator.LastRadius, projectile.CurrentAttack!.ExplosionRadius), $"{key}飞行期间范围来自本弹真实爆炸快照");
			await Wait(130); Check(!IsInstanceValid(projectile) && !IsInstanceValid(indicator), $"{key}爆炸后不留预警节点"); await Clear();
		}
		var fastTarget = Target(new Vector3(0, .05f, -8)); var fastEnemy = Enemy("bomb_lobber"); await Wait(3);
		var fastAbility = fastEnemy.GetNode<EnemyPlanAbility>("AbilityPlan");
		fastAbility.Plan = (EnemyActionPlan)fastAbility.Plan.Duplicate(true); fastAbility.Plan.Far[0].Speed = 500;
		fastAbility.TryActivate(fastTarget); await Until(() => Missile(fastEnemy) != null);
		var fastProjectile = Missile(fastEnemy)!;
		Check(Mathf.IsEqualApprox(fastProjectile.Speed, 500) && fastProjectile.ActualSpeed <= ConfigIndex.Settings.ProjectileMaxSpeed + .001f,
			$"接入预览后仍保留原始发射Speed，实际弹速独立受全局上限约束 raw={fastProjectile.Speed:R} actual={fastProjectile.ActualSpeed:R}");
	}
	private async Task Jumps()
	{
		var target = Target(); var enemy = Enemy("jumping_melee"); await Wait(3);
		var leap = enemy.GetNode<LeapAttackAbility>("AbilityLeap"); leap.TryActivate(target); var indicator = Indicator(leap);
		Check(indicator.IsShowing && indicator.CurrentStyle == SkillIndicator.IndicatorStyle.LineArea && Mathf.IsEqualApprox(indicator.LastWidth, 2.2f), "旧跳跃敌人按真实盒形落地命中体显示预计范围");
		Check(Mathf.Abs(indicator.LastWorldPoint.Y - .05f) < .03f && indicator.LastWorldPoint.Z < -2, "跳跃落地区域在预测前方的地面，避免圈挂在空中");
		leap.InterruptByControl(); Check(!indicator.IsShowing && !enemy.Movement!.IsDashing, "跳跃打断后预警和水平冲刺一并清理");
		await Clear(); target = Target(); enemy = Enemy("hover_diver"); await Wait(3);
		var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); ability.TryActivate(target); indicator = Indicator(ability);
		Check(indicator.IsShowing && Mathf.IsEqualApprox(indicator.LastRadius, 2.3f), "新俯冲敌人起跳即预告后续落地范围");
		await Wait(22); Check(indicator.IsShowing && enemy.GlobalPosition.Y > 1 && indicator.LastWorldPoint.Y < .1f, "滞空时落地圈保持贴地");
		await Until(() => ability.ExecutedAttackCount > 0);
		ability.InterruptByControl(); Check(!indicator.IsShowing && !enemy.Movement!.HasVelocityOverride, "俯冲中断清理落点和速度所有权");
	}
	private async Task Capture()
	{
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, -25, 0), ShadowEnabled = true });
		var camera = new Camera3D { Position = new Vector3(20, 30, 32), Projection = Camera3D.ProjectionType.Orthogonal, Size = 36, Current = true }; AddChild(camera); camera.LookAt(Vector3.Zero);
		string[] keys = { "exploder", "meteor_mage", "heavy_slammer", "charger" };
		string[] nodes = { "AbilityExplode", "AbilityPlan", "AbilityPlan", "AbilityCharge" };
		Vector3[] positions = { new(-8, .05f, -5), new(6, .05f, -5), new(-8, .05f, 7), new(0, .05f, 9) };
		for (int i = 0; i < keys.Length; i++)
		{
			var enemy = Enemy(keys[i], positions[i]); Vector3 offset = i == 3 ? Vector3.Right * 6 : Vector3.Forward * 1.5f;
			var target = Target(positions[i] + offset); var ability = enemy.GetNode<Ability>(nodes[i]);
			ability.TryActivate(target); ability.SetPhysicsProcess(false); enemy.Movement!.SetPhysicsProcess(false);
			enemy.AddChild(new Label3D { Position = Vector3.Up * 2.8f, Text = ConfigRows.ById<EnemyConfig>("enemy_" + keys[i])!.DisplayName, FontSize = 48, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		}
		var canvas = new CanvasLayer(); AddChild(canvas);
		var label = new Label { Position = new Vector2(24, 20), Text = "敌人攻击预警\n自爆／陨石／重击：实际范围    冲锋：路径与宽度\n复用 SkillIndicator 预制体；多敌人独立显示" }; label.AddThemeFontSizeOverride("font_size", 25); canvas.AddChild(label);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j37_enemy_warning.png") == Error.Ok, "实际渲染预警截图保存");
	}
}
