using System;
using System.Collections.Generic;
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

namespace ActRoguelike.Testing;

/// <summary>喜爱：两倍飞速、抵达后1秒索敌、武器最近目标、回手和World边界。</summary>
public partial class ScytheSeekProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<Unit> _targets = new();
	private ScytheThrowAbility Scythe => _player.GetNode<ScytheThrowAbility>("AbilityWeapon5Skill2");
	private MeleeComboAbility Melee => _player.GetNode<MeleeComboAbility>("AbilityWeapon5Skill1");
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[ScytheSeek] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(bool grant = false)
	{
		Press("weapon_skill_1", false); Press("weapon_skill_2", false);
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_build = new RunBuild();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 0.95f, 26); _camera.LookAt(new Vector3(20, 0.95f, 0)); _camera.ResetPhysicsInterpolation();
		if (grant)
		{
			var row = ConfigRows.ById<RewardConfig>("evo_affection");
			var def = RewardCatalog.Find("evo_affection");
			Check(row?.WeaponSeek?.TargetAbility?.Id == "skl_b2_w1_2" && row.HasKnownHandler && def != null, "喜爱真实表行与强类型目标／处理器有效");
			if (def != null) _build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
			Check(_build.Has("evo_affection") && Mathf.IsEqualApprox(Scythe.SeekIntervalSeconds, 1f) && Mathf.IsEqualApprox(Scythe.SeekFlightSpeedScale, 2f), "真实入构筑：到达后1秒索敌，出手／回程速度2倍");
		}
		await Wait(0.2);
	}
	private Unit Target(string name, Vector3 position, Faction faction = Faction.Enemy, float hp = 100000)
	{
		var unit = new Unit { Name = name, Faction = faction, MaxHealth = hp, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new SphereShape3D { Radius = 0.3f } });
		unit.AddChild(new MeshInstance3D { Position = Vector3.Up * 0.9f, Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = faction == Faction.Enemy ? Colors.Coral : Colors.Cyan } });
		unit.AddChild(new Label3D { Text = name, Position = Vector3.Up * 1.7f, FontSize = 40 });
		AddChild(unit); unit.GlobalPosition = position; _targets.Add(unit); return unit;
	}
	private async Task ClearTargets()
	{
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		_targets.Clear(); await Wait(0.1);
	}
	private async Task Throw()
	{
		await Until(() => Scythe.IsReady && !Scythe.InFlight, 4);
		Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false);
		Check(await Until(() => Scythe.InFlight), "真实按键与ATK之后飞镰离手");
		Check(Melee.ExternallyBlocked, "离手期间武技1的真实锁门仍生效");
	}
	private async Task Recall()
	{
		Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false);
		Check(await Until(() => !Scythe.InFlight), "再次真实按键可收回，直到武器实际到手");
		Check(!Melee.ExternallyBlocked, "实际回手后解除武技1锁门");
	}
	private async Task Screenshot(string name, string caption)
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = caption, Position = new Vector3(23, 3.2f, 17), FontSize = 48 };
		var marker = new Label3D { Text = "Flying scythe", Position = Scythe.WeaponPosition + Vector3.Up * 0.9f, FontSize = 40 };
		AddChild(label); AddChild(marker);
		_camera.GlobalPosition = new Vector3(31, 10, 29); _camera.LookAt(new Vector3(23, 0.7f, 16)); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/{name}.png")) == Error.Ok, $"截图 {name}");
		label.QueueFree(); marker.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Scythe;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await DefaultAndSeeking();
			await EmptyAndWorld();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[ScytheSeek] === 飞镰喜爱验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task DefaultAndSeeking()
	{
		await Equip();
		var a = Target("OwnerNearest", new Vector3(26, 0, 20));
		var b = Target("WeaponNearest", new Vector3(20, 0, 11.4f));
		await Wait(0.1); await Throw(); await Wait(2.3);
		Check(Scythe.InFlight && Scythe.HasSettled && Scythe.SeekTickCount == 0 && Mathf.IsZeroApprox(Scythe.SeekIntervalSeconds), "默认未获喜爱：2秒后仍原地悬停，不主动索敌");
		await Recall();
		await Equip(true);
		Target("FriendlyNearest", new Vector3(21, 0, 20), Faction.Player);
		var dead = Target("DeadNearest", new Vector3(22, 0, 20), hp: 1);
		dead.ReceiveHit(new AttackInstance { Attacker = _player, Ability = "probe_kill", BaseDamage = 10 }.RollHit(dead, Vector3.Zero, dead.GlobalPosition));
		await Wait(0.1); await Throw();
		Check(Scythe.WeaponVelocity.Length() > Scythe.ThrowSpeed * 1.9f, "真实离手飞速提高100%");
		await Until(() => Scythe.HasSettled, 1);
		double arrival = _elapsed;
		b.GlobalPosition = Scythe.WeaponPosition + Vector3.Right * 3 - Vector3.Up * .9f;
		await Wait(.65);
		Check(Scythe.SeekTickCount == 0 && Scythe.HasSettled, "抵达后未满1秒不提前索敌");
		Check(await Until(() => Scythe.SeekTickCount == 1, 1), "抵达后第1秒触发第一次索敌");
		Check(Scythe.LastSeekTarget == "WeaponNearest" && Scythe.RedirectCount == 1 && !Scythe.HasSettled,
			$"按武器距离选WeaponNearest，忽略友军／死者及离玩家近的目标：{Scythe.LastSeekTarget}");
		Check(Math.Abs(_elapsed - arrival - 1) < .07 && Scythe.WeaponVelocity.X > 0f, "停留约1秒后武器实际转向侧方");
		Check(await Until(() => Scythe.HasSettled, 1), "重定向飞行到达目标才停留");
		double secondArrival = _elapsed;
		Check(Scythe.WeaponPosition.DistanceTo(b.GlobalPosition + Vector3.Up * 0.9f) < 0.15f && Scythe.HasSettled, "实际到目标点停留而不越过目标");
		Check(b.Health < b.MaxHealth, $"新目标被真实伤害命中，生命{b.Health:0.###}");
		a.GlobalPosition = Scythe.WeaponPosition + Vector3.Right * 6 - Vector3.Up * .9f;
		b.GlobalPosition += Vector3.Right * 30;
		await Wait(.65);
		Check(Scythe.SeekTickCount == 1, "第二次飞行时间不消耗新的停留间隔");
		Check(await Until(() => Scythe.SeekTickCount >= 2, 1) && Math.Abs(_elapsed - secondArrival - 1) < .07 && Scythe.LastSeekTarget == "OwnerNearest", "新一次抵达后重新等1秒，再按最新武器距离切换目标");
		await Screenshot("j48_affection", "Affection: weapon-nearest\n2x speed | wait 1s after arrival");
		int ticks = Scythe.SeekTickCount;
		await Recall(); await Wait(2.1);
		Check(Scythe.SeekTickCount == ticks && !Scythe.InFlight, "回程和回手后停止索敌，不被下一次周期抢回飞行状态");
		int strikes = Melee.StrikeCount;
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		Check(await Until(() => Melee.StrikeCount > strikes), "回手后武技1真实ATK可正常使用");
		await Throw(); Check(await Until(() => Scythe.HasSettled, 1), "再次抛出后先正常悬停");
		_player.GlobalPosition = Scythe.WeaponPosition + new Vector3(0.7f, -0.9f, 0); _player.Velocity = Vector3.Zero;
		Check(await Until(() => !Scythe.InFlight), "携带喜爱时靠近武器仍自动收回");
		await ClearTargets();
		await Equip();
		Check(Mathf.IsZeroApprox(Scythe.SeekIntervalSeconds) && Scythe.SeekTickCount == 0 && Mathf.IsEqualApprox(Scythe.SeekFlightSpeedScale, 1), "重新配装后清除喜爱配置、速度倍率与计时状态");
	}
	private async Task EmptyAndWorld()
	{
		await Equip(true); await Throw(); await Until(() => Scythe.SeekTickCount >= 1, 3);
		Check(Scythe.SeekTickCount == 1 && Scythe.RedirectCount == 0 && Scythe.InFlight && Scythe.HasSettled, "没有有效敌人时只做周期检查，继续悬停而不产生伪目标");
		await Recall();
		await Equip(true);
		var behind = Target("WallBehind", new Vector3(29, 0, 20));
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		var size = new Vector3(0.6f, 4f, 4f);
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.SlateGray } });
		AddChild(wall); wall.GlobalPosition = new Vector3(23, 1.8f, 15.7f);
		await Wait(0.1); await Throw();
		Check(await Until(() => Scythe.SeekTickCount >= 3, 7), "墙前经历三次到达后重新计时的索敌");
		await Wait(0.25);
		Check(Scythe.HasSettled && Scythe.WeaponPosition.X <= 22.71f && behind.Health == behind.MaxHealth,
			$"真实World阻挡与墙面再出发：武器{Scythe.WeaponPosition}，墙后靶零伤害");
		await Screenshot("j48_affection_world", "Affection: World blocks redirected flight\n3 seek attempts | no wall penetration");
		await Recall(); wall.QueueFree(); await ClearTargets();
	}
}
