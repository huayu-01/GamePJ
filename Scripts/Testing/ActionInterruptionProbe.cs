using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>动作交接的真实输入与动画验收；独立场景，不写玩家存档。</summary>
public partial class ActionInterruptionProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Press(string action, bool value) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = value });
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Action] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图{name}");
	}
	private Unit Spawn(string name, Vector3 position)
	{
		var unit = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		unit.Name = name;
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
		return unit;
	}
	private async Task Equip(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "fall_attack" }) Press(action, false);
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		await Wait(0.15);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true;
			RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			_player = Spawn("ActionPlayer", new Vector3(0, 0.05f, 4));
			_camera = new Camera3D { Current = true, Position = new Vector3(5, 4, 9) };
			AddChild(_camera); _camera.LookAt(new Vector3(0, 1, 2));
			await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
			await DefaultInterrupt();
			await CrossUnitEvents();
			await WindupLocks();
			await ForeignPlayback();
			await ParallelLifetimes();
			await BlinkCancellation();
			await InputOrder();
			await RepeatAndReplay();
			await RejectedAndDeath();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2" }) Press(action, false);
		GD.Print($"[Action] === 动作交接验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task DefaultInterrupt()
	{
		var left = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var right = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill2");
		Press("weapon_skill_2", true); await Wait(0.12);
		Check(right.IsCharging && right.IsActive, "真实右键进入蓄力");
		Press("weapon_skill_1", true); await Wait(0.04); Press("weapon_skill_1", false);
		Check(!right.IsActive && left.IsActive, "后来左键打断右键蓄力，旧动作结束");
		Check(right.CooldownRemaining > 0, "已起手的右键被打断后进入原有冷却");
		await Wait(0.7); Press("weapon_skill_2", false); await Wait(0.2);
		Check(left.LastShotDamage > 0 && right.LastShotDamage == 0, $"旧右键松手不补发：左={left.LastShotDamage}，右={right.LastShotDamage}");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var dodge = _player.GetNode<BackdashAbility>("AbilitySkill1");
		Press("skill_1", true); await Wait(0.12); Press("skill_1", false);
		Check(dodge.InvulnerableOwned && _player.Movement!.IsFacingLocked, "真实Dodge开启无敌和自身朝向锁");
		Press("weapon_skill_1", true); await Wait(0.04); Press("weapon_skill_1", false);
		Check(!dodge.IsActive && !dodge.InvulnerableOwned && !_player.Movement!.IsFacingLocked, "新动作打断后撤并清理无敌、朝向锁");
		await Shot("j3_interrupt_dodge");
		await Wait(0.8);
	}
	private async Task CrossUnitEvents()
	{
		Unit slow = Spawn("SlowAnimation", new Vector3(-5, 0.05f, 3));
		Unit fast = Spawn("FastAnimation", new Vector3(5, 0.05f, 3));
		var slowLoadout = new PlayerLoadout(); var fastLoadout = new PlayerLoadout();
		slowLoadout.Apply(slow, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		fastLoadout.Apply(fast, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		await Wait(0.15); _camera.Current = true;
		var a = slow.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var b = fast.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		a.AnimSpeedScale = 0.1f; b.AnimSpeedScale = 2;
		a.TryPress(); b.TryPress();
		await Wait(0.22);
		Check(b.LastShotDamage > 0, "第二单位真实快速动画ATK已发射");
		Check(a.LastShotDamage == 0, $"其他单位同名ATK不能触发慢动画技能：慢单位伤害快照={a.LastShotDamage}");
		slow.QueueFree(); fast.QueueFree(); await Wait(0.1);
	}
	private async Task WindupLocks()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var left = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var right = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill2");
		var row = (AbilityConfig)ConfigRows.ById<AbilityConfig>("skl_a1_w1_2")!.Duplicate(true);
		row.InterruptLockSeconds = 0.2f; row.AnimSpeedScale = 0.5f;
		string fixture = $"user://j3_action_{Time.GetTicksUsec()}.tres";
		try
		{
			Check(ResourceSaver.Save(row, fixture) == Error.Ok, "前摇锁定配置写盘");
			var restored = RuntimeResources.Load<AbilityConfig>(fixture);
			Check(restored.CanBeInterrupted && Mathf.IsEqualApprox(restored.InterruptLockSeconds, 0.2f), "强类型前摇配置实际读回");
			restored.ApplyTo(right);
		}
		finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(fixture)); }
		Press("weapon_skill_2", true); await Wait(0.24);
		Check(right.IsBusy && right.ActionTimelineTime < 0.2 && right.InterruptLockRemaining > 0,
			$"0.5倍动画前摇：动画时间={right.ActionTimelineTime:0.###}s，剩余锁定={right.InterruptLockRemaining:0.###}s");
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Check(right.IsActive && !left.IsActive && left.CooldownRemaining == 0, "前摇内拒绝新动作且不消耗其冷却");
		await Wait(0.25);
		Check(right.IsInterruptibleNow && right.InterruptLockRemaining == 0, $"动画时间{right.ActionTimelineTime:0.###}s后解锁");
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Check(!right.IsActive && left.IsActive, "前摇结束后真实左键成功打断");
		await Shot("j3_windup_unlock");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		row.AnimSpeedScale = 2; row.ApplyTo(right);
		Press("weapon_skill_2", true); await Wait(0.14);
		Check(right.ActionTimelineTime >= 0.2 && right.IsInterruptibleNow, $"2倍动画提前解锁：动画时间={right.ActionTimelineTime:0.###}s");
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Check(!right.IsActive && left.IsActive, "加速后锁定随动画时间轴缩短");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		row.CanBeInterrupted = false; row.InterruptLockSeconds = 0; row.ApplyTo(right);
		Press("weapon_skill_2", true); await Wait(0.12);
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Check(right.IsActive && !left.IsActive && !right.IsInterruptibleNow, "全程不可打断例外拒绝新动作");
		right.Enabled = false; await Wait(0.05);
		Check(!right.IsActive, "禁用仍能清理不可打断状态");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
	}
	private async Task ForeignPlayback()
	{
		Unit unit = Spawn("PlaybackOwner", new Vector3(-5, 0.05f, 3));
		new PlayerLoadout().Apply(unit, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		await Wait(0.1); _camera.Current = true;
		var a = unit.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		a.AnimSpeedScale = 0.1f; a.TryPress();
		var animator = unit.GetNode<UnitAnimator>("Animator");
		animator.PlaySkillAnimation(a.AnimName, 1);
		ulong externalPlayback = animator.SkillPlaybackId;
		await Wait(0.15);
		Check(a.LastShotDamage == 0, "同单位同名动画的另一轮播放也不能串ATK");
		a.ResetForLoadout();
		Check(animator.SkillPlaybackId == externalPlayback, "旧技能收尾不停止不属于自己的动画");
		unit.QueueFree(); await Wait(0.1);
	}
	private async Task ParallelLifetimes()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
		var hawk = _player.GetNode<HawkEyeAbility>("AbilityWeapon3Skill2");
		var arrow = _player.GetNode<ArrowVolleyAbility>("AbilityWeapon3Skill1");
		Press("weapon_skill_2", true); await Wait(0.5);
		Press("weapon_skill_1", true); await Wait(0.15);
		Check(hawk.IsActive && arrow.IsActive, "后台眼与前台蓄箭并用");
		Press("skill_1", true); await Wait(0.12); Press("skill_1", false);
		Check(hawk.IsActive && !arrow.IsActive && _player.GetNode<BackdashAbility>("AbilitySkill1").InvulnerableOwned,
			"后撤打断蓄箭，后台眼保留");
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		var thrown = _player.GetNode<ScytheThrowAbility>("AbilityWeapon5Skill2");
		Press("weapon_skill_2", true); await Wait(0.03); Press("weapon_skill_2", false);
		await Wait(0.7);
		Check(thrown.InFlight && !thrown.IsBusy, "飞镰刀已经离手，不占动作");
		Press("skill_2", true); await Wait(0.08); Press("skill_2", false);
		var guard = _player.GetNode<GuardStanceAbility>("AbilityBSkill2");
		Check(thrown.InFlight && guard.IsActive, "架势与离手镰刀并用");
		ulong guardPlayback = _player.GetNode<UnitAnimator>("Animator").SkillPlaybackId;
		Press("weapon_skill_2", true); await Wait(0.03); Press("weapon_skill_2", false);
		await Wait(0.4);
		Check(!thrown.InFlight && guard.IsActive && _player.GetNode<UnitAnimator>("Animator").SkillPlaybackId == guardPlayback,
			"回手收尾不取消架势或停止其动画");
	}
	private async Task InputOrder()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var left = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var dodge = _player.GetNode<BackdashAbility>("AbilitySkill1");
		// 同一渲染帧按下，按实际事件顺序交接，不能由场景节点顺序决定。
		Press("weapon_skill_1", true); Press("skill_1", true);
		await Wait(0.03); Press("weapon_skill_1", false); Press("skill_1", false);
		Check(!left.IsActive && dodge.IsActive, "同帧先左键后Q，最后Q持有动作");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Press("skill_1", true); Press("weapon_skill_1", true);
		await Wait(0.03); Press("weapon_skill_1", false); Press("skill_1", false);
		Check(left.IsActive && !dodge.IsActive, "同帧先Q后左键，最后左键持有动作");
	}
	private async Task BlinkCancellation()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_player.GlobalPosition = new Vector3(0, 0.05f, 4); _player.Velocity = Vector3.Zero;
		await Wait(0.05);
		var dash = _player.GetNode<BlinkDashAbility>("AbilityBSkill1");
		dash.GrantInvulnerability = true;
		uint beforeMask = _player.CollisionMask;
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false);
		double blinkWait = 0;
		while (!_player.Movement!.IsDashing && dash.IsActive && blinkWait < 1)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			blinkWait += GetProcessDeltaTime();
		}
		Check(dash.IsActive && _player.Movement!.IsDashing && _player.IsInvulnerable, "真实闪已进入冲刺与配置的无敌");
		Press("skill_2", true); await Wait(0.03); Press("skill_2", false);
		Check(!dash.IsActive && !_player.Movement!.IsDashing && !_player.IsInvulnerable && _player.CollisionMask == beforeMask,
			"架打断闪：冲刺、无敌和临时碰撞状态全部清理");
		Vector3 stopped = _player.GlobalPosition;
		await Wait(0.1);
		Check(_player.GlobalPosition.DistanceTo(stopped) < 0.1f, $"中断后0.1秒无残留冲刺位移：{_player.GlobalPosition.DistanceTo(stopped):0.###}m");
	}
	private async Task RepeatAndReplay()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var left = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var right = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill2");
		Press("weapon_skill_1", true); await Wait(0.03);
		Press("weapon_skill_2", true); await Wait(0.6);
		Check(right.IsActive && !left.IsActive && left.CooldownRemaining == 0, "左键保持按住，原CD已结束，右键正在蓄力");
		Press("weapon_skill_1", true); await Wait(0.03);
		Check(right.IsActive && !left.IsActive, "重复的按住事件不能当作新按下打断动作");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var animator = _player.GetNode<UnitAnimator>("Animator");
		animator.PlaySkillAnimation(left.AnimName, 0.1f);
		await Wait(0.14);
		double before = animator.SkillPlaybackPosition;
		animator.PlaySkillAnimation(left.AnimName, 0.1f);
		Check(before > 0.005 && animator.SkillPlaybackPosition < 0.001, $"同名新播放回到0：{before:0.#####}→{animator.SkillPlaybackPosition:0.#####}");
		animator.StopSkillAnimation();
	}
	private async Task RejectedAndDeath()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var left = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		var right = _player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill2");
		left.Cooldown = 10;
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Press("weapon_skill_2", true); await Wait(0.03);
		Check(right.IsActive && !left.IsActive && left.CooldownRemaining > 9, "打断后的冷却保留原技能数值");
		Press("weapon_skill_1", true); await Wait(0.03); Press("weapon_skill_1", false);
		Check(right.IsActive && !left.IsActive, "冷却中请求不能打断当前动作");
		left.Enabled = false;
		Check(!left.TryPress() && right.IsActive, "禁用技能请求不能打断当前动作");
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Press("skill_1", true); await Wait(0.12); Press("skill_1", false);
		var dodge = _player.GetNode<BackdashAbility>("AbilitySkill1");
		Check(dodge.InvulnerableOwned, "死亡清理前，真实后撤已持有无敌状态");
		_player.IsInvulnerable = false;
		_player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 99999 });
		await Wait(0.06);
		Check(_player.IsDead && !dodge.IsActive && !dodge.InvulnerableOwned && !_player.Movement!.IsFacingLocked,
			"拥有者死亡取消前台动作并清理无敌、朝向锁");
	}
}
