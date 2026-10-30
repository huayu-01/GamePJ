using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

public partial class Content032037Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content032037";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Spear;
	private async Task<bool> Until(Func<bool> test, double seconds = 3)
	{
		for (int i = 0; i < seconds * 50; i++) { if (test()) return true; await Wait(.02); }
		return test();
	}
	private static RewardDefinition Reward(string id) => RewardCatalog.Find(id) ?? throw new InvalidOperationException(id);
	private void Grant(string id, RewardContext context) => context.Build.Add(Reward(id), context);
	private HitInstance Hit(Unit target, Unit enemy, float amount) => new AttackInstance { Attacker = enemy, BaseDamage = amount }.RollHit(target, Vector3.Back, target.GlobalPosition);
	protected override async Task RunChecks()
	{
		await Rewards(); await Defense(); await Spear(); await Hover(); await Shroom();
	}
	private async Task Rewards()
	{
		var owner = new Unit { Faction = Faction.Player, MaxHealth = 100, StartingArmor = 0 }; Mode.AddChild(owner);
		var weapon = new Weapon(); owner.AddChild(weapon);
		var build = new RunBuild(); var context = new RewardContext { Player = owner, Weapon = weapon, Build = build };
		Grant("upg_power", context); Grant("upg_power", context);
		Check(Mathf.IsEqualApprox(owner.Stats.Get("DamageMultiplier"), 1.3f), "力量两级按15个百分点相加，不乘算为1.3225");
		Grant("upg_swift", context); Grant("upg_swift", context);
		Check(Mathf.IsEqualApprox(owner.Stats.Get("MoveSpeedMultiplier"), 1.16f), "快速两级固定增加16个百分点");
		Grant("upg_life", context); Grant("upg_life", context);
		Check(owner.MaxHealth == 140 && build.DisplayName(Reward("upg_life")).Contains("2级") && build.DisplayName(Reward("upg_life"), true).Contains("3级"), "生命每级+20，已持有/待选等级分别显示");
		Grant("mut_cleave", context); Grant("mut_cleave_plus", context);
		Check(Mathf.IsEqualApprox(owner.Stats.Get("AttackRangeMultiplier"), 1.7f) && !build.Acquired.Any(r => r.Id == "mut_cleave") && build.Has("mut_cleave"), "升级范围替换前级，历史前置保留且UI只保留当前级");
		Grant("mut_cleave_plus2", context);
		Check(Mathf.IsEqualApprox(owner.Stats.Get("AttackRangeMultiplier"), 2.5f) && build.Acquired.Count(r => r.Id.StartsWith("mut_cleave")) == 1, "第二次升级使用2.5最终倍率，不与前两级乘算");
		Grant("mut_growth", context); Grant("mut_growth_plus", context); Grant("mut_growth_plus2", context);
		Check(owner.Stats.Get("ProjectileCountAdd") == 3, "子弹数量升级替换成总额外3发，不累计旧级");
		Check(ConfigRows.ById<RewardConfig>("upg_armor")!.Weight == 0 && ConfigRows.ById<RewardConfig>("upg_life_plus")!.Weight == 0, "护甲和历史生命分支退出随机强化候选");
		var node = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(RewardNodeKind.Armor)!;
		float armor = owner.Armor; int completed = 0;
		var session = new NodeRewardContext { Player = owner, Config = node, IsActive = () => true, Offer = (_, _) => false,
			Choice = (_, _, _) => false, OnCompleted = () => completed++ };
		Check(node.RewardBehavior!.Begin(session) && !node.RewardBehavior.Begin(session) && completed == 1 && owner.Armor == armor + 30, "独立护甲节点只发30甲一次，不回血或占用强化卡");
		owner.QueueFree(); await Wait(.1);
	}
	private async Task Defense()
	{
		var owner = new Unit { Faction = Faction.Player, MaxHealth = 500, StartingArmor = 10 }; Mode.AddChild(owner);
		var weapon = new Weapon(); owner.AddChild(weapon);
		var build = new RunBuild(); var context = new RewardContext { Player = owner, Weapon = weapon, Build = build };
		Grant("1337", context); Grant("1338", context);
		var enemy = MakeEnemy(owner.GlobalPosition + Vector3.Right * 2);
		owner.ReceiveHit(Hit(owner, enemy, 15));
		var ward = owner.GetChildren().OfType<DefenseReactionRule>().Single(r => r.Definition.Kind == DefenseReactionRewardEffect.ReactionKind.ArmorBreakWard);
		var burst = owner.GetChildren().OfType<DefenseReactionRule>().Single(r => r.Definition.Kind == DefenseReactionRewardEffect.ReactionKind.BlockBurst);
		float hp = owner.Health; float enemyHp = enemy.Health;
		Check(ward.Remaining > 9.9f && ward.GetChildren().OfType<MeshInstance3D>().Any(m => m.Visible), "盾悟由真实破甲触发，存在10秒护罩表现");
		owner.ReceiveHit(Hit(owner, enemy, 20));
		Check(owner.Health == hp && ward.BlockCount == 1 && ward.Remaining == 0 && burst.BurstCount == 1 && enemy.Health < enemyHp, "一次护罩挡下后消耗，并触发铿锵范围伤害");
		owner.ReceiveHit(Hit(owner, enemy, 20)); Check(owner.Health < hp && ward.BlockCount == 1, "护罩不能挡第二次，不变成持续无敌");
		owner.GrantArmor(10); owner.ReceiveHit(Hit(owner, enemy, 15)); await Wait(10.1);
		Check(ward.Remaining == 0 && !ward.GetChildren().OfType<MeshInstance3D>().Any(m => m.Visible), "10秒到期自动回收一次格挡和外观");
		float outside = enemy.Health;
		enemy.GlobalPosition = owner.GlobalPosition + Vector3.Right * 4.5f; await Wait(.1);
		owner.RaiseBlocked(Hit(owner, enemy, 1)); Check(enemy.Health == outside, "未扩大范围时，4米外敌人不受铿锵伤害");
		Grant("mut_cleave", context); owner.RaiseBlocked(Hit(owner, enemy, 1));
		Check(enemy.Health == outside, "一级大刀阔斧范围未覆盖4米，不扩大实际命中到预警外");
		Grant("mut_cleave_plus", context); owner.RaiseBlocked(Hit(owner, enemy, 1));
		Check(enemy.Health < outside, "升级后的大刀阔斧实际扩大铿锵攻击范围");
		owner.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(build);
		int count = burst.BurstCount; owner.RaiseBlocked(Hit(owner, enemy, 1));
		Check(burst.BurstCount == count, "撤销构筑立即解除防御订阅，不残留迟到震荡");
		owner.QueueFree(); enemy.QueueFree(); await Wait(.1);
		var guard = Player.GetNode<GuardStanceAbility>("AbilityBSkill2");
		guard.RefreshCooldown(); Check(guard.TryPress(), "角色B正式招架可以发动");
		Check(await Until(() => guard.StanceActive), "招架等待真实GuardOn打开窗口");
		enemy = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 4); hp = Player.Health;
		int parries = guard.ParryCount; Player.ReceiveHit(Hit(Player, enemy, 10)); Player.ReceiveHit(Hit(Player, enemy, 10));
		Check(guard.ParryCount == parries + 2 && guard.StanceActive && Player.Health == hp && guard.StanceSeconds == 1.5f, "1.5秒窗口连续格挡两次，不因首次命中退出");
		var projectile = new Projectile { Speed = 12, SeekAnyHostile = false }; Mode.AddChild(projectile);
		projectile.GlobalPosition = enemy.GlobalPosition + Vector3.Up * .9f;
		enemyHp = enemy.Health;
		projectile.Launch(new AttackInstance { Attacker = enemy, BaseDamage = 10, Lifetime = 3 }, (Projectile.GetTargetCenter(Player) - projectile.GlobalPosition).Normalized());
		Check(await Until(() => projectile.ReflectionCount > 0) && await Until(() => enemy.Health < enemyHp), "真实飞弹被反射后改为玩家阵营并命中原敌人");
		Check(Player.Health == hp && await Until(() => !guard.StanceActive), "反射不伤玩家，持续窗口到期正常结束");
		guard.RefreshCooldown(); guard.TryPress(); await Until(() => guard.StanceActive);
		var bomb = new Projectile { Speed = 12, ExplosionOnly = true }; Mode.AddChild(bomb);
		bomb.GlobalPosition = enemy.GlobalPosition + Vector3.Up * .9f; enemyHp = enemy.Health;
		bomb.Launch(new AttackInstance { Attacker = enemy, BaseDamage = 10, ExplosionRadius = 1.2f, ExplosionDamage = 15, Lifetime = 3 },
			(Projectile.GetTargetCenter(Player) - bomb.GlobalPosition).Normalized());
		Check(await Until(() => bomb.ReflectionCount > 0) && await Until(() => enemy.Health < enemyHp) && Player.Health == hp, "炸弹在引爆前反射，返回后只伤敌方，不先在玩家身上爆炸");
		await Until(() => !guard.StanceActive);
		enemy.QueueFree(); await Wait(.15);
	}
	private async Task Spear()
	{
		var spear = Player.GetNode<SpearThrowAbility>("AbilityWeapon6Skill2");
		Check(spear.LinearShot && !spear.HoldToCharge && spear.Cooldown == 10 && spear.LanceMaxTargets == 2, "长虹贯日生产配置无蓄力、10秒冷却、2个目标");
		var camera = new Camera3D(); Mode.AddChild(camera); camera.GlobalPosition = Player.GlobalPosition + Vector3.Up * .9f + Vector3.Back * 6;
		camera.LookAt(Player.GlobalPosition + Vector3.Up * .9f + Vector3.Forward * 20); camera.MakeCurrent();
		var targets = Enumerable.Range(0, 3).Select(i => MakeEnemy(Player.GlobalPosition + Vector3.Forward * (4 + i * 2))).ToArray(); await Wait(.1);
		float[] hp = targets.Select(t => t.Health).ToArray();
		spear.RefreshCooldown(); int shots = spear.LanceShots; Check(spear.TryPress(), "点按长枪武技2进入真实动画");
		Check(spear.LanceShots == shots, "攻击事件前不提前生成投枪");
		Check(await Until(() => spear.LanceShots > shots), "真实ATK后发射直线长枪"); await Wait(.3);
		Check(targets[0].Health < hp[0] && targets[1].Health < hp[1] && targets[2].Health == hp[2], "直线投枪实际只伤前两个目标，第三个不穿透");
		Check(!spear.InFlight && !spear.IsCharging && !spear.InputPreviewShowing, "不进入旧离手召回状态，不残留曲线指示器");
		var run = Mode.GetNode<RunController>("RunController"); Check(run.GrantReward("evo_rainbow"), "气贯长虹按正式奖励路径获取");
		Check(spear.LanceUnlimitedTargets && Mathf.IsEqualApprox(spear.Cooldown, 3), "气贯长虹无限穿透、冷却减少70%为3秒");
		hp = targets.Select(t => t.Health).ToArray(); spear.RefreshCooldown(); shots = spear.LanceShots; spear.TryPress();
		await Until(() => spear.LanceShots > shots); await Wait(.3);
		Check(targets.Select((t, i) => t.Health < hp[i]).All(b => b), "气贯长虹真实穿透并伤害三名敌人");
		Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseBuild(run.Build);
		Check(!spear.LanceUnlimitedTargets && spear.Cooldown == 10, "撤销进化还原穿透与冷却");
		foreach (var target in targets) target.QueueFree(); await Wait(.1);
		var air = MakeEnemy(Player.GlobalPosition + new Vector3(0, 5, -8)); await Wait(.1);
		camera.LookAt(Projectile.GetTargetCenter(air)); hp = new[] { air.Health }; spear.RefreshCooldown(); shots = spear.LanceShots;
		spear.TryPress(); await Until(() => spear.LanceShots > shots); await Until(() => air.Health < hp[0]);
		Check(air.Health < hp[0], "向上瞄准时，枪口投枪真实命中准星所指空中靶");
		air.QueueFree(); camera.QueueFree(); await Wait(.1);
	}
	private async Task Hover()
	{
		var run = Mode.GetNode<RunController>("RunController"); var movement = Player.Movement!;
		var hover = Player.GetNode<AirHoverController>("AirHover");
		Check(run.GrantReward("evo_light_leap"), "浮光跃金从正式长枪进化池获取");
		Player.IsInvulnerable = true; movement.SetPhysicsProcess(true); await Wait(.25);
		Vector3 origin = Player.GlobalPosition; float expected = movement.StandardJumpHeight * 3;
		Check(movement.TryJump() && Mathf.Abs(movement.LastTeleportJumpHeight - expected) < .05f, "039上升实际高度等于基础跳高3倍，不放大速度或额外跳跃次数");
		Check(hover.IsHovering && hover.Maintaining && Mathf.Abs(hover.HoverRemaining - .5f) < .05f, "039瞬移跳跃同时获得0.5秒默认保持的浮空");
		await Wait(.05); Player.Velocity = Player.Velocity with { Y = -20 }; hover._PhysicsProcess(.01);
		Check(hover.IsSlowFalling && Player.Velocity.Y > -3, "不按住任何键也会实际减缓下落并显示光环");
		Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = true }); await Wait(.08);
		var fall = Player.AbilitySlots.Resolve("fall_attack")!;
		Check(!hover.IsHovering && !hover.Maintaining && fall.IsActive, "同一次Ctrl动作取消浮空并启动角色B下落攻击，不吞掉输入");
		Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = false }); fall.ResetForLoadout();
		Player.GlobalPosition = origin + Vector3.Up * 4; Player.ResetPhysicsInterpolation(); hover.Grant(.05f); await Wait(.15);
		Check(!hover.IsHovering && !hover.Maintaining, "自动浮空到期后恢复正常下落");
		Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(run.Build, "evo_light_leap");
		Check(!movement.TeleportJumpEnabled && movement.TeleportJumpHoverSeconds == 0, "撤销进化恢复原跳跃参数，不残留三倍高度或浮空授予");
		Player.GlobalPosition = origin; Player.ResetPhysicsInterpolation(); await Wait(.2); movement.SetPhysicsProcess(false); Player.IsInvulnerable = false;
	}
	private async Task Shroom()
	{
		var row = ConfigRows.ById<EnemyConfig>("1339")!;
		var shroom = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(shroom); Mode.AddChild(shroom);
		shroom.GlobalPosition = Player.GlobalPosition + Vector3.Forward * 6;
		Check(row.MoveSpeed == 0 && shroom.GetNode("Brain").GetChildren().All(n => n.Name != "KeepDistance") && row.VisualProfile != null, "胆小菇使用已有骨骼模型，不能靠常规走路位移");
		var plan = shroom.GetNode<EnemyPlanAbility>("AbilityPlan");
		Check(plan.Plan.Far[0].Action is ProjectileEnemyAction { Speed: 6.6f } && row.SpawnResourceCost > 0, "慢速追踪弹与行为压力成本实际注册");
		Vector3 position = shroom.GlobalPosition;
		Check(await Until(() => plan.ExecutedAttackCount > 0, 4), "胆小菇通过真实攻击动画事件发射追踪弹");
		Check(await Until(() => shroom.GlobalPosition.DistanceTo(position) > 1, 5), "胆小菇完成等待后通过导航有效点遁地换位");
		float distance = shroom.GlobalPosition.DistanceTo(Player.GlobalPosition);
		Check(distance >= 3.5f && distance <= 8.5f, "出土位置保持在玩家周围配置距离内");
		Check(ConfigIndex.Settings.Stages.All(s => (s.AllowedEnemyIds.Length == 0 || s.AllowedEnemyIds.Contains(row.Id))) && Director.Definition!.Phases.All(p => p.Enemies.Any(e => e.Id == row.Id)), "原关卡白名单与死战阶段均已接入胆小菇");
		await Shot("content-032-037"); shroom.QueueFree();
	}
}
