using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实输入、常驻首领霸体和按实际生成成本续波；设置夹具只在内存中替换。</summary>
public partial class GameplayRulesProbe : Node3D
{
	private int _pass, _fail;
	private PlayerController _player = null!;
	private readonly List<Unit> _units = new();
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[GameplayRules] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frames(int count = 1) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private static void Call(EncounterController encounter, string method) => typeof(EncounterController).GetMethod(method, Private)!.Invoke(encounter, null);
	public override async void _Ready()
	{
		FieldInfo file = typeof(SettingsStore).GetField("_file", BindingFlags.Static | BindingFlags.NonPublic)!;
		// 先让正式设置完成懒加载，再将测试控制模式隔离在新ConfigFile里。
		_ = SettingsStore.PlayerMovementMode;
		object? previous = file.GetValue(null);
		using var fixture = new ConfigFile();
		try
		{
			file.SetValue(null, fixture);
			AddToGroup("nav_geometry");
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } }); AddChild(floor);
			var map = new MapManager { BakeDelaySeconds = .1f }; AddChild(map);
			for (int i = 0; i < 180 && !map.IsReady; i++) await Frames();
			Check(map.IsReady, "规则夹具真实导航烘焙完成");
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.Bow;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<PlayerController>(); AddChild(_player);
			_player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
			_player.GlobalPosition = new Vector3(20, .05f, 20); await Frames(5);
			await Controls(fixture);
			await BossArmor();
			await WavePressure();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { Press("move_forward", false); Press("move_right", false); file.SetValue(null, previous); }
		GD.Print($"[GameplayRules] === 玩法规则验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Controls(ConfigFile settings)
	{
		Check(SettingsStore.PlayerMovementMode == SettingsStore.MovementMode.Direct, "没有保存模式时仍用原直接操控");
		var page = new VBoxContainer();
		new ControlsSettingsSection().Build(page, null!);
		var mode = page.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault();
		Check(mode?.ItemCount == 2 && mode.Selected == 0 && mode.GetItemText(1).Contains("锚点"), "设置控制页提供直接／锚点两种模式，默认直接");
		page.Free();
		Check(InputMap.ActionGetEvents("skill_1").OfType<InputEventKey>().Any(key => key.PhysicalKeycode == Key.Q || key.Keycode == Key.Q), "技能1实际InputMap绑定Q");
		settings.SetValue(SettingsStore.ControlsSection, SettingsStore.KeyMovementMode, 1);
		Press("move_forward", true); await Frames(18);
		Check(_player.HasMovementAnchor && _player.Movement!.PlanarVelocity.Length() > 3, "真实W输入通过锚点推进角色");
		Check(Mathf.Abs(_player.GlobalPosition.DistanceTo(_player.MovementAnchorPosition) - _player.MovementAnchorRadius) < .001f, "锚点始终保持固定归一化半径");
		Vector3 forward = _player.Movement!.IntentDirection;
		Press("move_right", true); await Frames(18);
		Check(Mathf.Abs(_player.Movement.IntentDirection.Length() - 1) < .001f && _player.Movement.IntentDirection.Dot(forward) < .9f, "斜向输入沿单位圆改变方向，不叠加两轴速度");
		Press("move_forward", false); Press("move_right", false); await Frames(12);
		Check(!_player.HasMovementAnchor && _player.Movement.PlanarVelocity.Length() < .01f, "松键停止锚点追随，不继续跑向历史世界坐标");
		settings.SetValue(SettingsStore.ControlsSection, SettingsStore.KeyMovementMode, 0);
		Press("move_right", true); await Frames(12);
		Check(!_player.HasMovementAnchor && _player.Movement.PlanarVelocity.Length() > 3, "切回直接模式后原移动链路继续工作");
		Press("move_right", false); await Frames(10);
		var backdash = _player.GetNode<BackdashAbility>("AbilitySkill1");
		Check(backdash.BackwardDistance == 12 && backdash.TravelSeconds == .3f && _player.GetNode<AfterimageTrail>("Afterimage").GhostStartAlpha == .25f, "066后撤预设12米/0.3秒，残影75%透明");
	}
	private Unit Enemy(bool boss, Vector3 position)
	{
		EnemyConfig row = ConfigRows.ById<EnemyConfig>("enemy_heavy_slammer")!;
		var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit);
		if (boss) unit.Tags = unit.Tags.Append("Boss").ToArray();
		AddChild(unit); unit.GlobalPosition = position; unit.GetNode("Brain").SetPhysicsProcess(false);
		_units.Add(unit); return unit;
	}
	private async Task BossArmor()
	{
		_player.GlobalPosition = new Vector3(-15, .05f, -2); _player.Velocity = Vector3.Zero;
		Unit ordinary = Enemy(false, new Vector3(-15, .05f, 0));
		Unit boss = Enemy(true, new Vector3(-13.8f, .05f, 0)); await Frames(8);
		var normalSkill = ordinary.GetNode<EnemyPlanAbility>("AbilityPlan");
		var bossSkill = boss.GetNode<EnemyPlanAbility>("AbilityPlan");
		Check(normalSkill.TryActivate(_player) && bossSkill.TryActivate(_player), "普通／首领对照均实际进入重击技能");
		await Frames(6);
		float before = boss.Health;
		HitInstance Hit(Unit target) => new() { Target = target, Attacker = _player, Damage = 1, Direction = Vector3.Back, Position = target.GlobalPosition, Context = EventContext.Create(_player, target) };
		ordinary.ReceiveHit(Hit(ordinary)); boss.ReceiveHit(Hit(boss));
		Check(!normalSkill.IsBusy && ordinary.Reactions!.HitStunCount == 1, "无甲普通敌人被命中打断前摇");
		Check(boss.Health < before && bossSkill.IsBusy && boss.Reactions!.HitStunCount == 0 && boss.Movement!.IsControlImmune, "首领无甲仍结算伤害，技能和霸体贯穿前摇");
		bossSkill.InterruptByControl(); boss.Reactions!.BlockRecoil(.3f);
		Check(bossSkill.IsBusy && !boss.Reactions.IsStaggered && boss.Reactions.BlockStunCount == 0, "显式硬控和格挡者硬直都不能打断首领");
		Check(!boss.ReceiveKnockback(Vector3.Right * 10) && !boss.ReceiveForce(this, Vector3.Right * 20, 1), "首领拒绝战斗击退和持续战斗力");
		boss.Movement!.SetForce(this, Vector3.Right, .1f); await Frames(2);
		Check(boss.Movement.ExternalVelocity.X > 0, "环境物理力仍有明确独立入口");
		foreach (Unit unit in _units) unit.QueueFree(); _units.Clear(); await Frames(3);
	}
	private async Task WavePressure()
	{
		_player.GlobalPosition = new Vector3(0, .05f, 0); _player.Velocity = Vector3.Zero; await Frames(3);
		var encounter = new EncounterController { AutoStart = false, TotalBudget = 100, BudgetCap = 100, SpawnRadius = 18, MinSpawnDistance = 8 };
		AddChild(encounter); encounter.SetPhysicsProcess(false);
		typeof(EncounterController).GetField("_player", Private)!.SetValue(encounter, _player);
		var spawned = (List<Unit>)typeof(EncounterController).GetField("_spawned", Private)!.GetValue(encounter)!;
		Unit Spawn(string id)
		{
			EnemyConfig row = ConfigRows.ById<EnemyConfig>(id)!;
			var entry = new EnemySpawnEntry { Scene = RuntimeResources.Load<PackedScene>(row.ScenePath), Cost = row.SpawnResourceCost, EliteChance = 0 };
			bool ok = (bool)typeof(EncounterController).GetMethod("Spawn", Private)!.Invoke(encounter, new object[] { entry })!;
			entry.Free();
			if (!ok) throw new InvalidOperationException("规则夹具未取得合法生成点");
			Unit unit = spawned[^1]; unit.ProcessMode = ProcessModeEnum.Disabled; _units.Add(unit); return unit;
		}
		Check(encounter.NextWavePressureThreshold == 3 && encounter.CanOpenNextWave, "默认严格低于3资源续波，空场可开波");
		Unit a = Spawn("enemy_chaser"), b = Spawn("enemy_chaser"), c = Spawn("enemy_chaser");
		Check(encounter.AlivePressure == 6 && encounter.RemainingBudget == 94 && !encounter.CanOpenNextWave, "真实生成3普通兵：扣6预算，存活压力6阻止续波");
		a.ForceKill(); b.ForceKill(); Call(encounter, "PruneSpawned");
		Check(encounter.AliveCount == 1 && encounter.AlivePressure == 2 && encounter.CanOpenNextWave, "剩1普通兵压力2允许续波，死亡释放压力");
		c.AddToGroup("elite_units"); Call(encounter, "PruneSpawned");
		Check(encounter.HasPrioritySurvivor && !encounter.CanOpenNextWave, "只剩一只精英，低于资源门槛仍禁止续波");
		c.RemoveFromGroup("elite_units"); c.Tags = c.Tags.Append("Boss").ToArray(); Call(encounter, "PruneSpawned");
		Check(encounter.HasPrioritySurvivor && !encounter.CanOpenNextWave, "只剩一只首领时同样禁止续波");
		c.ForceKill(); Call(encounter, "PruneSpawned");
		Unit d = Spawn("enemy_exploder"), e = Spawn("enemy_exploder"); Call(encounter, "PruneSpawned");
		Check(encounter.AliveCount == 2 && encounter.AlivePressure == 2 && encounter.CanOpenNextWave, "两个自爆小怪合计2资源，允许续波");
		Unit f = Spawn("enemy_exploder"); Call(encounter, "PruneSpawned");
		Check(encounter.AlivePressure == 3 && !encounter.CanOpenNextWave, "恰好等于门槛3时不刷，使用严格小于判定");
		d.ForceKill(); e.ForceKill(); f.ForceKill(); Call(encounter, "PruneSpawned");
		Unit heavy = Spawn("enemy_heavy_slammer"); Call(encounter, "PruneSpawned");
		Check(encounter.AliveCount == 1 && encounter.AlivePressure == 3 && !encounter.CanOpenNextWave, "只剩一个高压力普通敌人也不会按人数误开波");
		heavy.QueueFree(); await Frames(2); Call(encounter, "PruneSpawned");
		Check(encounter.AlivePressure == 0 && !encounter.HasPrioritySurvivor && encounter.CanOpenNextWave, "无效／释放单位及时清除压力，不产生永久阻塞");
		encounter.ResetEncounter();
		Check(encounter.AlivePressure == 0 && encounter.AliveCount == 0 && encounter.RemainingBudget == 100 && !encounter.HasPrioritySurvivor, "重置同时清空压力账本和优先单位状态");
		encounter.QueueFree();
		foreach (Unit unit in _units) if (IsInstanceValid(unit)) unit.QueueFree(); _units.Clear();
		await Frames(2);
	}
}
