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

/// <summary>地爆：三配装共通落的真实落地纯击退、阵营／抗性／范围与伤害隔离。</summary>
public partial class LandingKnockbackProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly List<Unit> _targets = new();
	private readonly List<(Unit Target, Vector3 Impulse, bool Landed)> _knocks = new();
	private FallAttackAbility Fall => _player.GetNode<FallAttackAbility>("AbilityFallAttack");
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[LandingKnockback] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(WeaponPresetKind weapon, bool grant)
	{
		Press("fall_attack", false); Press("skill_1", false);
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		_targets.Clear(); _knocks.Clear();
		RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, weapon);
		_player.GlobalPosition = new Vector3(20, 6, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 12, 20); _camera.LookAt(new Vector3(20, 0, 20), Vector3.Forward); _camera.ResetPhysicsInterpolation();
		if (grant)
		{
			var row = ConfigRows.ById<RewardConfig>("evo_ground_burst");
			var def = RewardCatalog.Find("evo_ground_burst");
			Check(row?.SkillEnhancement?.TargetAbility?.Id == "skl_b_fall" && row.HasKnownHandler && def != null, "地爆真实表行指定共通落，不再指灵剑额外落剑式");
			var build = new RunBuild();
			if (def != null) build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
			Check(build.Has("evo_ground_burst") && Mathf.IsEqualApprox(Fall.LandingKnockback, 6f), "真实入构筑配置落地击退预设6");
		}
		await Wait(0.04);
	}
	private Unit Target(string name, Vector3 position, float resistance = 0f, Faction faction = Faction.Enemy, float hp = 10000)
	{
		var unit = new Unit { Name = name, Faction = faction, MaxHealth = hp, KnockbackResistance = resistance };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new SphereShape3D { Radius = 0.3f } });
		unit.AddChild(new MeshInstance3D { Position = Vector3.Up * 0.9f, Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = faction == Faction.Enemy ? Colors.Coral : Colors.Cyan } });
		unit.AddChild(new Label3D { Text = name, Position = Vector3.Up * 1.6f, FontSize = 36 });
		unit.AddChild(new CharacterMovement { Name = "Movement" });
		AddChild(unit); unit.GlobalPosition = position; _targets.Add(unit);
		unit.KnockbackReceived += v => _knocks.Add((unit, v, Fall.HasLanded));
		return unit;
	}
	private bool IsPulse((Unit Target, Vector3 Impulse, bool Landed) item) => item.Landed && Mathf.IsEqualApprox(item.Impulse.Length(), 6f);
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			foreach (var weapon in new[] { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
			{
				float baseline = await Drop(weapon, false);
				float enhanced = await Drop(weapon, true);
				Check(baseline > 0f && Mathf.IsEqualApprox(baseline, enhanced), $"{weapon} 地爆不追加伤害：抗性靶实际总伤害{baseline:0.###}→{enhanced:0.###}");
			}
			await NegativeCases();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[LandingKnockback] === 地爆验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task<float> Drop(WeaponPresetKind weapon, bool grant)
	{
		await Equip(weapon, grant);
		var near = Target("MovableNear", new Vector3(22, 0.05f, 20));
		var resist = Target("Resist1", new Vector3(18, 0.05f, 20), 1f);
		var far = Target("Outside4.2m", new Vector3(20, 0.05f, 24.2f));
		var friend = Target("Friendly", new Vector3(20, 0.05f, 18.5f), faction: Faction.Player);
		var dead = Target("Dead", new Vector3(20, 0.05f, 21.5f), hp: 1);
		dead.ReceiveHit(new AttackInstance { Attacker = _player, Ability = "probe_kill", BaseDamage = 10 }.RollHit(dead, Vector3.Zero, dead.GlobalPosition));
		await Wait(0.03);
		Vector3 nearStart = near.GlobalPosition, resistStart = resist.GlobalPosition;
		Press("fall_attack", true); await Wait(0.02); Press("fall_attack", false);
		Check(await Until(() => Fall.IsDiving), $"{weapon} 真实按键与Action起动下冲");
		Check(Fall.LandingKnockbackPulseCount == 0 && !_knocks.Any(IsPulse), "空中阶段不提前施加地爆击退");
		Check(await Until(() => Fall.HasLanded), $"{weapon} 真实World落地");
		await Wait(0.3);
		if (grant)
		{
			Check(Fall.LandingKnockbackPulseCount == 1 && Fall.LandingKnockbackHitCount == 2, "仅一次落地查询命中两名有效敌人，不含友军／死者／范围外");
			Check(_knocks.Count(k => k.Target == near && IsPulse(k)) == 1 && _knocks.Count(k => k.Target == resist && IsPulse(k)) == 1, "每名范围内敌人只收到一次落地强度6，不依赖额外落剑式");
			Check(near.GlobalPosition.X > nearStart.X + 0.35f && _knocks.Where(k => k.Target == near && IsPulse(k)).All(k => k.Impulse.X > 0f), $"真实移动组件向落点外推出敌人：X {nearStart.X:0.###}→{near.GlobalPosition.X:0.###}");
			Check(new Vector2(resist.GlobalPosition.X - resistStart.X, resist.GlobalPosition.Z - resistStart.Z).Length() < 0.005f, "击退抗性1仍免疫真实位移");
			Check(!_knocks.Any(k => (k.Target == far || k.Target == friend || k.Target == dead) && IsPulse(k)) && far.Health == far.MaxHealth && friend.Health == friend.MaxHealth,
				"范围外／友军／死者不受地爆，友军与外靶生命保持");
			if (weapon == WeaponPresetKind.Scythe) await Screenshot();
		}
		else Check(Fall.LandingKnockbackPulseCount == 0 && Mathf.IsZeroApprox(Fall.LandingKnockback) && !_knocks.Any(IsPulse), "默认未获地爆：没有新增落地击退，原下冲命中保持");
		return resist.MaxHealth - resist.Health;
	}
	private async Task NegativeCases()
	{
		await Equip(WeaponPresetKind.Scythe, true);
		Target("NaturalLanding", new Vector3(22, 0.05f, 20));
		Check(await Until(() => _player.Movement!.IsGrounded, 4), "真实自然下落触地");
		Check(Fall.LandingKnockbackPulseCount == 0 && !_knocks.Any(IsPulse), "普通落地不触发地爆");
		await Equip(WeaponPresetKind.Scythe, true);
		Target("Interrupted", new Vector3(22, 0.05f, 20));
		Press("fall_attack", true); Press("skill_1", true); await Wait(0.03); Press("fall_attack", false); Press("skill_1", false);
		await Wait(0.4);
		Check(Fall.LandingKnockbackPulseCount == 0 && !Fall.HasLanded && !_knocks.Any(IsPulse), "真实前摇被闪打断，不伪造地爆落地");
		await Equip(WeaponPresetKind.Scythe, false);
		Check(Mathf.IsZeroApprox(Fall.LandingKnockback) && Fall.LandingKnockbackPulseCount == 0, "换装清除地爆强度与计数");
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = "Ground burst: common fall, scythe loadout\nOne landing push | resistance 1 stays | no extra damage", Position = new Vector3(20, 3, 20), FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label); _camera.GlobalPosition = new Vector3(28, 8, 29); _camera.LookAt(new Vector3(20, 0.8f, 20)); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng("res://docs/screenshots/j19_ground_burst.png") == Error.Ok, "截图 j19_ground_burst");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
}
