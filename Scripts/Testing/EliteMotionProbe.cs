using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Weapons;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>七项机动／射击特性，使用正式模型、弹体、导航和真实伤害验证。</summary>
public partial class EliteMotionProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "EliteMotion";
	private Projectile[] Shots(Unit owner) => GetTree().GetNodesInGroup("projectiles").OfType<Projectile>()
		.Where(p => IsInstanceValid(p) && !p.IsQueuedForDeletion() && p.CurrentAttack?.Attacker == owner).ToArray();
	private void ClearShots() { foreach (var p in GetTree().GetNodesInGroup("projectiles").OfType<Projectile>()) p.QueueFree(); }
	private Unit Ranged(Vector3 position)
	{
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/RangedEnemy.tscn");
		var unit = scene.Instantiate<Unit>(); ConfigRows.ByScenePath<EnemyConfig>(scene.ResourcePath)?.ApplyTo(unit);
		Mode.AddChild(unit); unit.GlobalPosition = position; unit.ResetPhysicsInterpolation();
		unit.ApplyBaseStats(5000, 0, 0, 0); unit.FillVitals();
		foreach (Node child in unit.GetChildren()) if (child.GetType().Name == "EnemyBrain") child.ProcessMode = ProcessModeEnum.Disabled;
		unit.Movement?.SetPhysicsProcess(false); return unit;
	}
	private async Task Retire(EliteTraitWave wave, Unit owner)
	{
		wave.Stop(); wave.World.Clear(); wave.World.QueueFree(); wave.QueueFree(); owner.QueueFree(); ClearShots(); await Wait(.08);
	}
	protected override async Task RunChecks()
	{
		Player.GlobalPosition = new Vector3(0, .1f, 20); Player.ResetPhysicsInterpolation();
		await Speed(); await Projectiles(); await Knockback(); await Summon(); await Reposition(); await Controls();
	}
	private async Task Speed()
	{
		var unit = MakeEnemy(Player.GlobalPosition + new Vector3(5, 0, 0));
		var ability = unit.GetChildren().OfType<MeleeAttackAbility>().First();
		ability.Diagnose(Player);
		float move = unit.Stats.Get("MoveSpeedMultiplier"), cooldown = ability.EffectiveCooldown, windup = ability.Windup, animation = ability.AnimSpeedScale;
		var def = Definition("speeder"); def.Magnitude = 1.25f;
		var wave = MakeWave(def, unit);
		Check(Mathf.IsEqualApprox(unit.Stats.Get("MoveSpeedMultiplier"), move * 1.25f), "迅捷真实移速乘区提高25%");
		Check(Mathf.IsEqualApprox(ability.EffectiveCooldown, cooldown / 1.25f), "迅捷真实技能冷却缩短");
		Check(Mathf.IsEqualApprox(EliteProjectileRules.RecoveryScale(unit), 1.25f), "迅捷恢复段读取来源化速度");
		Check(Mathf.IsEqualApprox(ability.Windup, windup) && Mathf.IsEqualApprox(ability.AnimSpeedScale, animation), "迅捷保留前摇和事件动画，准备窗口不缩短");
		wave.Stop();
		Check(Mathf.IsEqualApprox(unit.Stats.Get("MoveSpeedMultiplier"), move) && Mathf.IsEqualApprox(ability.EffectiveCooldown, cooldown)
			&& EliteProjectileRules.RecoveryScale(unit) == 1, "撤销迅捷恢复全部来源修正");
		await Retire(wave, unit);
	}
	private async Task Projectiles()
	{
		var shooter = Ranged(Player.GlobalPosition + new Vector3(0, 0, -12)); await Wait(.08);
		var weapon = shooter.GetNode<Weapon>("AbilityRanged/Weapon"); weapon.SpreadDegrees = 0; weapon.ProjectilesPerShot = 1;
		var seeker = Definition("seeker"); seeker.Radius = 1.8f; seeker.Magnitude = 75;
		Check(EliteProjectileRules.CanSeek(shooter), "真实普通远程可获得追踪");
		var wave = MakeWave(seeker, shooter);
		float hp = Player.Health;
		Check(weapon.TryFire(Projectile.GetTargetCenter(Player)), "旧普通远程真实武器开火");
		var shot = Shots(shooter).Single();
		Check(Mathf.IsEqualApprox(shot.ActualSpeed, weapon.ProjectileSpeed), "旧远程弹体遵守武器配置的实际弹速");
		Check(shot.SeekAnyHostile && Mathf.IsEqualApprox(shot.HomingStartDistance, 1.8f) && shot.HomingTurnRateDegrees == 75, "追踪参数真实装入正式弹体");
		Check(shot.HomingStartedAtDistance < 0 && shot.HomingTargetName.Length == 0, "发射瞬间保留直飞段");
		Player.GlobalPosition += new Vector3(2, 0, 0); Player.ResetPhysicsInterpolation();
		await Wait(.12);
		Check(IsInstanceValid(shot) && shot.HomingStartedAtDistance >= 1.8f && shot.HomingTargetName == Player.Name,
			"直飞达到距离后才真实追踪移动后的玩家");
		await Wait(1.5);
		Check(Player.Health < hp, "追踪弹通过真实碰撞命中玩家");
		await Retire(wave, shooter);

		shooter = Ranged(Player.GlobalPosition + new Vector3(0, 0, -10)); await Wait(.06);
		weapon = shooter.GetNode<Weapon>("AbilityRanged/Weapon"); weapon.SpreadDegrees = 0; weapon.ProjectilesPerShot = 1;
		var multi = Definition("multishot"); multi.Count = 3; multi.Magnitude = 1.35f; multi.Radius = 40;
		wave = MakeWave(multi, shooter);
		Check(weapon.TryFire(Projectile.GetTargetCenter(Player)), "多重射击覆盖旧普通远程发射入口");
		var volley = Shots(shooter);
		Check(volley.Length == 3, "旧远程真实生成三颗独立弹体");
		Check(volley.Select(p => p.CurrentAttack!.CastId).Distinct().Count() == 1, "三颗弹体共用一次出手身份");
		float total = volley.Sum(p => p.CurrentAttack!.BaseDamage);
		Check(Mathf.IsEqualApprox(total, weapon.Damage * shooter.Stats.Get("DamageMultiplier") * 1.35f), "三弹总伤害按一次1.35倍预算分配");
		Check(volley.Length == 3 && Mathf.IsEqualApprox(Mathf.RadToDeg(volley[0].LaunchDirection.AngleTo(volley[^1].LaunchDirection)), 40), "三弹固定在40度夹角内");
		ClearShots(); await Wait(.04);
		EnemyProjectileExecution.Launch(shooter, Player, new EnemyActionState { Kind = EnemyActionStep.ActionKind.Shot,
			Damage = 20, Speed = 8, TravelDistance = 20, TurnDegreesPerSecond = 0 }, "ProbeDirectShot");
		volley = Shots(shooter);
		Check(volley.Length == 3 && volley.All(p => p.CurrentAttack!.HasTag("Projectile")), "新敌人动作发射入口同样生成三弹并携带投射物事实");
		Check(Mathf.IsEqualApprox(volley.Sum(p => p.CurrentAttack!.BaseDamage), 27 * shooter.Stats.Get("DamageMultiplier")), "新动作多弹总伤害没有重复计算增幅");
		ClearShots(); await Wait(.04);
		EnemyProjectileExecution.Launch(shooter, Player, new EnemyActionState { Kind = EnemyActionStep.ActionKind.Meteor,
			Damage = 20, Speed = 8, TravelDistance = 20, Radius = 2 }, "ProbeMeteor");
		Check(Shots(shooter).Length == 1 && Shots(shooter)[0].ExplosionOnly, "多重射击不擅自复制已经预告落点的陨石");
		await Retire(wave, shooter);
		var prototype = new Unit(); prototype.AddChild(new EnemyPlanAbility { Plan = new EnemyActionPlan
		{ Opening = new[] { new EnemyActionStep { Kind = EnemyActionStep.ActionKind.Shot, TurnDegreesPerSecond = 100 } } } });
		Check(!EliteProjectileRules.CanSeek(prototype) && !EliteProjectileRules.CanMultishot(prototype), "原生追踪动作不重复抽追踪或无配置的追踪多弹"); prototype.Free();
	}
	private async Task Knockback()
	{
		var unit = MakeEnemy(Player.GlobalPosition + new Vector3(3, 0, 0));
		var def = Definition("knockback"); def.Cooldown = .65f; def.Magnitude = 6;
		var wave = MakeWave(def, unit); int pushes = 0;
		void Pushed(Vector3 force) { if (force.Length() > 0) pushes++; }
		Player.KnockbackReceived += Pushed;
		void Hit(bool dot = false, bool secondary = false)
		{
			var attack = new AttackInstance { Attacker = unit, Ability = "ProbeHit", BaseDamage = 10, IsDamageOverTime = dot, IsSecondary = secondary };
			Player.ReceiveHit(attack.RollHit(Player, Vector3.Left, Projectile.GetTargetCenter(Player)));
		}
		Hit(); Hit(); Check(pushes == 1, "普通命中产生真实击退，同帧多次限流");
		await Wait(.72); Hit(dot: true); Hit(secondary: true); Check(pushes == 1, "持续伤害和衍生伤害不触发击退");
		Hit(); Check(pushes == 2, "间隔结束后本体攻击恢复击退");
		Player.KnockbackReceived -= Pushed; await Retire(wave, unit);
	}
	private async Task Summon()
	{
		var unit = MakeEnemy(Player.GlobalPosition + new Vector3(5, 0, 0));
		var def = Definition("summoner"); def.Count = 2; def.WarningSeconds = .35f;
		var wave = MakeWave(def, unit); var summoned = new List<Unit>(); wave.RegisterSummoned = (u, _) => summoned.Add(u);
		await Wait(1.6);
		Check(summoned.Count == 2 && wave.SummonedCount == 2, "真实导航合法点召出有限两只随从");
		Check(summoned.All(u => u.HasMeta("EliteSummoned") && EliteTraitRuntime.Find(u) == null), "随从有来源标记且不继承精英特性");
		int committed = wave.SummonedCount;
		foreach (var child in summoned) child.QueueFree(); await Wait(1.4);
		Check(wave.SummonedCount == committed, "随从死亡或退场不恢复召唤额度");
		Check(EliteTraitRuntime.Find(unit)?.Behavior is SummonerTraitBehavior { Finished: true, SummonedCount: 2 }, "一次召唤批次完成后停止请求");
		await Retire(wave, unit);

		unit = MakeEnemy(Player.GlobalPosition + new Vector3(5, 0, 0));
		wave = MakeWave(def, unit); wave.MaxAlive = 0; await Wait(2.65);
		Check(wave.SummonedCount == 0 && EliteTraitRuntime.Find(unit)?.Behavior is SummonerTraitBehavior { Finished: true }, "数量上限阻止生成且失败重试有界");
		await Retire(wave, unit);
	}
	private async Task Reposition()
	{
		var unit = MakeEnemy(Player.GlobalPosition + new Vector3(0, 0, -1.2f)); unit.Movement!.SetPhysicsProcess(true);
		var ability = unit.GetChildren().OfType<MeleeAttackAbility>().First(); ability.Enabled = true; ability.Windup = 4;
		Check(ability.TryActivate(Player), "真实近战进入长前摇用于换位互斥检查");
		var def = Definition("shifter"); def.Cooldown = 1; def.WarningSeconds = .35f; def.Radius = 5;
		var wave = MakeWave(def, unit); var behavior = (ShifterTraitBehavior)EliteTraitRuntime.Find(unit)!.Behavior;
		await Wait(1.3); Check(behavior.RepositionCount == 0, "瞬移不会插入正在执行的攻击");
		ability.InterruptByControl(); ability.Enabled = false; Vector3 start = unit.GlobalPosition;
		await Wait(1.3);
		Check(behavior.RepositionCount >= 1 && unit.GlobalPosition.DistanceTo(start) > 2, "空闲后经过预警完成真实瞬移");
		Check(MapManager.Instance!.TryPath(start, unit.GlobalPosition, out _, unit), "瞬移前后保持导航连通");
		float time = wave.Time; int count = behavior.RepositionCount; GetTree().Paused = true; await Wait(.25); GetTree().Paused = false;
		Check(Mathf.IsEqualApprox(time, wave.Time) && behavior.RepositionCount == count, "暂停不推进波次和瞬移计时");
		await Retire(wave, unit);

		unit = MakeEnemy(Player.GlobalPosition + new Vector3(0, 0, -7)); unit.Movement!.SetPhysicsProcess(true);
		def = Definition("flanker"); def.Cooldown = 1; def.WarningSeconds = .35f; def.Radius = 3; def.Magnitude = 8;
		wave = MakeWave(def, unit); var flank = (FlankerTraitBehavior)EliteTraitRuntime.Find(unit)!.Behavior;
		start = unit.GlobalPosition; await Wait(1.55); Vector3 middle = unit.GlobalPosition; await Wait(1.15);
		Check(flank.RepositionCount >= 1 && unit.GlobalPosition.DistanceTo(start) > 1, "侧翼猎手通过实际身体移动换位");
		Check(middle.DistanceTo(start) > .05f && middle.DistanceTo(flank.LastDestination) > .2f, "侧翼换位具有中间路径过程，不是瞬移");
		Check(MapManager.Instance!.TryPath(start, flank.LastDestination, out _, unit), "侧翼终点在同一可达导航区域");
		await Shot("motion-flanker"); await Retire(wave, unit);
	}
	private async Task Controls()
	{
		var unit = MakeEnemy(Player.GlobalPosition + new Vector3(1.2f, 0, 0)); unit.Movement!.SetPhysicsProcess(true); await Wait(.2);
		var ability = unit.GetChildren().OfType<MeleeAttackAbility>().First(); ability.Enabled = true;
		Check(ability.TryActivate(Player), "无控制时同一目标和距离可以正常发动攻击"); ability.InterruptByControl(); ability.RefreshCooldown();
		unit.ApplyStatus(new StatusEffect { Id = "ProbeControlLock", Duration = .6f, IsControl = true, PreventsActions = true });
		Check(unit.HasActionLock && !ability.TryActivate(Player), "真实控制状态阻止新动作");
		Check(!unit.Movement.TryJump(), "真实控制状态阻止主动跳跃");
		Vector3 start = unit.GlobalPosition; unit.Movement.SetIntent(Vector3.Right, 6); await Wait(.12);
		Check(new Vector2(unit.GlobalPosition.X - start.X, unit.GlobalPosition.Z - start.Z).Length() < .05f, "控制锁停止自主移动");
		Check(unit.ReceiveKnockback(Vector3.Right * 5), "控制锁仍允许外力击退");
		await Wait(.18); Check(unit.GlobalPosition.X > start.X + .1f, "被控制的真实身体仍响应外力");
		await Wait(.45); Check(!unit.HasActionLock, "控制状态正常到期解除");
		unit.QueueFree(); await Wait(.08);
	}
}
