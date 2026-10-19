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
		try
		{
			RuneCarry.UseInMemoryStoreForSelfTest(); RunSetup.HasSelection = true;
			await Wait(0.2);
			Catalogue(); await Clear();
			Thunder(); await Clear();
			await War(); await Clear();
			await Wind(); await Clear();
			await RealAbilities(); await Clear();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Specialization] === 特化验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
#endif
	}
	private void Catalogue()
	{
		var rows = ConfigIndex.Settings.Upgrades.Where(r => r.Specialization != K.None).ToArray();
		Check(rows.Length == 23 && rows.Select(r => r.Specialization).Distinct().Count() == 23, "23项特化全部进生产索引且类型唯一");
		Check(RewardCatalog.All.Length == 91 && RewardCatalog.Find("mut_chain") == null && RewardCatalog.Find("mut_sunset_sword") == null && RewardCatalog.Find("mut_homing_missile") == null, "旧三项退出生产目录，J33补快乐小子后91");
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
				var expected = rows.Where(r => r.Requires.Length == 0 || r.Requires.Contains(token)).Where(r => r.Specialization is not (K.ThunderRecall or K.WindDomain) || b2).Select(r => r.Id).Order().ToArray();
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
		Near(heart.Stats.Get("DamageReductionAdd"), 0, "战斗之心将后获得的三档坚硬全部转走"); Near(heart.Stats.Get("CritMultiplierAdd"), 0.6f, "5%＋10%＋15%坚硬按1:2转60%爆伤");
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
			await Wait(0.15); GetViewport().GetTexture().GetImage().SavePng("res://docs/character-design-j32-wind.png");
			Check(true, "风系区域真实渲染截图"); camera.QueueFree();
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
		player = await RealPlayer(master, scytheKind); runtime = Grant(player, "evo_wind_domain"); scythe = (ScytheThrowAbility)Ability.FindByAbilityId(player, "skl_b2_w1_2")!;
		Check(scythe.TryPress(), "风之领域无情开始出手");
		for (int i = 0; i < 60 && !scythe.HasSettled; i++) await Wait(0.05);
		Step(runtime, 0.01f); var domain = Fields(runtime).Single(f => f.Kind == K.WindDomain);
		Check(scythe.HasSettled && domain.Active, "武器停止后才产生领域"); Near(domain.Radius, 5, "领域大于普通旋风");
		Grant(player, "upg_wild_wind"); Near(domain.Radius, 7.5f, "狂风覆盖无情领域"); scythe.TryPress(); Step(runtime, 0.01f); Check(!domain.Active, "武器回程时领域停止伤害");
		await Clear();
		player = await RealPlayer(master, scytheKind); runtime = Grant(player, "evo_thunder_pool");
		var masterSkill = Ability.FindByAbilityId(player, "skl_b_skill_2")!;
		Check(masterSkill.TryPress() && Fields(runtime).Count(f => f.Kind == K.ThunderPool) == 1, "武器大师实际角色技能2同样触发雷池");
		masterSkill.TryPress(); Check(Fields(runtime).Count(f => f.Kind == K.ThunderPool) == 1, "角色技能被忙碌／冷却拒绝时不凭空多生成雷池");
	}
}
