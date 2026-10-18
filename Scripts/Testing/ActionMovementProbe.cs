using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实按键验证动作移动表、灵动授权、空中例外与收招／换装复位。</summary>
public partial class ActionMovementProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private CharacterMovement Movement => _player.Movement!;
	private AirJumpPassiveAbility Agility => _player.GetNode<AirJumpPassiveAbility>("AbilityPassive");
	private SprintController Sprint => _player.GetNode<SprintController>("Sprint");
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[ActionMovement] {(ok ? "PASS" : "FAIL")} {text}"); }
	private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
	private async Task Equip(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		foreach (string action in new[] { "move_right", "sprint", "jump", "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2" }) Press(action, false);
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 1, 0)); _camera.ResetPhysicsInterpolation();
		await Wait(0.22);
		Check(Movement.IsGrounded && !Movement.IsActionMovementLocked, "空闲地面正常放行，没有旧动作移动锁残留");
	}
	private Ability Skill(string action) => PlayerAbilityInput.FindByInputAction(_player, action)!;
	private async Task Walk(bool sprint = false)
	{
		Press("move_right", true); Press("sprint", sprint); await Wait(0.2);
		Check(Movement.PlanarVelocity.Length() > 3f, "真实移动输入使身体正常移动");
	}
	private async Task Start(string action)
	{
		Press(action, true);
		Check(await Until(() => Skill(action).IsBusy, 1), $"真实输入进入前台动作：{Skill(action).AbilityId}");
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			foreach (var weapon in new[] { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear }) await MasterAttack(weapon);
			foreach (var weapon in new[] { WeaponPresetKind.EnergyPalm, WeaponPresetKind.Crossbow, WeaponPresetKind.Bow }) await HunterAttack(weapon);
			await ExplicitAndAir();
			await FlightAndInterrupt();
			await NonAttackAndImpulse();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[ActionMovement] === 动作移动验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task MasterAttack(WeaponPresetKind weapon)
	{
		await Equip(PlayerCharacterKind.WeaponMaster, weapon); await Walk(true);
		Check(Sprint.IsSprinting, "空闲移动时真实Shift可以奔跑");
		await Start("weapon_skill_1");
		Vector3 start = _player.GlobalPosition;
		await Wait(0.12);
		Check(Skill("weapon_skill_1").IsBusy && Movement.IsActionMovementLocked && !Agility.Enabled, $"{weapon} 地面攻击由默认规则锁主动移动，没有灵动豁免");
		Check(Distance(_player.GlobalPosition, start) < 0.02f && Movement.PlanarVelocity.Length() < 0.02f, "从奔跑进入动作立即收住主动惯性，持续方向／Shift不能绕过");
		Check(!Sprint.IsSprinting, "攻击状态真实退出奔跑");
		float y = _player.GlobalPosition.Y;
		Press("jump", true); await Wait(0.03); Press("jump", false);
		Check(_player.GlobalPosition.Y - y < 0.02f && _player.Velocity.Y <= 0f, "锁定的地面动作不能通过跳跃输入绕过");
		Press("weapon_skill_1", false); await Until(() => !Skill("weapon_skill_1").IsBusy);
		start = _player.GlobalPosition; await Wait(0.15);
		Check(!Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.2f, "真实收招后持续方向输入自然恢复移动");
	}
	private async Task HunterAttack(WeaponPresetKind weapon)
	{
		await Equip(PlayerCharacterKind.Hunter, weapon); await Walk(true);
		string action = weapon == WeaponPresetKind.Bow ? "weapon_skill_1" : "weapon_skill_2";
		Check(Agility.Enabled && Agility.GrantAttackMovement && ConfigRows.ById<AbilityConfig>("skl_a1_passive")!.GrantAttackMovement, "移动攻击授权来自灵动表行与启用的真实被动");
		await Start(action); Vector3 start = _player.GlobalPosition; await Wait(0.15);
		Check(Skill(action).IsBusy && !Movement.IsActionMovementLocked && Skill(action).ActionMovement == Ability.MovementPolicy.Attack, $"{weapon} 真实攻击动画／蓄力期间由灵动放行");
		Check(Distance(_player.GlobalPosition, start) > 0.4f && Movement.PlanarVelocity.Length() > 3f && !Sprint.IsSprinting, "可边移动边攻击，但出招仍退出奔跑加速");
		if (weapon == WeaponPresetKind.EnergyPalm)
		{
			using var row = (AbilityConfig)ConfigRows.ById<AbilityConfig>("skl_a1_passive")!.Duplicate(true);
			row.GrantAttackMovement = false; row.ApplyTo(Agility); start = _player.GlobalPosition; await Wait(0.08);
			Check(Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) < 0.02f, "只改灵动表参数为false，同一攻击动作立即失去移动授权");
			row.GrantAttackMovement = true; row.ApplyTo(Agility); start = _player.GlobalPosition; await Wait(0.12);
			Check(!Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.15f, "只改回表参数true，同一动作恢复移动，没有写死角色A");
			await Screenshot();
		}
		Press(action, false);
	}
	private async Task ExplicitAndAir()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		using var row = (AbilityConfig)ConfigRows.ById<AbilityConfig>("skl_b1_w1_1")!.Duplicate(true);
		row.ActionMovement = Ability.MovementPolicy.Allowed; row.ApplyTo(Skill("weapon_skill_1"));
		await Walk(); await Start("weapon_skill_1"); Vector3 start = _player.GlobalPosition; await Wait(0.12);
		Check(!Movement.IsActionMovementLocked && !Agility.Enabled && Distance(_player.GlobalPosition, start) > 0.4f, "B技能表明确Allowed时直接放行，不依赖灵动");
		Press("weapon_skill_1", false);
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		_player.GlobalPosition = new Vector3(20, 5, 20); _player.Velocity = Vector3.Zero; await Wait(0.03);
		Press("move_right", true); await Start("weapon_skill_1"); start = _player.GlobalPosition; await Wait(0.15);
		Check(!Movement.IsGrounded && Skill("weapon_skill_1").IsBusy && !Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.05f,
			"普通空中方向移动保持，即使B攻击动画没有地面移动许可");
		Press("weapon_skill_1", false);
	}
	private async Task FlightAndInterrupt()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe); await Walk();
		await Start("weapon_skill_2"); var thrown = (ScytheThrowAbility)Skill("weapon_skill_2");
		Check(Movement.IsActionMovementLocked, "抛掷动作出手前锁地面主动移动");
		Press("weapon_skill_2", false); Check(await Until(() => thrown.InFlight), "真实ATK后武器离手");
		Vector3 start = _player.GlobalPosition; await Wait(0.15);
		Check(!Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.2f, "离手实体不占前台，武器在外时恢复主动移动");
		Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false); await Until(() => !thrown.InFlight);
		Check(!Movement.IsActionMovementLocked, "实际回手后不遗留移动锁");
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword); await Walk(); await Start("weapon_skill_1");
		Press("weapon_skill_1", false); Press("skill_1", true); await Wait(0.02); Press("skill_1", false);
		Check(await Until(() => _player.Movement!.HasVelocityOverride, 1), "真实闪Action仍能接管自身位移，不被输入锁阻断");
		Check(await Until(() => !Skill("skill_1").IsBusy, 2), "闪自然结束");
		start = _player.GlobalPosition; await Wait(0.15);
		Check(!Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.2f, "动作交接并收招后正常恢复，没有旧动作残留锁");
	}
	private async Task NonAttackAndImpulse()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		// 通过真实技能行临时授予灵动，验证规则依据被动与技能分类，而非角色名。
		ConfigRows.ById<AbilityConfig>("skl_a1_passive")!.ApplyTo(Agility);
		await Walk(); await Start("skill_2"); Vector3 start = _player.GlobalPosition; await Wait(0.08);
		Check(Agility.Enabled && Agility.GrantAttackMovement && Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) < 0.02f, "灵动只放行Attack，默认Locked的架仍不能主动走位");
		start = _player.GlobalPosition; _player.ReceiveKnockback(Vector3.Right * 4f); await Wait(0.1);
		Check(Movement.IsActionMovementLocked && Distance(_player.GlobalPosition, start) > 0.15f, "主动移动锁保留真实外力击退，没有把身体冻结");
		Press("skill_2", false);
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		Check(!Agility.Enabled && !Agility.GrantAttackMovement, "换装清理临时被动授权");
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = "Agility grants moving attacks from the skill table\nAttack movement enabled | sprint boost ends on cast", Position = _player.GlobalPosition + Vector3.Up * 2.5f, FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label); _camera.GlobalPosition = _player.GlobalPosition + new Vector3(-5, 7, -5); _camera.LookAt(_player.GlobalPosition + Vector3.Up); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng("res://docs/screenshots/j20_action_movement.png") == Error.Ok, "截图 j20_action_movement");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
}
