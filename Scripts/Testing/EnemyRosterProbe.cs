using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.World;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Testing;

/// <summary>真实场景、动画事件与物理验收；每种敌人隔离测试，避免互相抢目标。</summary>
public partial class EnemyRosterProbe : Node3D
{
	private int _pass, _fail;
	private readonly List<Unit> _units = new();
	private MapManager _map = null!;
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[Roster] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Wait(int frames) { for (int i = 0; i < frames; i++) await Frame(); }
	private Unit Target(float distance = 6)
	{
		var unit = new Unit { Faction = Faction.Player, MaxHealth = 10000 };
		unit.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .45f, Height = 1.8f } });
		AddChild(unit); unit.GlobalPosition = new Vector3(0, .05f, -distance); _units.Add(unit); return unit;
	}
	private Unit Enemy(string key, bool brain = false)
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_" + key)!;
		var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit);
		AddChild(unit); unit.GlobalPosition = Vector3.Up * .05f;
		unit.GetNode("Brain").SetPhysicsProcess(brain); _units.Add(unit); return unit;
	}
	private async Task Clear()
	{
		foreach (var unit in _units) if (IsInstanceValid(unit)) unit.QueueFree(); _units.Clear();
		foreach (var node in GetTree().GetNodesInGroup("projectiles")) node.QueueFree();
		await Wait(3);
	}
	private HitInstance Hit(Unit target, Unit attacker, float damage, bool negated = false) => new()
	{
		Target = target, Attacker = attacker, Damage = damage, Direction = Vector3.Back,
		Position = target.GlobalPosition, Context = EventContext.Create(attacker, target), Negated = negated,
	};
	public override async void _Ready()
	{
		try { await Run(); }
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Roster] === 敌人清单验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Run()
	{
		var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) } }); AddChild(floor);
		_map = new MapManager { BakeDelaySeconds = .1f }; AddChild(_map);
		for (int i = 0; i < 120 && !_map.IsReady; i++) await Frame();
		Check(_map.IsReady, "导航地图真实烘焙同步");
		Check(ConfigIndex.Settings.Enemies.Length == 21, "生产索引21行：全局1、旧敌种9、新敌种11");
		var encounter = new EncounterController { AutoStart = false }; AddChild(encounter);
		encounter.ApplyEntryFilter(new StageDefinition());
		Check(encounter.EntryCount == 19 && encounter.AllowedEntrySummary.Contains("enemy_combo_fighter(成本4)"), $"无限制关卡自动接入19个随机敌种：{encounter.AllowedEntrySummary}");
		encounter.ApplyEntryFilter(new StageDefinition());
		Check(encounter.EntryCount == 19, "重复应用白名单不重复注册生成项");
		encounter.QueueFree(); await Wait(2);
		string[] keys = { "burrow_hunter", "spear_guard", "arc_thrower", "meteor_mage", "tracking_charger", "hover_diver", "heavy_slammer", "blade_spinner", "bomb_lobber", "arc_bomber", "combo_fighter" };
		int[] costs = { 5, 6, 4, 4, 5, 4, 3, 3, 3, 6, 4 };
		for (int i = 0; i < keys.Length; i++)
		{
			Check(ConfigRows.ById<EnemyConfig>("enemy_" + keys[i])!.SpawnResourceCost == costs[i], $"{keys[i]}按行为压力计费={costs[i]}");
			await Plan(keys[i]); await Clear();
		}
		await NearBranches(); await Clear();
		await Reactions(); await Clear();
		await GuardSkill(); await Clear();
		await Suicide(); await Clear();
		await Distances(); await Clear();
		await SpawnBudget(); await Clear();
	}
	private async Task Plan(string key)
	{
		float distance = key == "heavy_slammer" ? 2 : key is "blade_spinner" or "combo_fighter" ? 3 : 6;
		var target = Target(distance); var enemy = Enemy(key); await Wait(4);
		var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); var presentation = enemy.GetNode<UnitPresentation>("Presentation");
		int hits = 0; target.Damaged += (_, _) => hits++;
		Check(ability.TryActivate(target), $"{key}通过真实动画存在性闸门起手");
		var seen = new HashSet<Kind>(); bool projectileSeen = false, arc = false, down = false, guardedCharge = false;
		bool homing = false, hovered = false, movedTarget = false, turned = false; float highest = 0, displacement = 0, spin = 0;
		Vector3 start = enemy.GlobalPosition;
		for (int f = 0; f < 420 && ability.IsBusy; f++)
		{
			seen.Add(ability.CurrentKind); highest = Mathf.Max(highest, enemy.GlobalPosition.Y);
			if (key == "tracking_charger" && ability.CurrentKind == Kind.HomingCharge && ability.ExecutedAttackCount > 0)
			{
				if (!movedTarget) { target.GlobalPosition += Vector3.Right * 2; movedTarget = true; }
				turned |= enemy.Velocity.X > .5f;
			}
			displacement = Mathf.Max(displacement, enemy.GlobalPosition.DistanceTo(start));
			spin = Mathf.Max(spin, Mathf.Abs(enemy.GetNode<Node3D>("Model").Rotation.Y));
			if (ability.CurrentKind == Kind.Hover && enemy.GlobalPosition.Y > 1 && enemy.Velocity.Length() < .1f) hovered = true;
			if (ability.CurrentKind == Kind.Charge && enemy.Block!.Blocking) guardedCharge = true;
			foreach (Node node in GetTree().GetNodesInGroup("projectiles"))
				if (node is Projectile p && p.CurrentAttack?.Attacker == enemy)
				{
					projectileSeen = true; arc |= p.GravityScale > 0 && p.VelocityDirection.Y > .1f;
					down |= p.GlobalPosition.Y > 4 && p.VelocityDirection.Y < -.8f;
					homing |= p.SeekAnyHostile && p.HomingTurnRateDegrees > 0;
				}
			await Frame();
		}
		if (key is "meteor_mage" or "arc_thrower" or "bomb_lobber" or "arc_bomber") await Wait(90);
		Check(!ability.IsBusy && ability.ExecutedAttackCount >= 1, $"{key}等待真实ATK并完整收招，次数={ability.ExecutedAttackCount}");
		Check(presentation.AttackWarningCount == ability.ExecutedAttackCount, $"{key}每段攻击起手闪身一次，举盾和等待不误闪");
		Check(!enemy.Block!.Blocking && !enemy.Movement!.HasVelocityOverride, $"{key}收尾归还格挡和速度所有权");
		switch (key)
		{
			case "burrow_hunter":
				Check(projectileSeen && homing, "地鼠发射转向率受限的追踪弹");
				Check(seen.Contains(Kind.Wait) && seen.Contains(Kind.Burrow) && displacement > 3 && _map.HasBodyClearance(enemy.GlobalPosition, enemy), "攻击后等待、遁地，并在导航净空点出土"); break;
			case "spear_guard": Check(seen.Contains(Kind.Guard) && guardedCharge && displacement > 2, "矛盾先举盾，远处冲锋持续正面格挡"); break;
			case "arc_thrower": Check(projectileSeen && arc, "投掷兵真实抛物线飞行"); break;
			case "meteor_mage": Check(projectileSeen && down && hits == 1, $"法师蓄力后在目标上方下砸，爆炸仅伤一次 hits={hits}"); break;
			case "tracking_charger": Check(turned && seen.Contains(Kind.HomingCharge) && seen.Contains(Kind.Melee) && ability.ExecutedAttackCount == 2 && displacement > 2, "追踪冲锋真实转向移动后的目标，结束后仅接一次近战"); break;
			case "hover_diver": Check(highest > 1.5f && hovered && seen.Contains(Kind.Dive) && enemy.GlobalPosition.Y < .3f, $"跳起、真实滞空、俯冲落地 maxY={highest:0.##}"); break;
			case "heavy_slammer": Check(hits == 1 && enemy.Movement!.Mass >= 3, $"重击范围伤害一次、重型质量 hits={hits}"); break;
			case "blade_spinner": Check(displacement > 1 && spin > 1 && hits == 1, $"持刀突进大回旋、同段只命中一次 hits={hits}"); break;
			case "bomb_lobber": Check(projectileSeen && hits == 1, $"直投炸弹接触爆炸，不叠直击伤害 hits={hits}"); break;
			case "arc_bomber": Check(projectileSeen && arc && hits == 1, $"抛物线炸弹落地爆炸一次 hits={hits}"); break;
			case "combo_fighter": Check(ability.ExecutedAttackCount == 3 && hits >= 2 && displacement > 1, $"三段真实ATK连招，各段突进 hits={hits}"); break;
		}
	}
	private async Task NearBranches()
	{
		foreach (string key in new[] { "spear_guard", "arc_thrower", "arc_bomber" })
		{
			var target = Target(1.5f); var enemy = Enemy(key); await Wait(4);
			var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); ability.TryActivate(target);
			bool melee = false, hop = false, exposed = false; float highest = 0;
			for (int i = 0; i < 240 && ability.IsBusy; i++)
			{
				melee |= ability.CurrentKind == Kind.Melee; hop |= ability.CurrentKind == Kind.BackHop;
				exposed |= ability.CurrentKind == Kind.Melee && !enemy.Block!.Blocking;
				highest = Mathf.Max(highest, enemy.GlobalPosition.Y); await Frame();
			}
			Check(melee && exposed && !ability.IsBusy, $"{key}近身走近战分支，近战不保留格挡");
			if (key != "spear_guard") Check(hop && highest > .15f && _map.TryPath(enemy.GlobalPosition, target.GlobalPosition, out _, enemy), $"{key}近战后跳，落点仍有导航连通路径");
			await Clear();
		}
	}
	private async Task Reactions()
	{
		var target = Target(2); var enemy = Enemy("heavy_slammer"); await Wait(4);
		var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); ability.Cooldown = 0;
		ability.TryActivate(target); await Wait(6); enemy.ReceiveHit(Hit(enemy, target, 1));
		Check(!ability.IsBusy && enemy.Reactions!.IsStaggered && enemy.Reactions.HitStunCount == 1, "无甲前摇被普通命中打断并进入受击硬直");
		await Wait(20); enemy.GrantArmor(5); ability.TryActivate(target); await Wait(6);
		enemy.ReceiveHit(Hit(enemy, target, 20));
		Check(enemy.Armor == 0 && ability.IsBusy && enemy.Reactions!.HitStunCount == 1, "破甲当击按命中前有甲处理，攻击前摇继续");
		await Wait(2); enemy.ReceiveHit(Hit(enemy, target, 1));
		Check(!ability.IsBusy && enemy.Reactions!.HitStunCount == 2, "护甲破碎后下一击可打断");
		await Wait(20);
		enemy.Block!.Blocking = true; enemy.Rotation = Vector3.Zero;
		int attackerStuns = target.Reactions!.BlockStunCount;
		enemy.ReceiveHit(Hit(enemy, target, 5));
		Check(enemy.Reactions!.BlockStunCount == 1 && enemy.Reactions.IsStaggered && target.Reactions.BlockStunCount == attackerStuns, "持盾格挡硬直属于格挡者，攻击者不受此硬直");
		await Wait(20); enemy.Block.Blocking = false;
		int before = enemy.Reactions.HitStunCount; enemy.ReceiveHit(Hit(enemy, target, 9, true));
		Check(!enemy.Reactions.IsStaggered && enemy.Reactions.HitStunCount == before, "一次护罩无效化本次命中，无受击或格挡硬直");
		var presentation = enemy.GetNode<UnitPresentation>("Presentation"); var mesh = enemy.GetNode<MeshInstance3D>("Model");
		// J38护罩也有短闪；先让该瞬态结束，再记录实际精英底材用于恢复比较。
		enemy.AddChild(new EliteModifier()); await Wait(9);
		Material? eliteBase = mesh.MaterialOverride;
		Check(enemy.Scale.X > 1 && enemy.GetNode<MeshInstance3D>("ArmorAura").Visible, "精英体型放大、有甲额外外壳可见");
		presentation.FlashAttack(); await Wait(12);
		Check(mesh.MaterialOverride == eliteBase, "攻击闪色结束后恢复精英底材");
		enemy.ReceiveHit(Hit(enemy, target, 1000)); await Wait(3);
		Check(!enemy.GetNode<MeshInstance3D>("ArmorAura").Visible, "破甲或死亡后外壳隐藏");
	}
	private async Task GuardSkill()
	{
		var enemy = Enemy("heavy_slammer"); enemy.GlobalPosition = new Vector3(0, .05f, -3);
		var player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(player); _units.Add(player);
		player.GlobalPosition = Vector3.Up * .05f;
		var rig = player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		new ActRoguelike.Player.PlayerLoadout().Apply(player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		await Wait(20);
		var guard = player.GetNode<GuardStanceAbility>("AbilityBSkill2");
		Check(guard.TryActivate(enemy), "玩家主动架势通过真实技能动画起手");
		for (int i = 0; i < 60 && !guard.StanceActive; i++) await Frame();
		float armor = player.Armor, health = player.Health;
		player.ReceiveHit(Hit(player, enemy, 12));
		Check(guard.ParryCount == 1 && !guard.IsBusy && player.Armor == armor && player.Health == health && player.Reactions!.BlockStunCount == 1 && player.Reactions.IsStaggered, "真实GuardOn后挡伤、退出架势，并只给玩家自身格挡硬直");
	}
	private async Task Suicide()
	{
		var target = Target(1.5f); var enemy = Enemy("exploder"); await Wait(4);
		var ability = enemy.GetNode<AreaAttackAbility>("AbilityExplode");
		Check(ability.Windup >= .9f && ability.TryActivate(target), "自爆前摇至少0.9秒且闪身预警");
		await Wait(24); Check(!enemy.IsDead && target.Health == target.MaxHealth, "前摇期间不提前自爆或造成伤害");
		enemy.ReceiveHit(Hit(enemy, target, 1)); await Wait(60);
		Check(!enemy.IsDead && !ability.IsBusy && target.Health == target.MaxHealth, "无甲自爆在前摇被打断，取消爆炸命中体");
		await Clear(); target = Target(1.5f); enemy = Enemy("exploder"); await Wait(4);
		ability = enemy.GetNode<AreaAttackAbility>("AbilityExplode"); ability.TryActivate(target); await Wait(75);
		Check(enemy.IsDead && target.Health < target.MaxHealth, "未被打断则前摇后真实爆炸伤害并死亡");
	}
	private async Task Distances()
	{
		var target = Target(20); var enemy = Enemy("bomb_lobber"); await Wait(3);
		var p = new Projectile { Speed = 12, MaxTravelDistance = 2, MaxLifetime = 5, DistanceEnd = Projectile.DistanceEndMode.Drop };
		AddChild(p); p.GlobalPosition = new Vector3(8, 4, 0); p.Launch(new AttackInstance { Attacker = enemy, BaseDamage = 1 }, Vector3.Right);
		await Wait(20);
		Check(IsInstanceValid(p) && !p.IsQueuedForDeletion() && p.VelocityDirection.Y < -.9f && p.GlobalPosition.X <= 10.01f, "固定航程后停止追踪，垂直掉落");
		p.QueueFree();
		var bomb = new Projectile { Speed = 12, MaxTravelDistance = 2, DistanceEnd = Projectile.DistanceEndMode.Explode, ExplosionOnly = true };
		AddChild(bomb); bomb.GlobalPosition = new Vector3(0, 1, -16); bomb.Launch(new AttackInstance { Attacker = enemy, ExplosionDamage = 7, ExplosionRadius = 3, BaseDamage = 0 }, Vector3.Forward);
		int count = 0; target.Damaged += (_, _) => count++;
		await Wait(35); Check(count == 1, $"炸弹达到固定航程触发一次范围爆炸 hits={count}");
	}
	private async Task SpawnBudget()
	{
		var target = Target(0); target.IsInvulnerable = true;
		var encounter = new EncounterController { AutoStart = false, WaveMode = false, TotalBudget = 10, BudgetCap = 10, SpawnInterval = .1f, MinSpawnDistance = 6, SpawnRadius = 9 };
		AddChild(encounter); encounter.ApplyEntryFilter(new StageDefinition { AllowedEnemyIds = new[] { "enemy_combo_fighter" } }); encounter.Start();
		// 生产解锁门槛为20秒；只推进场景时间，不改表和预算公式。
		for (int i = 0; i < 210; i++) { encounter._PhysicsProcess(.1); await Frame(); }
		Check(encounter.TotalSpawned == 2 && encounter.RemainingBudget == 0, $"生成连招敌人每只4点、余2不足作废，真实生成={encounter.TotalSpawned}");
		foreach (Node child in encounter.GetChildren()) if (child is Unit unit)
			Check(unit.GetNode<EnemyPlanAbility>("AbilityPlan").Plan != null && unit.GetNodeOrNull<ActRoguelike.AI.UnitNavigation>("Navigation") != null, "生产生成的新敌人带动作计划和导航");
		encounter.QueueFree();
	}
}
