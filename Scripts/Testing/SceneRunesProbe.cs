using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

public partial class SceneRunesProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "SceneRunes";
	private async Task<SceneRuneController> Begin(SceneRuneDefinition source)
	{
		Director.Stop();
		Director.Definition!.FixedSceneRune = (SceneRuneDefinition)source.Duplicate(true);
		Director.Definition.FixedSceneRune.InitialDelay = 1000;
		Director.EnableSceneRunes = true;
		Director.Begin(Mode.GetNode<RunController>("RunController"), Player); Director.SetPhysicsProcess(true);
		for (int i = 0; i < 600 && Director.State != SurvivalDirector.PhaseState.Surviving; i++) await Wait(.02);
		Director.SetPhysicsProcess(false);
		Check(Director.State == SurvivalDirector.PhaseState.Surviving && Director.SceneRune != null, "正式死战入口安装一种场景符文");
		Check(Director.GetChildren().OfType<SceneRuneController>().Count(c => !c.IsQueuedForDeletion()) == 1, "重进不会叠加第二个场景符文组件");
		Player.FillVitals(); Player.IsInvulnerable = false;
		return Director.SceneRune!;
	}
	protected override async Task RunChecks()
	{
		Mode.GetNode<RunController>("RunController").SetCarriedRune(""); Player.ApplyBaseStats(50000, 0, 0, 0);
		var rows = ConfigIndex.Settings.SceneRunes;
		Check(rows.Length == 8 && rows.Select(r => r.NumericId).Distinct().Count() == 8 && rows.All(r => !r.Validate().Any()), "8个数字身份唯一、参数有效的场景符文，不混入玩家符文或增幅池");
		foreach (var definition in rows)
		{
			var controller = await Begin(definition);
			var hud = Director.GetChildren().OfType<SurvivalHud>().Single(h => !h.IsQueuedForDeletion());
			Check(hud.SceneRuneIconVisible && hud.SceneRuneText.Contains(definition.LocalizedName) && !hud.SceneRuneText.Contains('{'), "HUD显示该场景唯一图标、名称和已解析完整说明");
			Check(controller.TrySpawn() && controller.ActiveCount == 1, "场景元素在所属可达导航地面实际生成");
			var actor = controller.Actors.Single();
			int spawned = controller.SpawnedCount; await Wait(.12);
			Check(controller.SpawnedCount == spawned, "导演暂停推进时不会自行追加实例");
			float elapsed = controller.ElapsedSeconds; GetTree().Paused = true; controller.Tick(100); GetTree().Paused = false;
			Check(controller.SpawnedCount == spawned && controller.ElapsedSeconds == elapsed, "暂停游戏时场景符文时钟与触发均冻结");
			if (definition.Kind == SceneRuneDefinition.RuneKind.Missile) await Visual(actor);
			if (definition.Kind == SceneRuneDefinition.RuneKind.Missile) await Missile(controller, actor);
			else if (definition.Kind == SceneRuneDefinition.RuneKind.StarRain) await Meteor(controller, actor);
			else await Field(controller, (SceneRuneField)actor);
			Check(controller.ActiveCount <= definition.MaxActive, "周期机制受独立实例数量上限约束");
			Director.Stop();
			Check(!controller.Running && controller.ActiveCount == 0 && Player.TemporaryShieldFrom(controller) == 0, "取消／停止即时回收场景效果、临时护盾与地板增伤");
			await Wait(.12);
		}
		var selected = await Begin(rows[0]); var same = selected.Definition;
		Director.SceneRune!.Tick(.05f);
		Check(ReferenceEquals(same, Director.SceneRune.Definition), "推进阶段不会重抽或额外装备场景符文");
		await Shot("scene-rune-hud"); Director.Stop();
	}
	private async Task Visual(SceneRuneActor actor)
	{
		if (DisplayServer.GetName() == "headless") return;
		var camera = new Camera3D(); Mode.AddChild(camera); camera.GlobalPosition = actor.GlobalPosition + new Vector3(0, 6, 5);
		camera.LookAt(actor.GlobalPosition + Vector3.Up); camera.MakeCurrent();
		await Shot("scene-rune-" + actor.Controller.Definition.NumericId); camera.QueueFree();
	}
	private async Task Missile(SceneRuneController controller, SceneRuneActor actor)
	{
		Player.ApplyDirectDamage(100); float hp = Player.Health; Vector3 position = Player.GlobalPosition;
		Vector3 midpoint = (actor.GlobalPosition + Projectile.GetTargetCenter(Player)) * .5f;
		var wall = new StaticBody3D { Position = midpoint, CollisionLayer = CombatLayers.World };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3, 5, 3) } }); Mode.AddChild(wall);
		Player.Movement!.SetPhysicsProcess(true); await Wait(.1);
		for (int i = 0; i < 300 && controller.ImpactCount == 0; i++) { controller.Tick(.04f); await Wait(.005); }
		Check(controller.ImpactCount == 1, "飞弹真实扫中玩家，途中地形不会拦截");
		await Wait(.08);
		switch (controller.Definition.Impact)
		{
			case SceneRuneDefinition.MissileEffect.Damage: Check(Player.Health < hp, "冲击飞弹只通过统一受伤管线造成伤害"); break;
			case SceneRuneDefinition.MissileEffect.Heal: Check(Player.Health > hp, "治疗飞弹实际恢复玩家生命"); break;
			case SceneRuneDefinition.MissileEffect.Shield: Check(Player.TemporaryShieldFrom(controller) > 0 && Player.Health == hp, "护盾飞弹给予有时限护盾，不冒充招架或治疗"); break;
			case SceneRuneDefinition.MissileEffect.Knockback: Check(Player.GlobalPosition.DistanceTo(position) > .05f && Player.Health == hp, "弹开飞弹造成真实位移而不附带伤害"); break;
		}
		Player.Movement.SetPhysicsProcess(false); wall.QueueFree();
	}
	private async Task Field(SceneRuneController controller, SceneRuneField field)
	{
		var enemy = MakeEnemy(field.GlobalPosition + Vector3.Right * .45f);
		Player.GlobalPosition = field.GlobalPosition + Vector3.Up * .04f; Player.ResetPhysicsInterpolation();
		Player.ApplyDirectDamage(50); enemy.ApplyDirectDamage(50);
		float hp = Player.Health, enemyHp = enemy.Health, damage = Player.Stats.Get("DamageMultiplier");
		if (controller.Definition.Kind == SceneRuneDefinition.RuneKind.Updraft)
		{
			Player.Movement!.SetPhysicsProcess(true); enemy.Movement!.SetPhysicsProcess(true); await Wait(.4);
		}
		else await Wait(.15);
		controller.Tick(controller.Definition.WarningSeconds + .01f);
		await Visual(field);
		if (controller.Definition.Kind == SceneRuneDefinition.RuneKind.Updraft)
		{
			await Wait(.25); Check(Player.GlobalPosition.Y > field.GlobalPosition.Y + .3f && enemy.GlobalPosition.Y > field.GlobalPosition.Y + .3f, "浮空场把玩家和敌人真实弹起，不依赖攻击打断或击退抗性");
			Player.Movement!.SetPhysicsProcess(false); enemy.Movement!.SetPhysicsProcess(false);
		}
		else if (controller.Definition.Kind == SceneRuneDefinition.RuneKind.HealingSpring)
			Check(Player.Health > hp && enemy.Health > enemyHp, "治疗泉同样治疗玩家和敌人，沿用各自治疗倍率");
		else
		{
			Check(Mathf.IsEqualApprox(Player.Stats.Get("DamageMultiplier"), damage * controller.Definition.DamageMultiplier)
				&& enemy.Stats.Get("DamageMultiplier") == 1, "力场只提高圈内玩家输出，不给敌人增加同一增伤");
			Player.GlobalPosition += Vector3.Right * (controller.Definition.Radius + 4); await Wait(.1);
			Check(Mathf.IsEqualApprox(Player.Stats.Get("DamageMultiplier"), damage), "离开力场立即撤销增伤");
			Player.GlobalPosition = field.GlobalPosition + Vector3.Up * .04f; await Wait(.1); controller.Tick(.3f);
			controller.Stop(); Check(Mathf.IsEqualApprox(Player.Stats.Get("DamageMultiplier"), damage), "站在圈内停止也不残留增伤");
		}
		enemy.QueueFree();
	}
	private async Task Meteor(SceneRuneController controller, SceneRuneActor actor)
	{
		var enemy = MakeEnemy(actor.GlobalPosition + Vector3.Right * .3f);
		Player.GlobalPosition = actor.GlobalPosition + Vector3.Left * .3f; Player.ResetPhysicsInterpolation(); await Wait(.1);
		float hp = Player.Health, enemyHp = enemy.Health;
		await Visual(actor);
		controller.Tick(controller.Definition.WarningSeconds + controller.Definition.FallHeight / controller.Definition.Speed + .02f);
		Check(enemy.Health < enemyHp && Player.Health == hp, "星雨真实落地范围攻击只伤敌人、不伤落点上的玩家");
		enemy.QueueFree();
	}
}
