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

namespace ActRoguelike.Testing;

/// <summary>杀戮践踏必须在成功起跳时触发，覆盖空中跳、失败跳跃与瞬移跳的起点。</summary>
public partial class JumpStompProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<Unit> _targets = new();
	private readonly Dictionary<Unit, List<HitInstance>> _hits = new();
	private readonly List<double> _hitTimes = new();
	private CharacterMovement Movement => _player.Movement!;
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Press(string action, bool held) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = held });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 3)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[JumpStomp] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(PlayerCharacterKind character = PlayerCharacterKind.Hunter, WeaponPresetKind weapon = WeaponPresetKind.EnergyPalm)
	{
		foreach (string action in new[] { "jump", "skill_1", "skill_2", "weapon_skill_1", "weapon_skill_2" }) Press(action, false);
		// 各用例是独立构筑；仅替换RunBuild变量不会移除仍挂在玩家上的规则节点。
		foreach (var rule in _build.Triggers)
			if (IsInstanceValid(rule)) { rule.GetParent()?.RemoveChild(rule); rule.QueueFree(); }
		_build = new RunBuild();
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		await Wait(0.05); _targets.Clear(); _hits.Clear(); _hitTimes.Clear();
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon); _build = new RunBuild();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 1, 12)); _camera.ResetPhysicsInterpolation();
		await Wait(0.2); Check(Movement.IsGrounded, "玩家真实物理身体已落在地面");
	}
	private Unit Target(string name, Vector3 position, Faction faction = Faction.Enemy)
	{
		var unit = new Unit { Name = name, Faction = faction, MaxHealth = 10000, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.4f } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = faction == Faction.Player ? Colors.CornflowerBlue : Colors.OrangeRed } });
		unit.AddChild(new Label3D { Name = "ProbeLabel", Text = name, Position = Vector3.Up, FontSize = 32, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		AddChild(unit); unit.GlobalPosition = position; _targets.Add(unit); _hits[unit] = new();
		unit.HitReceived += hit => { if (hit.Attack?.Ability == "mut_stomp") { _hits[unit].Add(hit); _hitTimes.Add(_elapsed); } };
		return unit;
	}
	private void Grant(string id = "mut_stomp")
	{
		var def = RewardCatalog.Find(id); Check(def != null, $"真实奖励目录存在：{id}");
		if (def == null) return;
		_build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(id), $"真实入构筑：{id}");
	}
	private async Task Jump()
	{
		Press("jump", false);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Press("jump", true);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Press("jump", false);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2); RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await OrdinaryAndAir(); await NaturalAndLocked(); await TeleportOrigin(); await SkillJump(); await TableParameters();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[JumpStomp] === 起跳践踏验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task OrdinaryAndAir()
	{
		await Equip(); var baseline = Target("NoAugment", new Vector3(22, 0.9f, 20)); await Wait(0.05);
		await Jump(); Check(_player.Velocity.Y > 0 && baseline.Health == baseline.MaxHealth, "没有增幅的真实跳跃不制造践踏伤害");
		await Equip(); Grant();
		var near = Target("Near", new Vector3(22, 0.9f, 20));
		var outside = Target("Outside", new Vector3(24, 0.9f, 20));
		var friend = Target("Friend", new Vector3(18, 0.9f, 20), Faction.Player);
		var dead = Target("Dead", new Vector3(20, 0.9f, 22));
		dead.ReceiveHit(new AttackInstance { Attacker = _player, BaseDamage = 20000, CanCrit = false }.RollHit(dead, Vector3.Forward, dead.GlobalPosition)); await Wait(0.05);
		await Jump();
		Check(!Movement.IsGrounded && _player.Velocity.Y > 0 && _hits[near].Count == 1, "地面真实按跳仍在上升时已结算一次，未等待落地");
		Check(_hits[near].All(h => h.Attacker == _player && Mathf.IsEqualApprox(h.Attack!.BaseDamage, 15)), "起跳伤害来自本人真实AttackInstance，表内基础伤害15");
		Check(outside.Health == outside.MaxHealth && friend.Health == friend.MaxHealth && _hits[dead].Count == 0, "3米外、友军与死者不触发伤害");
		await Jump();
		Check(Movement.AirJumpsLeft == 0 && _player.Velocity.Y > 0 && _hits[near].Count == 2, "真实空中跳消耗额外次数，并独立再触发一次伤害");
		Check(_hitTimes.Count == 2 && _hitTimes[1] - _hitTimes[0] < 0.2, "相隔不足0.2秒的两次有效跳跃均触发，没有旧隐藏冷却吞掉空中跳");
		await Jump(); Check(_hits[near].Count == 2, "额外次数耗尽后再按跳失败，不额外触发践踏");
		Check(await Until(() => Movement.IsGrounded), "跳跃后真实自然落地"); await Wait(0.05);
		Check(_hits[near].Count == 2, "普通落地不再额外触发旧落地践踏");
		await Screenshot();
	}
	private async Task NaturalAndLocked()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword); Grant();
		var target = Target("NaturalFall", new Vector3(22, 0.9f, 20)); await Wait(0.05);
		_player.GlobalPosition = new Vector3(20, 4, 20); _player.Velocity = Vector3.Zero;
		await Until(() => !Movement.IsGrounded); Check(await Until(() => Movement.IsGrounded), "没有主动跳跃的自然下落真实落地"); await Wait(0.05);
		Check(_hits[target].Count == 0 && target.Health == target.MaxHealth, "单纯离地及自然落地都不算起跳践踏");
		Press("skill_2", true); await Wait(0.02); Press("skill_2", false); await Until(() => Movement.IsActionMovementLocked);
		await Jump(); Check(_hits[target].Count == 0 && Movement.IsGrounded, "架动画锁主动跳跃时，失败按键不触发践踏");
		await Equip(); Grant(); target = Target("Override", new Vector3(22, 0.9f, 20)); await Wait(0.05);
		Movement.BeginVelocityOverride(this, Vector3.Zero); await Jump(); Movement.EndVelocityOverride(this);
		Check(_hits[target].Count == 0, "技能速度覆盖阻止起跳时，不产生践踏事件");
		await Equip(); target = Target("AfterReset", new Vector3(22, 0.9f, 20)); await Wait(0.05); await Jump();
		Check(_hits[target].Count == 0, "移除旧构筑规则后退订起跳事件，之后真实起跳没有旧增幅伤害");
	}
	private async Task TeleportOrigin()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear); Movement.SetExtraAirJumps(2, 3); Grant(); Grant("evo_light_leap");
		var ground = Target("TeleportStart", new Vector3(22, 0.9f, 20));
		var arrival = Target("TeleportArrival", new Vector3(21, Movement.StandardJumpHeight * 3, 20)); await Wait(0.05);
		await Jump();
		Check(Movement.TeleportJumpCount == 1 && _hits[ground].Count == 1 && _hits[arrival].Count == 0, "瞬移跳成功时按起跳前的位置结算，命中原地近敌，不改成到达点攻击");
		await Jump(); Check(_hits[ground].Count == 1, "瞬移跳在空中再次按键失败，不重复触发");
	}
	private async Task SkillJump()
	{
		await Equip(); Grant(); var target = Target("Leap", new Vector3(22, 0.9f, 20)); await Wait(0.05);
		Press("skill_2", true); await Wait(0.02); Press("skill_2", false);
		Check(await Until(() => _hits[target].Count == 1) && _player.Velocity.Y > 0, "跃升真实Action起跳也发出一次起跳事实，伤害不由动画时长补发");
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		foreach (var target in _targets) if (IsInstanceValid(target)) target.GetNode<Label3D>("ProbeLabel").Text = $"{target.Name}: {target.Health:0.0}";
		var label = new Label3D { Text = "Ground jump + air jump: two pulses before landing\nLanding adds no damage | range, allies and dead filtered", Position = _player.GlobalPosition + Vector3.Up * 2.5f, FontSize = 36, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled }; AddChild(label);
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(-5, 10, -5); _camera.LookAt(_player.GlobalPosition); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage(); Check(screenshot.SavePng("res://docs/screenshots/j22_jump_stomp.png") == Error.Ok, "截图j22_jump_stomp");
		label.QueueFree();
	}
	private async Task TableParameters()
	{
		await Equip();
		using var row = (RewardConfig)ConfigRows.ById<RewardConfig>("mut_stomp")!.Duplicate(true);
		foreach (var entry in row.HandlerParams)
		{
			if (entry.Key == "Radius") entry.Value = 1.5f;
			if (entry.Key == "Damage") entry.Value = 7f;
		}
		var definition = RewardEffectRegistry.Compile(row);
		Check(definition != null, "复制真实表行，仅改半径／伤害后仍经真实注册表编译");
		if (definition == null) return;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		var near = Target("TableNear", new Vector3(21, 0.9f, 20));
		var far = Target("TableFar", new Vector3(22, 0.9f, 20)); await Wait(0.05); await Jump();
		Check(_hits[near].Count == 1 && Mathf.IsEqualApprox(_hits[near][0].Attack!.BaseDamage, 7) && _hits[far].Count == 0,
			"只改表半径1.5／伤害7后真实起跳立即采用新参数，未写死3米／15伤害");
	}
}
