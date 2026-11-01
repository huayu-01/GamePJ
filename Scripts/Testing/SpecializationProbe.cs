using System;
using System.Collections.Generic;
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
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Testing;

/// <summary>用真实表、奖励候选、命中结算、物理投射物与无情状态机验证23项特化。</summary>
public partial class SpecializationProbe : Node3D
{
	private int _pass, _fail;
	private readonly List<Unit> _owned = new();
	private readonly Dictionary<Unit, RewardContext> _contexts = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Specialization] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Near(float actual, float expected, string text) => Check(Mathf.Abs(actual - expected) < 0.03f, $"{text}：{actual:0.###} / {expected:0.###}");
	private async Task Wait(double seconds = 0.06) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private Unit CreateOwner(float armor = 0f)
	{
		var owner = new Unit { Faction = Faction.Player, MaxHealth = 100f, StartingArmor = armor, MoveSpeed = 6.5f };
		owner.AddChild(new SprintController { Name = "Sprint" }); AddChild(owner); _owned.Add(owner);
		owner.Stats.SetBase("BaseCritChance", 0.05f); owner.Stats.SetBase("BaseCritMultiplier", 2f);
		var weapon = new Weapon(); owner.AddChild(weapon);
		_contexts[owner] = new RewardContext { Player = owner, Weapon = weapon, Build = new RunBuild() };
		return owner;
	}
	private Unit Enemy(float health = 1000f, float x = 2f, float z = 0f)
	{
		var enemy = new Unit { Faction = Faction.Enemy, MaxHealth = health };
		enemy.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.6f }, Position = Vector3.Up * 0.7f });
		AddChild(enemy); enemy.GlobalPosition = new Vector3(x, 0.05f, z); enemy.SetPhysicsProcess(false); _owned.Add(enemy); return enemy;
	}
	private SpecializationRuntime Grant(Unit owner, params string[] ids)
	{
		foreach (string id in ids) _contexts[owner].Build.Add(RewardCatalog.Find(id)!, _contexts[owner]);
		var rule = owner.GetNode<SpecializationRuntime>(SpecializationRuntime.NodeName); rule.SetPhysicsProcess(false); return rule;
	}
	private static void Hit(Unit from, Unit to, float damage = 1f, bool critical = false, bool secondary = false, bool dot = false)
	{
		var attack = new AttackInstance { Attacker = from, Ability = "SpecializationProbe", BaseDamage = damage, CanCrit = critical, CritChance = critical ? 1f : 0f, IsSecondary = secondary, IsDamageOverTime = dot };
		to.ReceiveHit(attack.RollHit(to, Vector3.Zero, to.GlobalPosition));
	}
	private static void Step(SpecializationRuntime runtime, float dt) => runtime._PhysicsProcess(dt);
	private static SpecializationField[] Fields(SpecializationRuntime runtime) => runtime.GetChildren().OfType<SpecializationField>().Where(f => !f.IsQueuedForDeletion()).ToArray();
	private static void Pulse(SpecializationRuntime runtime, float dt) { foreach (var field in Fields(runtime)) { field.SetPhysicsProcess(false); field._PhysicsProcess(dt); } }
	private async Task Clear()
	{
		foreach (Unit unit in _owned) if (IsInstanceValid(unit)) unit.QueueFree();
		_owned.Clear(); _contexts.Clear(); await Wait();
	}
	public override async void _Ready()
	{
#if DEBUG
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Logs/evidence"));
		try
		{
			RuneCarry.UseInMemoryStoreForSelfTest(); RunSetup.HasSelection = true;
			await Wait(0.2);
			Catalogue(); await Clear();
			Thunder(); await Clear();
			await War(); await Clear();
			await Wind(); await Clear();
			await RealAbilities(); await Clear();
			await ScytheLinks(true); await Clear();
			await ScytheLinks(false); await Clear();
			await ActualEnemyLanding(); await Clear();
			await TemporaryVisuals(); await Clear();
			if (DisplayServer.GetName() != "headless") { await ThunderVisual(); await Clear(); }
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Specialization] === 特化验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
#endif
	}
	private void Catalogue()
	{
		var rows = ConfigIndex.Settings.Upgrades.Where(r => r.Specialization != K.None).ToArray();
		int count = ContentInventorySnapshot.Current.Count("Specializations");
		Check(rows.Length == count && rows.Select(r => r.Specialization).Distinct().Count() == count, $"{count}项特化全部进生产索引且类型唯一");
		Check(RewardCatalog.All.Length == ContentInventorySnapshot.Current.Count("Upgrades") && RewardCatalog.Find("mut_chain") == null && RewardCatalog.Find("mut_sunset_sword") == null && RewardCatalog.Find("mut_homing_missile") == null, "奖励总数符合唯一基线，旧三项仍退出生产目录");
		Check(rows.Count(r => r.Requires.Length == 1) == 22 && rows.Single(r => r.Specialization == K.Storm).Requires.Length == 0, "22项有明确符文前置，暴风没有额外前置");
		foreach (var row in rows) Check(RewardCatalog.Find(row.Id) is { MaxStacks: 1 } definition && !definition.Description.Contains('{'), $"真实行可编译且说明参数展开 {row.DisplayName}");
		var owner = CreateOwner(); var weapon = _contexts[owner].Weapon;
		foreach (var character in ConfigIndex.Settings.Characters)
		foreach (var weaponRow in ConfigIndex.Settings.Weapons)
		{
			weaponRow.ApplyTo(weapon); var pool = new RewardPool(); pool.Initialize(weapon, character.Id);
			var build = new RunBuild();
			var none = pool.Roll(build, 200).Where(d => rows.Any(r => r.Id == d.Id)).ToArray();
			Check(none.Length == 1 && none[0].Id == "upg_storm", $"无符文只有暴风可抽 {character.Id}/{weaponRow.Id}");
			foreach (string token in new[] { "雷之印", "战神刻印", "风之印" })
			{
				build.SetRuneUnlocks(new[] { token }); var rolled = pool.Roll(build, 200);
				bool b2 = character.Id == "chr_weapon_master" && weaponRow.Pool?.Id == "pool_scythe";
				var expected = rows.Where(r => r.Requires.Length == 0 || r.Requires.Contains(ContentIdentity.Prerequisite(token))).Where(r => r.Specialization != K.ThunderRecall || b2).Select(r => r.Id).Order().ToArray();
				var actual = rolled.Where(d => rows.Any(r => r.Id == d.Id)).Select(d => d.Id).Order().ToArray();
				Check(actual.SequenceEqual(expected), $"真实候选与派系／B2双限定 {character.Id}/{weaponRow.Id}/{token}");
				foreach (var item in rolled.Where(d => actual.Contains(d.Id))) build.DebugGrantWithoutApplying(item);
				Check(!pool.Roll(build, 200).Any(d => actual.Contains(d.Id)), "已获得的特化不再次出现");
				build = new RunBuild();
			}
		}
	}
	private void Thunder()
	{
		var owner = CreateOwner(); var target = Enemy(); var nearby = Enemy(x: 4);
		var runtime = Grant(owner, "mut_thunder_mark");
		Hit(owner, target, 5); Step(runtime, 0.01f);
		Check(target.HasStatus("ThunderMark") && runtime.MarkCount == 1, "攻击没有引雷的目标施加引雷");
		Hit(owner, target, 5); Step(runtime, 3.8f);
		Check(runtime.ThunderCount == 0 && runtime.MarkCount == 1, "重复攻击不叠加或刷新4秒引雷");
		Step(runtime, 0.2f); Near(target.Health, 950, "4秒后天雷单体40伤害"); Near(nearby.Health, 1000, "未获得暴雷不波及邻敌");
		Check(!target.HasStatus("ThunderMark") && runtime.MarkCount == 0, "天雷清除引雷，之后可重挂");
		Grant(owner, "evo_thunder_heart", "upg_thunder_mastery", "upg_thunder_blast");
		int chains = runtime.ChainCount;
		Hit(owner, target, 1, secondary: true); Hit(owner, target, 1, dot: true); Step(runtime, 0.01f);
		Check(runtime.ChainCount == chains && runtime.MarkCount == 0, "派生与DoT命中不自续引雷／闪电链");
		float before = target.Health, nearbyBefore = nearby.Health;
		Hit(owner, target); Step(runtime, 0.01f);
		Near(before - target.Health, 27, "雷法主动引爆：主击1＋链14＋天雷40×30%");
		Near(nearbyBefore - nearby.Health, 26, "暴雷范围12＋闪电链14，敌人不重复命中");
		Check(runtime.MarkCount == 0 && runtime.ChainCount == chains + 1, "主动引爆消标，派生链不递归");
		chains = runtime.ChainCount; Hit(owner, owner, 0); Step(runtime, 0.01f); Check(runtime.ChainCount == chains, "自身或友军命中不能触发敌对攻击特化");
		for (int i = 0; i < 5; i++) Enemy(x: 5 + i);
		// 雷之心已有1游戏秒冷却；该样本验证跳数，先推进实际协调器至冷却结束。
		Step(runtime, 1.01f);
		float totalBefore = _owned.Where(u => u.Faction == Faction.Enemy).Sum(u => u.Health);
		Hit(owner, target, 0); Step(runtime, 0.01f);
		float chainDamage = totalBefore - _owned.Where(u => u.Faction == Faction.Enemy).Sum(u => u.Health);
		Check(chainDamage >= 70, "雷法额外两跳，至少五名敌人承受链伤害");
		var armorOwner = CreateOwner(20); var attacker = Enemy(100, x: 1);
		var armorRule = Grant(armorOwner, "mut_thunder_armor");
		Hit(attacker, armorOwner, 5); Near(attacker.Health, 76, "雷凯护甲受伤反10＋闪电链14");
		Step(armorRule, 0.1f);
		armorOwner.AddChild(new BlockComponent { Name = "Block", BlockDamageMultiplier = 0f, FrontDotThreshold = 1f });
		Hit(attacker, armorOwner, 5); Near(attacker.Health, 52, "完全格挡即使不损护甲仍反击");
		var killer = CreateOwner(); var killRule = Grant(killer, "evo_thunder_heart"); var ephemeral = Enemy(1, x: 3);
		Hit(killer, ephemeral); ephemeral.Free(); float survivorHealth = target.Health; Step(killRule, 0.01f);
		Check(killRule.ChainCount == 1 && target.Health < survivorHealth, "主击杀死并释放目标后仍从命中点产生一次闪电链");
	}
	private async Task War()
	{
		var owner = CreateOwner(); var target = Enemy(10000);
		var runtime = Grant(owner, "mut_blood_battle");
		Near(new AttackInstance { Attacker = owner, BaseDamage = 10 }.RollHit(target, Vector3.Zero, target.GlobalPosition).Damage, 10, "血战满血不加伤");
		Hit(target, owner, 50);
		Near(new AttackInstance { Attacker = owner, BaseDamage = 10 }.RollHit(target, Vector3.Zero, target.GlobalPosition).Damage, 20, "血战半血增加100%");
		var blood = CreateOwner(20); Grant(blood, "mut_blood_armor"); Near(blood.MaxHealth, 120, "血铠已有护甲转生命上限"); Near(blood.Armor, 0, "血铠不保留护甲");
		blood.GrantArmor(5); Near(blood.MaxHealth, 125, "后续获得的护甲转生命"); blood.FillVitals(); Near(blood.Armor, 0, "回满不重新产生旧护甲");
		var violent = CreateOwner(); Grant(violent, "upg_violence"); violent.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Value = 1f });
		var offense = AttackOffense.Capture(violent); Near(offense.CritChance, 1, "暴力暴击上限100%"); Near(offense.DamageMultiplier, 1.55f, "155%暴击溢出55%转攻击力");
		var heart = CreateOwner(); Grant(heart, "upg_war_heart");
		_contexts[heart].Build.Add(RewardCatalog.Find("upg_hard")!, _contexts[heart]);
		_contexts[heart].Build.Add(RewardCatalog.Find("upg_hard_plus")!, _contexts[heart]);
		_contexts[heart].Build.Add(RewardCatalog.Find("upg_hard_plus2")!, _contexts[heart]);
		Near(heart.Stats.Get("DamageReductionAdd"), 0, "战斗之心将后获得的三档坚硬全部转走"); Near(heart.Stats.Get("CritMultiplierAdd"), 0.3f, "坚硬升级仅保留最高15%减伤，按1:2转30%爆伤");
		heart.Stats.AddModifier(new StatModifier { Stat = "DamageReductionAdd", Value = 0.2f, OriginId = "other" }); Near(heart.Stats.Get("DamageReductionAdd"), 0.2f, "不吞其他来源减伤");
		Near(heart.Stats.Get("MoveSpeedMultiplier"), 1.3f, "战斗之心移速"); Near(heart.Stats.Get("DamageMultiplier"), 1.5f, "战斗之心攻击"); Near(heart.Stats.Get("CritChanceAdd"), 0.2f, "战斗之心暴击");
		var reversed = CreateOwner(); _contexts[reversed].Build.Add(RewardCatalog.Find("upg_hard")!, _contexts[reversed]); Grant(reversed, "upg_war_heart"); Near(reversed.Stats.Get("CritMultiplierAdd"), 0.1f, "先坚硬再战斗之心结果一致");
		var after = CreateOwner(); var afterTarget = Enemy(10000, x: 30); var afterRuntime = Grant(after, "evo_war_afterimage"); GD.Seed(672);
		for (int i = 0; i < 200; i++) { Hit(after, afterTarget); Step(afterRuntime, 0.01f); }
		Check(afterRuntime.AfterimageCount is > 25 and < 100, $"30%残影真实随机触发 {afterRuntime.AfterimageCount}/200");
		Near(afterTarget.Health, 9800 - afterRuntime.AfterimageCount, "残影只额外造成一次，无递归");
		var thirst = CreateOwner(); Hit(target, thirst, 50); var thirstRuntime = Grant(thirst, "evo_bloodthirst"); Enemy(1).ForceKill(thirst); Step(thirstRuntime, 2.5f);
		Near(thirst.Health, 52.5f, "嗜血逐渐恢复而非瞬间回满"); Near(thirst.Stats.Get("CritChanceAdd"), 0.1f, "嗜血暴击10%");
		Enemy(1).ForceKill(thirst); Near(thirst.Stats.Get("CritChanceAdd"), 0.1f, "重复击杀刷新暴击而不叠出额外层数"); Step(thirstRuntime, 5.1f); Near(thirst.Stats.Get("CritChanceAdd"), 0, "嗜血到期撤销临时暴击");
		var duel = CreateOwner(); var duelRuntime = Grant(duel, "mut_death_duel"); Hit(target, duel, 1000);
		Check(!duel.IsDead && duel.Health == 0 && duelRuntime.DuelActive && !duel.IsInvulnerable, "死斗致命伤保留0血状态，不伪造治疗或无敌");
		Enemy(1).ForceKill(violent); Check(duelRuntime.DuelKills == 0, "别人击杀不计入死斗");
		for (int i = 0; i < 3; i++) Enemy(1).ForceKill(duel);
		Check(!duel.IsDead && !duelRuntime.DuelActive, "死斗限时内完成真实三杀"); Near(duel.Health, 30, "死斗成功恢复30%");
		Hit(target, duel, 1000); Check(duel.IsDead, "20秒冷却期间不会再度避免死亡");
		var failed = CreateOwner(); var failedRuntime = Grant(failed, "mut_death_duel"); Hit(target, failed, 1000); Step(failedRuntime, 5.1f); Check(failed.IsDead, "5秒未达击杀数会真正死亡");
		var renewed = CreateOwner(); var renewedRuntime = Grant(renewed, "mut_death_duel"); Hit(target, renewed, 1000);
		for (int i = 0; i < 3; i++) Enemy(1).ForceKill(renewed);
		Step(renewedRuntime, 20.01f); Hit(target, renewed, 1000); Check(renewedRuntime.DuelActive && !renewed.IsDead, "死斗经过20秒冷却可再次触发");
		await Clear();
		var spear = CreateOwner(); var victim = Enemy(100, x: 3); var spearRuntime = Grant(spear, "evo_war_spear"); await Wait();
		Hit(spear, victim, 1, critical: true); Check(spearRuntime.SpearCount == 1, "真实暴击从玩家发射追踪箭");
		await Wait(0.3); Check(victim.Health < 98, "追踪箭经过真实弹道命中并扣血"); Check(spearRuntime.SpearCount == 1, "派生箭不会再次触发战斗之矛");
	}
	private async Task Wind()
	{
		var owner = CreateOwner(); var enemy = Enemy(); var sprint = owner.GetNode<SprintController>("Sprint");
		var runtime = Grant(owner, "mut_wind_heart", "mut_wind_armor", "evo_wind_trail", "evo_wind_field", "upg_storm");
		Step(runtime, 0.01f); Pulse(runtime, 0.6f); Near(enemy.Health, 1000, "风之心未奔跑不造成伤害");
		sprint.DebugSprintOverride = true; Step(runtime, 0.01f); Pulse(runtime, 0.5f); Near(enemy.Health, 986.44f, "风之心周期12伤害正确叠加暴风13%加伤");
		Near(owner.Stats.Get("IncomingDamageMultiplier"), 0.7f, "风凯奔跑减伤30%");
		Step(runtime, 0.4f); var trail = Fields(runtime).Single(f => f.Kind == K.WindTrail); Near(trail.Radius, 1.5f, "风痕普通旋风的一半大小");
		Vector3 trailPosition = trail.GlobalPosition; owner.GlobalPosition = Vector3.Right * 2; Step(runtime, 0.4f);
		Check(trail.GlobalPosition.IsEqualApprox(trailPosition) && Fields(runtime).Count(f => f.Kind == K.WindTrail) == 2, "奔跑沿路径留下固定旋风");
		Near(trail.Lifetime, 3, "风痕持续3秒");
		sprint.DebugSprintOverride = false; Step(runtime, 0.01f); Near(owner.Stats.Get("IncomingDamageMultiplier"), 1, "退出奔跑撤销风凯减伤");
		Near(runtime.ScaleAgainst(enemy, new AttackInstance()), 1.13f, "暴风按6.5米移速提高13%伤害");
		Grant(owner, "upg_wild_wind"); Near(trail.Radius, 2.25f, "狂风使已存在的风痕同步增大");
		Near(Fields(runtime).Single(f => f.Kind == K.WindHeart).Radius, 6, "狂风使大旋风同步增大");
		Step(runtime, 3.1f); var field = Fields(runtime).Single(f => f.Kind == K.WindField);
		Near(field.Lifetime, 18, "风场持续18秒"); Near(field.Radius, 4.5f, "风场普通旋风也受狂风增大"); Check(field.GlobalPosition.Y >= -0.01f && field.GlobalPosition.DistanceTo(owner.GlobalPosition) <= 8.1f, "风场在范围内有效地面生成");
		if (DisplayServer.GetName() != "headless")
		{
			sprint.DebugSprintOverride = true; Step(runtime, 0.01f); Pulse(runtime, 0.01f);
			var camera = new Camera3D { Position = new Vector3(10, 12, 12), Current = true }; AddChild(camera); camera.LookAt(owner.GlobalPosition);
			await Wait(0.15); Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path("res://Logs/evidence/specialization-wind.png")) == Error.Ok, "风系区域真实渲染截图"); camera.QueueFree();
		}
		trail._PhysicsProcess(3.1); Check(trail.IsQueuedForDeletion(), "风痕到3秒按游戏时间消失");
		field._PhysicsProcess(18.1); Check(field.IsQueuedForDeletion(), "风场到18秒按游戏时间消失");
		await Clear();
		var sword = CreateOwner(); sword.GetNode<SprintController>("Sprint").DebugSprintOverride = true; var victim = Enemy(100, x: 4); var swordRuntime = Grant(sword, "evo_wind_sword"); await Wait();
		Step(swordRuntime, 0.5f); Check(swordRuntime.WindShotCount == 0, "风剑未满1秒不发射"); Step(swordRuntime, 0.5f); Check(swordRuntime.WindShotCount == 1, "奔跑满1秒朝敌人发射旋风弹");
		var windProjectile = GetTree().GetNodesInGroup("projectiles").OfType<Projectile>().Single(p => p.CurrentAttack?.Attacker == sword);
		Grant(sword, "upg_wild_wind"); Near(windProjectile.SweepRadius, 0.525f, "狂风使已飞出的旋风弹判定同步增大");
		await Wait(0.4); Check(victim.Health < 100, "旋风弹经真实物理扫掠命中");
	}
	private async Task<Unit> RealPlayer(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		var player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(player); _owned.Add(player);
		player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled; player.GlobalPosition = new Vector3(0, 0.05f, 0);
		await Wait(0.15); new PlayerLoadout().Apply(player, this, character, weapon);
		_contexts[player] = new RewardContext { Player = player, Weapon = NodeSearch.FindInSubtree<Weapon>(player)!, Build = new RunBuild() }; return player;
	}
	private async Task ScytheLinks(bool groundFirst)
	{
		var player = await RealPlayer(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		void Ground() => _contexts[player].Build.Add(RewardCatalog.Find("evo_ground_burst")!, _contexts[player]);
		if (groundFirst) Ground();
		var runtime = Grant(player, "evo_thunder_recall", "evo_thunder_pool", "mut_thunder_mark", "evo_thunder_heart", "upg_thunder_mastery", "upg_thunder_blast");
		if (!groundFirst) Ground();
		var scythe = (ScytheThrowAbility)Ability.FindByAbilityId(player, "skl_b2_w1_2")!;
		var fall = (FallAttackAbility)Ability.FindByAbilityId(player, "skl_b_fall")!;
		Check(Mathf.IsEqualApprox(fall.LandingKnockback, 6f), $"联动顺序地爆先={groundFirst}：地爆参数保留");
		Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_2", Pressed = true }); await Wait(.04);
		Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_2", Pressed = false });
		for (int i = 0; i < 40 && !scythe.InFlight; i++) await Wait(.025);
		Check(scythe.InFlight, "联动通过真实武技2输入让镰刀离手");
		Input.ParseInputEvent(new InputEventAction { Action = "skill_2", Pressed = true }); await Wait(.04);
		Input.ParseInputEvent(new InputEventAction { Action = "skill_2", Pressed = false });
		Check(Fields(runtime).Any(f => f.Kind == K.ThunderPool), $"镰刀离手期间真实角色技能2输入触发雷池，顺序={groundFirst}");
		await Wait(.8);
		Vector3 destination = scythe.WeaponPosition;
		var victim = Enemy(10000); victim.GlobalPosition = destination + Vector3.Right * 1.5f;
		var movement = new CharacterMovement { Name = "Movement" }; victim.AddChild(movement);
		var impulses = new List<Vector3>(); victim.KnockbackReceived += impulses.Add;
		await Wait(.04);
		int thunders = runtime.ThunderCount;
		Check(scythe.TryPress(), "联动第二次使用镰刀执行落雷术");
		Check(fall.LandingKnockbackPulseCount == 1 && impulses.Any(v => Mathf.IsEqualApprox(v.Length(), 6f)),
			$"落雷术与地爆复用真实范围击退，顺序={groundFirst}，脉冲={fall.LandingKnockbackPulseCount}，冲量=[{string.Join(",", impulses.Select(v => v.Length()))}]");
		Step(runtime, .02f);
		Check(victim.HasStatus("ThunderMark") || runtime.ThunderCount > thunders, "落雷术主攻击附加引雷，雷法可立即引爆，多个雷系特化不丢失");
		Vector3 before = victim.GlobalPosition;
		await Wait(.15);
		Check(victim.GlobalPosition.DistanceTo(before) > .08f, "落雷术＋地爆使有物理身体的存活普通目标真实位移");
	}
	private Unit ActualChaser(Vector3 position)
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_chaser")!;
		var enemy = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>();
		row.ApplyTo(enemy); enemy.GetNode<Node>("Brain").ProcessMode = ProcessModeEnum.Disabled;
		AddChild(enemy); enemy.GlobalPosition = position; ConfigIndex.ScaleEnemy(enemy, this, false); _owned.Add(enemy);
		return enemy;
	}
	private async Task ActualEnemyLanding()
	{
		var player = await RealPlayer(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_contexts[player].Build.Add(RewardCatalog.Find("evo_ground_burst")!, _contexts[player]);
		Grant(player, "evo_thunder_recall");
		player.Stats.SetBase("BaseCritChance", 0); player.Stats.SetBase("CritChanceAdd", -100);
		var scythe = (ScytheThrowAbility)Ability.FindByAbilityId(player, "skl_b2_w1_2")!;
		var fall = (FallAttackAbility)Ability.FindByAbilityId(player, "skl_b_fall")!;
		Check(scythe.TryPress(), "正式普通敌人专项：实际镰刀投出");
		for (int i = 0; i < 60 && !scythe.HasSettled; i++) await Wait(.05);
		Check(scythe.HasSettled, "正式敌人专项等待镰刀到位");
		var normal = ActualChaser(scythe.WeaponPosition + Vector3.Right * 1.8f);
		var fragile = ActualChaser(scythe.WeaponPosition + Vector3.Left * 1.8f);
		fragile.ApplyBaseStats(1, 0, 0, fragile.MoveSpeed);
		HitInstance? death = null; fragile.Died += hit => death = hit;
		int pushes = 0; normal.KnockbackReceived += impulse => { if (Mathf.IsEqualApprox(impulse.Length(), 6)) pushes++; };
		await Wait(.04); Vector3 before = normal.GlobalPosition; float health = normal.Health;
		Check(scythe.TryPress(), "正式敌人专项落雷术执行下落攻击");
		Check(!normal.IsDead && normal.Health < health && pushes == 1, $"真实基础近战存活并收到地爆击退：生命{health:0.##}→{normal.Health:0.##}");
		await Wait(.2);
		Check(normal.GlobalPosition.DistanceTo(before) > .08f, "真实普通敌人的移动组件产生外推位移");
		Check(fragile.IsDead && death?.Attack?.AbilityKind == fall.AbilityId && !fragile.ReceiveKnockback(Vector3.Right * 6), "同圈1血普通敌人由落雷术击杀，死亡后不再执行活体击退");
	}
	private async Task TemporaryVisuals()
	{
		var player = await RealPlayer(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		var runtime = Grant(player, "evo_bloodthirst", "mut_death_duel", "mut_wind_armor");
		await Wait(.1); var fx = player.GetChildren().OfType<StatusEffectFx>().Single();
		void Sync() => fx._PhysicsProcess(.11);
		Enemy(1).ForceKill(player); Step(runtime, .01f); Sync();
		Check(fx.HasNode("SpecializationBuff_Bloodthirst") && !player.HasStatus("SpecializationBuff_Bloodthirst"), "嗜血有持续图标，纯表现不伪造单位状态");
		Step(runtime, 5.1f); Sync(); await Wait(.02);
		Check(!fx.HasNode("SpecializationBuff_Bloodthirst"), "嗜血到期后图标同步消失");
		Hit(Enemy(), player, 100000); Sync();
		Check(runtime.DuelActive && fx.HasNode("SpecializationBuff_DeathDuel"), "死斗触发后显示独立持续提示");
		for (int i = 0; i < 3; i++) Enemy(1).ForceKill(player);
		Sync(); await Wait(.02); Check(!runtime.DuelActive && !fx.HasNode("SpecializationBuff_DeathDuel"), "死斗成功后撤去提示");
		var sprint = player.GetNode<SprintController>("Sprint"); sprint.DebugSprintOverride = true; Step(runtime, .01f); Sync();
		Check(fx.HasNode("SpecializationBuff_WindArmor"), "奔跑期间风凯有临时护甲提示");
		sprint.DebugSprintOverride = false; Step(runtime, .01f); Sync(); await Wait(.02);
		Check(!fx.HasNode("SpecializationBuff_WindArmor"), "停止奔跑后风凯提示清除");
		player.RemoveChild(runtime); runtime.QueueFree(); Sync(); await Wait(.02);
		Check(fx.VisualCount == 0, "移除特化规则后临时提示全部回收");
	}
	private async Task ThunderVisual()
	{
		var player = await RealPlayer(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		var runtime = Grant(player, "evo_thunder_pool", "mut_thunder_mark");
		var enemy = ActualChaser(new Vector3(2, .05f, 0)); enemy.Movement!.SetPhysicsProcess(false);
		Hit(player, enemy, 1); Step(runtime, .01f);
		var skill = Ability.FindByAbilityId(player, "skl_b_skill_2")!;
		Check(skill.TryPress(), "雷池可见性使用实际角色技能2触发");
		var field = Fields(runtime).Single(f => f.Kind == K.ThunderPool); field.SetPhysicsProcess(false); field._PhysicsProcess(.1);
		var camera = new Camera3D { Position = new Vector3(9, 9, 12), Current = true }; AddChild(camera); camera.LookAt(new Vector3(0, 1, 0));
		RuneCarry.Set("rune_lei_seal");
		var hud = RuntimeResources.Load<PackedScene>("res://Scenes/UI/Hud.tscn").Instantiate<PlayerHud>(); AddChild(hud);
		await Wait(.18);
		Check(field.GetNode<MeshInstance3D>("ThunderBoundary").Visible && enemy.HasStatus("ThunderMark"), "同画面保留雷池发光边界与正式敌人的引雷图标");
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path("res://Logs/evidence/adventure-thunder-rune.png")) == Error.Ok, "雷池、引雷与携带符文HUD真实渲染截图");
		camera.QueueFree(); hud.QueueFree();
	}
	private async Task RealAbilities()
	{
		var hunter = await RealPlayer(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow); var pool = Grant(hunter, "evo_thunder_pool");
		var skill = Ability.FindByAbilityId(hunter, "skl_a1_skill_2")!;
		Check(skill.TryPress(), "真实猎手角色技能2成功释放"); Check(Fields(pool).Count(f => f.Kind == K.ThunderPool) == 1, "雷池订阅实际技能释放而非全局任意按键");
		Vector3 origin = hunter.GlobalPosition; var enemy = Enemy(x: origin.X + 2, z: origin.Z); Pulse(pool, 1.01f); Check(enemy.Health < 1000, "雷池周期性降下天雷");
		await Clear();
		RunController.TryResolveCharacter("chr_weapon_master", out var master); RunController.TryResolveWeapon("wpn_scythe", out var scytheKind);
		var player = await RealPlayer(master, scytheKind); var runtime = Grant(player, "evo_thunder_recall");
		var scythe = Ability.FindByAbilityId(player, "skl_b2_w1_2") as ScytheThrowAbility;
		Check(scythe != null && scythe.TryPress(), "B2真实无情开始出手");
		for (int i = 0; i < 40 && scythe?.InFlight != true; i++) await Wait(0.05);
		Check(scythe?.InFlight == true, "无情真实动画事件后离手");
		if (scythe == null) return;
		Near(scythe.WeaponVelocity.Length(), 36, "落雷术飞行速度18×2");
		await Wait(0.35); Vector3 weaponPosition = scythe.WeaponPosition;
		var landingVictim = Enemy(10000); landingVictim.GlobalPosition = weaponPosition + Vector3.Right * 0.5f; await Wait();
		var fall = (FallAttackAbility)Ability.FindByAbilityId(player, "skl_b_fall")!;
		player.Stats.SetBase("BaseCritChance", 0f);
		float healthBefore = landingVictim.Health;
		var landingHits = new List<HitInstance>(); landingVictim.Damaged += (_, hit) => landingHits.Add(hit);
		int landed = 0; fall.Landed += () => landed++;
		Check(scythe.TryPress(), "无情第二次按键被落雷术接管"); Check(player.GlobalPosition.DistanceTo(weaponPosition) < 0.01f, "角色瞬移到武器中心");
		Check(landed == 1 && fall.HasLanded && fall.LastDamageBonus == 0, "立即派发一次100%倍率落地攻击");
		foreach (var hit in landingHits) GD.Print($"[Specialization] 落地命中：{hit.Attack?.Ability} damage={hit.Damage} base={hit.Attack?.BaseDamage} crit={hit.Critical} chance={hit.Attack?.CritChance}");
		Check(landingHits.Count(h => h.Attack?.AbilityKind == fall.AbilityId) == 1, "落地命中体只结算一次下落伤害");
		var landingHit = landingHits.Single(h => h.Attack?.AbilityKind == fall.AbilityId);
		Near(landingHit.Attack!.BaseDamage, fall.LastDamage, "落地使用100%倍率攻击快照，独立保留暴击结算");
		Near(healthBefore - landingVictim.Health, landingHits.Sum(h => h.Damage), "落地实际扣血与命中伤害总和一致");
		await Wait(0.15); Check(!scythe.InFlight, "接触武器后真实回手解除锁门");
		await Clear();
		player = await RealPlayer(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm); runtime = Grant(player, "evo_wind_domain");
		Step(runtime, .01f); Check(!Fields(runtime).Any(f => f.Kind == K.WindDomain && f.Active), "068地面静止不凭空生成浮空旋风，风之领域不再依赖镰刀");
		// 077（2026-11-01 口径 A）：浮空时长 +50%、每次跳跃给 1 秒浮空且 3 秒内不重复。
		var domainHover = player.GetNode<AirHoverController>("AirHover");
		Near(domainHover.DurationScale, 1.5f, "风之领域把角色获得的浮空时长倍率设为 1.5");
		domainHover.Grant(2f); Near(domainHover.HoverRemaining, 3f, "授予 2 秒浮空实际得到 3 秒（+50%）");
		domainHover.ResetForLoadout(); Near(domainHover.HoverRemaining, 0f, "换装复位清空浮空");
		var motion = player.Movement!;
		Check(motion.TryJump(), "自测直接触发起跳事件");
		Near(domainHover.HoverRemaining, 1.5f, "跳跃授予 1 秒浮空、经倍率后为 1.5 秒");
		Check(runtime.Behavior<WindDomainBehavior>(K.WindDomain)!.JumpGrantCount == 1, "跳跃浮空已授予 1 次");
		motion.TryJump(); Step(runtime, .2f); Check(runtime.Behavior<WindDomainBehavior>(K.WindDomain)!.JumpGrantCount == 1, "3 秒内再次跳跃不重复施加");
		Step(runtime, 3.1f); motion.TryJump(); Check(runtime.Behavior<WindDomainBehavior>(K.WindDomain)!.JumpGrantCount == 2, "超过 3 秒后再次跳跃重新施加");
		await Clear();
		player = await RealPlayer(master, scytheKind); runtime = Grant(player, "evo_thunder_pool");
		var masterSkill = Ability.FindByAbilityId(player, "skl_b_skill_2")!;
		Check(masterSkill.TryPress() && Fields(runtime).Count(f => f.Kind == K.ThunderPool) == 1, "武器大师实际角色技能2同样触发雷池");
		masterSkill.TryPress(); Check(Fields(runtime).Count(f => f.Kind == K.ThunderPool) == 1, "角色技能被忙碌／冷却拒绝时不凭空多生成雷池");
	}
}
