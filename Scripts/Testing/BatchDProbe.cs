using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.AI;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>D批专用真实物理探针；不创建存档，不注入模拟命中事件。</summary>
public partial class BatchDProbe : Node3D
{
	private int _pass;
	private int _fail;
	private Unit _player = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly List<Unit> _fixtures = new();
	private readonly string[] _enemies = { "Chaser", "FastMelee", "Guard", "RangedEnemy", "Charger", "Exploder", "Screamer", "JumpingMelee", "SkillElite", "Boss" };

	public override async void _Ready()
	{
		try { await Run(); } catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[BatchD] === D批验收结束：通过 {_pass}，失败 {_fail} ===");
		ActRoguelike.Core.GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string message)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[BatchD] {(ok ? "PASS" : "FAIL")} {message}");
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
	}
	private Unit Spawn(string path, Vector3 position, Faction? faction = null, bool frozen = true)
	{
		Unit unit = RuntimeResources.Load<PackedScene>(path).Instantiate<Unit>();
		if (faction.HasValue) unit.Faction = faction.Value;
		UnitAnimator.Ensure(unit);
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.ResetPhysicsInterpolation();
		unit.ApplyBaseStats(10000, 0, 0, unit.MoveSpeed);
		unit.KnockbackResistance = 1;
		if (frozen)
		{
			// 停AI与表现，仍保留真实物理体；默认Remove会把目标移出物理世界。
			unit.DisableMode = CollisionObject3D.DisableModeEnum.KeepActive;
			unit.ProcessMode = ProcessModeEnum.Disabled;
		}
		_fixtures.Add(unit);
		return unit;
	}
	private void Place(Vector3 position, Vector3? facing = null)
	{
		_player.Movement!.EndDash();
		_player.Movement.Stop();
		_player.GlobalPosition = position;
		_player.Velocity = Vector3.Zero;
		_player.Movement.FaceMovementDirection = false;
		_player.Rotation = Vector3.Zero;
		if (facing is { } direction) _player.Movement.FaceDirection(direction, 10);
		_player.ResetPhysicsInterpolation();
	}
	private void View(Vector3 point)
	{
		var rig = _player.GetNode<Node3D>("CameraRig");
		rig.SetPhysicsProcess(false);
		rig.SetProcessInput(false);
		Camera3D camera = GetViewport().GetCamera3D();
		camera.GlobalPosition = point + new Vector3(5, 4, 7);
		camera.LookAt(point + Vector3.Up);
		rig.ResetPhysicsInterpolation();
		camera.ResetPhysicsInterpolation();
	}
	private async Task Clear()
	{
		foreach (Unit unit in _fixtures) if (IsInstanceValid(unit)) unit.QueueFree();
		_fixtures.Clear();
		foreach (Node projectile in GetTree().GetNodesInGroup("projectiles")) projectile.QueueFree();
		await Wait(0.12);
	}
	private static float Vitals(Unit unit) => unit.Health + unit.Armor + unit.Shield;
	private async Task Run()
	{
		await Wait(0.3);
		RunSetup.Character = PlayerCharacterKind.WeaponMaster;
		RunSetup.Weapon = WeaponPresetKind.SpiritSword;
		RunSetup.HasSelection = true;
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		AddChild(_player);
		_player.SetPhysicsProcess(false);
		_player.GetNode("AbilityInput").ProcessMode = ProcessModeEnum.Disabled;
		_player.GetNode("InteractInput").ProcessMode = ProcessModeEnum.Disabled;
		_player.GetNode("WeaponMount/Pistol/WeaponInput").ProcessMode = ProcessModeEnum.Disabled;
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		_player.IsInvulnerable = true;
		Place(new Vector3(0, 0.05f, 4));
		View(_player.GlobalPosition);
		await Wait(0.3);
		string[] names = { "World", "PlayerUnit", "EnemyUnit", "PlayerHitbox", "EnemyHitbox", "Projectile", "Interactable", "Pickup", "VfxOnly" };
		for (int i = 0; i < names.Length; i++) Check(ProjectSettings.GetSetting($"layer_names/3d_physics/layer_{i + 1}").AsString() == names[i], $"层{i + 1}命名={names[i]}");
		Check(_player.CollisionLayer == CombatLayers.Player && _player.CollisionMask == CombatLayers.World && _player.Movement!.IsGrounded, "玩家身体2/1且真实落地");
		Check(_player.GetNode<SpringArm3D>("CameraRig/SpringArm").CollisionMask == CombatLayers.World, "镜头遮挡只检测地形");

		// 贴身体内仍可命中：使用所有真实敌人近战盒，不扩大表内范围。
		foreach (string name in _enemies)
		{
			Unit enemy = Spawn($"res://Scenes/Enemies/{name}.tscn", new Vector3(0, 0.05f, 4));
			Place(enemy.GlobalPosition);
			await Wait(0.12);
			Check(enemy.CollisionLayer == CombatLayers.Enemy && enemy.CollisionMask == CombatLayers.World, $"{name}身体4/1");
			var boxes = new List<AttackHitbox>();
			NodeSearch.CollectInSubtree(enemy, boxes);
			_player.IsInvulnerable = false;
			float before = Vitals(_player);
			foreach (AttackHitbox box in boxes)
			{
				box.ProcessMode = ProcessModeEnum.Always;
				box.Activate(enemy, new AttackInstance { Attacker = enemy, BaseDamage = 1, Knockback = 0 });
			}
			await Wait(0.1);
			if (boxes.Count > 0) Check(Vitals(_player) < before && boxes.TrueForAll(x => x.CollisionLayer == CombatLayers.EnemyHitbox && x.CollisionMask == CombatLayers.Player), $"{name}身体重叠时攻击盒仍命中玩家、层16/2");
			foreach (AttackHitbox box in boxes) box.Deactivate();
			_player.IsInvulnerable = true;
			_player.FillVitals();
			await Clear();
		}

		foreach (Faction faction in new[] { Faction.Player, Faction.Enemy })
		{
			Unit body = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(2, 0.05f, 4), faction);
			Place(new Vector3(0, 0.05f, 4), Vector3.Right);
			await Wait(0.15);
			_player.Movement!.BeginDash(Vector3.Right, 6);
			await Wait(0.72);
			_player.Movement.EndDash();
			Check(_player.GlobalPosition.X > body.GlobalPosition.X + 1, $"真实MoveAndSlide穿过{faction}身体，x={_player.GlobalPosition.X:0.###}");
			await Clear();
		}
		Place(new Vector3(0, 0.05f, -12));
		_player.Movement!.BeginDash(Vector3.Forward, 25);
		await Wait(0.4);
		_player.Movement.EndDash();
		Check(_player.GlobalPosition.Z > -15.2f && _player.GlobalPosition.Z < -14.5f, $"真实高速位移被关卡墙挡住，z={_player.GlobalPosition.Z:0.###}");
		View(_player.GlobalPosition);
		await Shot("d_world_blocks");

		await PlayerSkills();
		await OtherMelee();
		await Projectiles();
		await AiAndSafety();
	}
	private async Task PlayerSkills()
	{
		// 上一项高速移动结束后等待惯性衰减，再布置严格重叠的位置。
		_player.Movement!.Stop();
		await Wait(0.6);
		Place(new Vector3(0, 0.05f, 4));
		Unit target = Spawn("res://Scenes/Training/TrainingDummy.tscn", _player.GlobalPosition);
		Unit ally = Spawn("res://Scenes/Summons/Drone.tscn", _player.GlobalPosition);
		await Wait(0.2);
		var melee = _player.GetNode<MeleeComboAbility>("AbilityWeapon4Skill1");
		float before = Vitals(target), friendly = Vitals(ally);
		Check(melee.TryActivate(), "灵剑真实技能激活");
		await Wait(0.5);
		Check(Vitals(target) < before && Vitals(ally) == friendly, "重叠灵剑真实动画事件造成伤害、同位置队友不受伤");
		View(_player.GlobalPosition);
		await Shot("d_overlap_melee");
		await Wait(0.6);
		melee.ResetForLoadout();
		_player.Stats.SetBase("MeleeLungeAdd", 4);
		Place(new Vector3(0, 0.05f, 4), Vector3.Forward);
		target.GlobalPosition = new Vector3(0, 0.05f, 2);
		ally.GlobalPosition = new Vector3(4, 0.05f, 4);
		await Wait(0.2);
		before = Vitals(target);
		Check(melee.TryActivate(), "剑突由真实灵剑动画事件启动");
		await Wait(0.65);
		Check(_player.GlobalPosition.Z < target.GlobalPosition.Z - 0.7f && Vitals(target) < before, $"剑突穿过目标且命中，z={_player.GlobalPosition.Z:0.###}");
		_player.Stats.SetBase("MeleeLungeAdd", 0);
		await Clear();
		await Wait(0.6);
		Place(new Vector3(0, 0.05f, 4), Vector3.Forward);
		target = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(0, 0.05f, 1));
		var blink = _player.GetNode<BlinkDashAbility>("AbilityBSkill1");
		blink.Aim = Ability.AimSource.Facing;
		before = Vitals(target);
		Check(blink.TryActivate(), "闪真实技能激活");
		await Wait(0.4);
		Check(_player.GlobalPosition.Z < target.GlobalPosition.Z - 1 && Vitals(target) < before && _player.CollisionMask == CombatLayers.World, $"闪穿过敌人并造成路径伤害，结束mask=1，z={_player.GlobalPosition.Z:0.###}");
		View(_player.GlobalPosition);
		await Shot("d_blink_through");
		blink.ResetForLoadout();
		Place(new Vector3(0, 0.05f, -12));
		Check(blink.TryActivate(), "闪撞墙测试激活");
		await Wait(0.4);
		Check(_player.GlobalPosition.Z > -15.2f && _player.GlobalPosition.Z < -14.5f && _player.CollisionMask == CombatLayers.World, $"闪不能穿过地形，z={_player.GlobalPosition.Z:0.###}");
		blink.ResetForLoadout();
		Check(_player.CollisionMask == CombatLayers.World && !_player.Movement!.IsDashing, "换装取消闪不残留位移或错误mask");
		await Clear();
	}
	private Projectile Fire(string name, Unit owner, Vector3 from)
	{
		Projectile projectile = RuntimeResources.Load<PackedScene>($"res://Scenes/Combat/{name}.tscn").Instantiate<Projectile>();
		AddChild(projectile);
		projectile.GlobalPosition = from;
		projectile.Speed = 60;
		projectile.Launch(new AttackInstance { Attacker = owner, BaseDamage = 10, Lifetime = 1, Knockback = 0 }, Vector3.Right);
		return projectile;
	}
	private async Task OtherMelee()
	{
		foreach (WeaponPresetKind weapon in new[] { WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
		{
			RunSetup.Weapon = weapon;
			_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, weapon);
			_player.IsInvulnerable = true;
			await Wait(0.7);
			Place(new Vector3(0, 0.05f, 4));
			Unit target = Spawn("res://Scenes/Training/TrainingDummy.tscn", _player.GlobalPosition);
			Unit ally = Spawn("res://Scenes/Summons/Drone.tscn", _player.GlobalPosition);
			await Wait(0.15);
			Ability? attack = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1");
			float before = Vitals(target), friendly = Vitals(ally);
			Check(attack?.TryActivate() == true, $"{weapon}真实武技激活");
			await Wait(0.7);
			Check(Vitals(target) < before && Vitals(ally) == friendly, $"{weapon}重叠时真实动画事件命中且友军无伤");
			attack?.ResetForLoadout();
			await Clear();
		}
		RunSetup.Weapon = WeaponPresetKind.SpiritSword;
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		_player.IsInvulnerable = true;
	}
	private async Task Projectiles()
	{
		Place(new Vector3(-15, 0.05f, 4));
		foreach (Faction faction in new[] { Faction.Player, Faction.Enemy })
		foreach (string name in new[] { "Bullet", "Arrow", "SwordQi", "EnergyWave" })
		{
			Faction hostile = faction == Faction.Player ? Faction.Enemy : Faction.Player;
			Unit owner = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(-5, 0.05f, 4), faction);
			Unit friend = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(2, 0.05f, 4), faction);
			Unit target = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(4, 0.05f, 4), hostile);
			await Wait(0.12);
			float before = Vitals(target), friendly = Vitals(friend);
			Projectile shot = Fire(name, owner, new Vector3(0, 1, 4));
			Check(shot.CollisionLayer == CombatLayers.Projectile && shot.HitMask == (CombatLayers.World | CombatLayers.HostileTo(faction)) && !shot.Monitoring, $"{faction}/{name}投射物layer32与阵营mask正确、无重叠结算");
			await Wait(0.22);
			Check(Vitals(target) < before && Vitals(friend) == friendly, $"{faction}/{name}真实弹道穿队友并命中敌人");
			await Clear();
		}
		Unit shooter = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(-5, 0.05f, 4), Faction.Player);
		Unit embedded = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(0, 0.05f, 4));
		await Wait(0.12);
		float health = Vitals(embedded);
		Fire("Bullet", shooter, embedded.GlobalPosition + Vector3.Up);
		await Wait(0.1);
		Check(Vitals(embedded) < health, "弹道起点在敌人身体内仍真实命中");
		await Clear();
		shooter = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(-5, 0.05f, -16), Faction.Player);
		Unit behindWall = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(0, 0.05f, -18));
		await Wait(0.12);
		health = Vitals(behindWall);
		Projectile blocked = Fire("Bullet", shooter, new Vector3(0, 1, -14));
		blocked.Launch(new AttackInstance { Attacker = shooter, BaseDamage = 10, Lifetime = 1 }, Vector3.Forward);
		await Wait(0.15);
		Check(!IsInstanceValid(blocked) && Vitals(behindWall) == health, "真实子弹被地形消除，墙后目标无伤");
		await Clear();
		foreach (Faction faction in new[] { Faction.Player, Faction.Enemy })
		{
			Faction hostile = faction == Faction.Player ? Faction.Enemy : Faction.Player;
			Unit owner = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(-5, 0.05f, 4), faction);
			Unit first = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(2, 0.05f, 4), hostile);
			Unit second = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(4, 0.05f, 4), hostile);
			int firstHits = 0, secondHits = 0;
			first.Damaged += (_, _) => firstHits++;
			second.Damaged += (_, _) => secondHits++;
			await Wait(0.12);
			Projectile pierce = Fire("Bullet", owner, new Vector3(0, 1, 4));
			pierce.Launch(new AttackInstance { Attacker = owner, BaseDamage = 10, Lifetime = 1, Pierce = 1 }, Vector3.Right);
			await Wait(0.25);
			Check(firstHits == 1 && secondHits == 1, $"{faction}穿透次数只消耗一次、两个目标各受击一次：{firstHits}/{secondHits}");
			await Clear();
			owner = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(-5, 0.05f, 4), faction);
			first = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(2, 0.05f, 4), hostile);
			second = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(4, 0.05f, 6), hostile);
			Unit friend = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(2.5f, 0.05f, 4), faction);
			await Wait(0.12);
			float nextHealth = Vitals(second), friendHealth = Vitals(friend);
			Projectile ricochet = Fire("Bullet", owner, new Vector3(0, 1, 4));
			ricochet.Launch(new AttackInstance { Attacker = owner, BaseDamage = 10, Lifetime = 1, Ricochet = 1 }, Vector3.Right);
			await Wait(0.3);
			Check(Vitals(second) < nextHealth && Vitals(friend) == friendHealth, $"{faction}真实弹射只选择敌对目标");
			float enemyHealth = Vitals(first);
			Explosion blast = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn").Instantiate<Explosion>();
			AddChild(blast);
			blast.GlobalPosition = first.GlobalPosition + Vector3.Up;
			blast.Detonate(owner, 5, 3, Array.Empty<string>());
			Check(blast.CollisionLayer == CombatLayers.HitboxOf(faction) && blast.CollisionMask == CombatLayers.HostileTo(faction), $"{faction}离树生成爆炸按来源配置命中层");
			await Wait(0.18);
			Check(Vitals(first) < enemyHealth && Vitals(friend) == friendHealth, $"{faction}真实爆炸仅伤敌方，友军无伤");
			await Clear();
		}
	}
	private async Task AiAndSafety()
	{
		Place(new Vector3(0, 0.05f, 4));
		foreach (string name in _enemies)
		{
			Unit enemy = Spawn($"res://Scenes/Enemies/{name}.tscn", new Vector3(0, 0.05f, -3), frozen: false);
			int activations = 0;
			var abilities = new List<Ability>();
			NodeSearch.CollectInSubtree(enemy, abilities);
			foreach (Ability ability in abilities) ability.Activated += _ => activations++;
			Vector3 start = enemy.GlobalPosition;
			await Wait(1.3);
			EnemyBrain? brain = enemy.GetNodeOrNull<EnemyBrain>("Brain");
			Check(brain?.Target == _player && (enemy.GlobalPosition.DistanceTo(start) > 0.2f || activations > 0), $"{name}真实AI索敌并移动/攻击，位移={enemy.GlobalPosition.DistanceTo(start):0.###}，施法={activations}");
			await Clear();
		}
		Unit drone = Spawn("res://Scenes/Summons/Drone.tscn", new Vector3(0, 0.05f, 1), frozen: false);
		Unit enemyTarget = Spawn("res://Scenes/Training/TrainingDummy.tscn", new Vector3(0, 0.05f, -3));
		await Wait(1.3);
		Check(drone.CollisionMask == CombatLayers.World && drone.GetNode<EnemyBrain>("Brain").Target == enemyTarget, "玩家召唤物身体只撞地形且真实索敌");
		View(_player.GlobalPosition);
		await Shot("d_ai_summon");
		string[] fxPaths = { "res://Assets/Effects/fx_combat_hit.tscn", "res://Assets/Effects/fx_combat_explosion.tscn", "res://Assets/Effects/fx_weapon_muzzle.tscn", "res://Scenes/ArtRefresh/FxLootBeacon.tscn", "res://Scenes/ArtRefresh/FxBattleSwing.tscn" };
		foreach (string path in fxPaths)
		{
			Node fx = RuntimeResources.Load<PackedScene>(path).Instantiate();
			Check(SafeFx(fx), $"纯特效子树无单位/碰撞/分组：{path.GetFile()}");
			fx.Free();
		}
		Check(SafeFx(_player.GetNode("Afterimage")) && SafeFx(_player.GetNode("AbilityFallAttack/DiveWind")), "残影与下落风线不进物理世界或单位组");
		await Clear();
		var killZone = new ActRoguelike.World.KillZone { CenterY = -30, Extent = 6 };
		AddChild(killZone);
		killZone.GlobalPosition = new Vector3(20, -30, 20);
		Unit fallen = Spawn("res://Scenes/Summons/Drone.tscn", new Vector3(20, -30, 20));
		await Wait(0.18);
		Check(fallen.IsDead && fallen.CollisionLayer == 0 && killZone.CollisionMask == CombatLayers.UnitMask, "真实区域杀仍检测召唤物，死亡身体退层");
		killZone.QueueFree();
		await Clear();
	}
	private static bool SafeFx(Node node)
	{
		if (node is Unit or CollisionObject3D || node.GetGroups().Count > 0) return false;
		foreach (Node child in node.GetChildren()) if (!SafeFx(child)) return false;
		return true;
	}
}
