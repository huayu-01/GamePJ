using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Visual;
using ActRoguelike.Debug;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>C批真实基地输入探针，随逐步实现补齐角色交互及往返断言。</summary>
public partial class BatchCProbe : Node
{
	private int _pass;
	private int _fail;
	private Node _base = null!;
	private Unit _player = null!;
	private PracticeTarget _target = null!;
	public override async void _Ready()
	{
		try { await Run(); } catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[BatchC] === C批验收结束：通过 {_pass}，失败 {_fail} ===");
		GetTree().Quit(_fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[BatchC] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task Action(string action, double hold = 0.05)
	{
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
		await Wait(hold);
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
		await Wait(0.05);
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
	}
	private async Task KeyPress(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
		await Wait(0.06);
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
		await Wait(0.06);
	}
	private void Aim()
	{
		_player.GlobalPosition = _target.GlobalPosition + new Vector3(0, 0.05f, 3);
		_player.Velocity = Vector3.Zero;
		_player.Rotation = Vector3.Zero;
		var rig = _player.GetNode<Node3D>("CameraRig");
		rig.SetPhysicsProcess(false);
		rig.SetProcessInput(false);
		Camera3D camera = GetViewport().GetCamera3D();
		camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 2.3f, 4);
		camera.LookAt(_target.GlobalPosition + Vector3.Up * 1.1f);
		_player.ResetPhysicsInterpolation();
		rig.ResetPhysicsInterpolation();
		camera.ResetPhysicsInterpolation();
	}
	private async Task Run()
	{
		// 主场景Ready可能早于配置索引的延后建表。
		await Wait(0.25);
		if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--memory-check") >= 0)
		{
			Check(LoadoutMemory.WeaponFor(ConfigRows.ById<CharacterConfig>("chr_hunter")!)?.Id == "wpn_bow", "新进程读回猎手上次的弓");
			Check(LoadoutMemory.WeaponFor(ConfigRows.ById<CharacterConfig>("chr_weapon_master")!)?.Id == "wpn_spirit_sword", "新进程读回武器大师上次的灵剑");
			LoadoutMemory.Restore();
			Check(RunSetup.Character == PlayerCharacterKind.WeaponMaster && RunSetup.Weapon == WeaponPresetKind.SpiritSword, "新进程还原最后选择的角色与合法武器");
			RunSetup.Character = PlayerCharacterKind.Hunter;
			RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			RunSetup.Clear();
			Node freshBase = GD.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate();
			GetTree().Root.AddChild(freshBase);
			GetTree().CurrentScene = freshBase;
			await Wait(0.6);
			Check(RunSetup.Character == PlayerCharacterKind.WeaponMaster && RunSetup.Weapon == WeaponPresetKind.SpiritSword, "新进程基地启动自动恢复保存的选择");
			Check(freshBase.GetNode<CharacterDisplay>("Player/Appearance").CurrentModelPath == ConfigRows.ById<CharacterConfig>("chr_weapon_master")!.ModelPath, "新进程基地实际玩家模型与存档一致");
			return;
		}
		RunSetup.Character = PlayerCharacterKind.Hunter;
		RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
		RunSetup.HasSelection = true;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_base = GD.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate();
		GetTree().Root.AddChild(_base);
		GetTree().CurrentScene = _base;
		await Wait(1.2);
		_player = _base.GetNode<Unit>("Player");
		_target = _base.GetNode<PracticeTarget>("PracticeTarget");
		Check(RunController.Instance == null && StageController.Instance == null, "基地没有关卡/奖励/结算控制器");
		Check(_base.GetNode<AbilityHud>("Hud/Root/Abilities").SlotCount == 5, "基地猎手技能与武技5格已就绪");
		Check(Mathf.IsEqualApprox(_player.MaxHealth, 200 + MetaProgress.BonusMaxHealth), "基地局外加成与关卡同源且只加一次");
		Aim();
		await Wait(0.2);
		float before = _target.Target!.Health + _target.Target.Armor;
		await Action("weapon_skill_1");
		await Wait(0.6);
		Check(_target.HitCount > 0 && _target.Target.Health + _target.Target.Armor < before, $"基地真实左键→动画事件→命中：生命+护甲{before}→{_target.Target.Health + _target.Target.Armor}，命中{_target.HitCount}");
		await Shot("c_base_combat");
		await Action("skill_1");
		await Wait(0.4);
		Check(_player.GetNode<Ability>("AbilitySkill1").CooldownRemaining > 0, "基地Q后撤进入真实冷却");
		RunSetup.Character = PlayerCharacterKind.WeaponMaster;
		RunSetup.Weapon = WeaponPresetKind.SpiritSword;
		await Wait(0.3);
		Check(_base.GetNode<AbilityHud>("Hud/Root/Abilities").SlotCount == 6, "基地换武器大师后即时切为6格");
		Check(Mathf.IsEqualApprox(_player.Movement!.JumpVelocity, 12) && Mathf.IsEqualApprox(_player.Movement.GravityScale, 3.2f), "换角色同步跳跃固定值");
		Aim();
		await Wait(0.3);
		before = _target.Target.Health + _target.Target.Armor;
		await Action("weapon_skill_1");
		await Wait(0.45);
		Check(_target.Target.Health + _target.Target.Armor < before, $"基地真实灵剑近战命中：生命+护甲{before}→{_target.Target.Health + _target.Target.Armor}");
		await Shot("c_base_melee");
		Check(_base.GetNode("CharacterRoster").GetChildCount() == ConfigIndex.Settings.Characters.Length, "每个角色有独立交互实例，数量读取角色表");
		foreach (string characterId in new[] { "chr_hunter", "chr_weapon_master" })
		{
			var stand = _base.GetNode<CharacterStand>($"CharacterRoster/{characterId}");
			_player.GlobalPosition = stand.GlobalPosition + new Vector3(0, 0.05f, 1.4f);
			await Wait(0.2);
			await KeyPress(Key.F);
			await Wait(0.25);
			StartScreen screen = _base.GetNode<StartScreen>("Screens/StartScreen");
			Check(screen.IsOpen && screen.OptionCount == 3 && RunController.CharacterRow(RunSetup.Character)?.Id == characterId,
				$"真实F与{characterId}交互：直接换角色且只列其3把武器 [{screen.OptionIds}]");
			foreach (string id in screen.OptionIds.Split(',')) Check(LoadoutSelection.Allows(stand.Character, ConfigRows.ById<WeaponConfig>(id)), $"列表武器{id}归属正确");
			screen.Close();
			await Wait(0.25);
		}
		WeaponPresetKind prior = RunSetup.Weapon;
		Check(!LoadoutSelection.SelectWeapon(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Bow) && RunSetup.Weapon == prior, "跨角色武器调用被拒绝且不改当前装备");
		await Shot("c_character_stands");
		await SwitchAtStand("chr_hunter", Key.Key3);
		Check(RunSetup.Weapon == WeaponPresetKind.Bow, "猎手真实数字键选择弓");
		await SwitchAtStand("chr_weapon_master", Key.Key2);
		Check(RunSetup.Weapon == WeaponPresetKind.Scythe, "武器大师真实数字键选择镰刀");
		await SwitchAtStand("chr_hunter");
		var choices = _base.GetNode<StartScreen>("Screens/StartScreen");
		Check(RunSetup.Weapon == WeaponPresetKind.Bow && choices.SelectedIndex == 2 && choices.SelectedCardDisabled, "回猎手还原弓，当前武器有选中标识且不可重复点");
		await KeyPress(Key.Key3);
		Check(choices.IsOpen, "选中项数字键不会重复确认或关闭面板");
		await KeyPress(Key.Escape);
		await Wait(0.3);
		await SwitchAtStand("chr_weapon_master");
		Check(RunSetup.Weapon == WeaponPresetKind.Scythe && choices.SelectedIndex == 1 && choices.SelectedCardDisabled, "A交互B：切换B，恢复镰刀并高亮");
		Check(_player.GetNode<CharacterDisplay>("Appearance").CurrentModelPath == ConfigRows.ById<CharacterConfig>("chr_weapon_master")!.ModelPath, "玩家真实换成武器大师模型");
		await Shot("c_scythe_selected");
		await KeyPress(Key.Key1);
		await Wait(0.35);
		Check(RunSetup.Weapon == WeaponPresetKind.SpiritSword && !choices.IsOpen, "点灵剑后关闭面板并立刻切换装备");
		var display = _player.GetNode<CharacterDisplay>("Appearance");
		Check(display.CurrentWeaponPath.EndsWith("spirit_sword.tscn") && _base.GetNode<AbilityHud>("Hud/Root/Abilities").DisplayedIds.Contains("skl_b1_w1_1"), "灵剑握持模型与武技组同步");
		Check(_base.GetNode<CharacterDisplay>("CharacterRoster/chr_weapon_master/Visual").CurrentWeaponPath == display.CurrentWeaponPath, "交互对象自己也显示最后选择的灵剑");
		Check(LoadoutMemory.LastSaveSucceeded, "角色分别记忆的选择真实落盘成功");
		foreach (string id in new[] { "chr_hunter", "chr_weapon_master" }) Check(SafeVisual(_base.GetNode($"CharacterRoster/{id}/Visual")), $"{id}展示子树无单位/碰撞/分组");
		Aim();
		await Wait(0.3);
		before = _target.Target.Health + _target.Target.Armor;
		await Action("weapon_skill_1");
		await Wait(0.5);
		Check(_target.Target.Health + _target.Target.Armor < before, "切模型后可见角色的真实动画事件继续命中木桩");
		await Shot("c_sword_equipped");
		await Wait(0.7);
		Check(_player.Movement!.ExtraAirJumps == 0 && !_player.GetNode<AirHoverController>("AirHover").IsHovering, "换到B后不残留A的额外跳跃或滞空");
		_player.GlobalPosition = new Vector3(0, 0.05f, 6);
		_player.Velocity = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
		await Wait(0.2);
		float startY = _player.GlobalPosition.Y;
		await Action("jump");
		await Wait(0.13);
		Check(_player.GlobalPosition.Y > startY + 0.5f, "基地真实空格起跳");
		Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = true });
		await Wait(0.02);
		Check(_player.GetNode<FallAttackAbility>("AbilityFallAttack").IsDiving && _player.Velocity.Y < -20, "基地真实下落攻击加速");
		await Shot("c_base_dive");
		Input.ParseInputEvent(new InputEventAction { Action = "fall_attack", Pressed = false });
		await Wait(0.4);
		Check(_player.Movement.IsGrounded, "下落攻击真实落地");
		Input.ParseInputEvent(new InputEventAction { Action = "move_forward", Pressed = true });
		await Wait(0.2);
		Input.ParseInputEvent(new InputEventAction { Action = "sprint", Pressed = true });
		await Wait(0.4);
		Check(_player.GetNode<SprintController>("Sprint").IsSprinting && _player.Movement.PlanarVelocity.Length() > _player.MoveSpeed,
			$"基地真实移动+Shift奔跑提速：奔跑={_player.GetNode<SprintController>("Sprint").IsSprinting} 实速={_player.Movement.PlanarVelocity.Length():0.###} 基速={_player.MoveSpeed:0.###}");
		Input.ParseInputEvent(new InputEventAction { Action = "sprint", Pressed = false });
		Input.ParseInputEvent(new InputEventAction { Action = "move_forward", Pressed = false });
		await Wait(0.15);
		Check(!_player.GetNode<SprintController>("Sprint").IsSprinting, "松开Shift退出奔跑");
		int shards = MetaProgress.Shards;
		float vitality = _player.Health + _player.Armor;
		_player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 30 });
		Check(_player.Health + _player.Armor < vitality, "基地入站伤害走真实生命护甲结算");
		int respawns = _target.RespawnCount;
		_player.Stats.SetBase("DamageMultiplier", 1000);
		Aim();
		await Wait(0.3);
		await Action("weapon_skill_1");
		await Wait(0.5);
		Check(_target.Target!.IsDead, "高伤测试装备经真实左键击破木桩");
		await Wait(2.5);
		Check(_target.RespawnCount == respawns + 1 && _target.Target is { IsDead: false }, "木桩击破后自动重建且可继续练习");
		Check(MetaProgress.Shards == shards, "基地击破木桩不发结算碎片");
		_player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 100000 });
		await Wait(1.2);
		_base = GetTree().CurrentScene;
		_player = _base.GetNode<Unit>("Player");
		Check(!_player.IsDead && _player.Health == _player.MaxHealth && RunController.Instance == null && MetaProgress.Shards == shards, "基地死亡后重建练习场，不生成关卡结算或资源");
		Check(RunSetup.Character == PlayerCharacterKind.WeaponMaster && RunSetup.Weapon == WeaponPresetKind.SpiritSword, "练习死亡后仍保留各角色已选装备");
		var gate = _base.GetNode<Node3D>("TerminalGate");
		_player.GlobalPosition = gate.GlobalPosition + Vector3.Back;
		_player.Velocity = Vector3.Zero;
		await Wait(0.25);
		await KeyPress(Key.F);
		await Wait(1.5);
		Node level = GetTree().CurrentScene;
		Check(level.SceneFilePath == "res://Scenes/Main.tscn" && RunController.Instance?.CurrentWeapon == WeaponPresetKind.SpiritSword && RunController.Instance.CurrentCharacter == PlayerCharacterKind.WeaponMaster, "真实F出击门带B+灵剑进入关卡");
		Check(level.GetNode<CharacterDisplay>("Player/Appearance").CurrentModelPath == ConfigRows.ById<CharacterConfig>("chr_weapon_master")!.ModelPath, "基地与关卡使用相同角色模型");
		await Shot("c_enter_level");
		GmConsole levelGm = level.GetNode<GmConsole>("GmConsole");
		levelGm.ExecuteCommand("run chr_hunter wpn_spirit_sword");
		Check(!levelGm.LastCommandSucceeded && RunSetup.Character == PlayerCharacterKind.WeaponMaster && RunSetup.Weapon == WeaponPresetKind.SpiritSword,
			"GM跨角色武器命令也拒绝且不留下部分配装修改");
		Check(level.GetNode<GmConsole>("GmConsole").ExecuteCommand("base").Contains("返回基地"), "GM辅助返回基地走正常场景切换");
		await Wait(0.9);
		Check(GetTree().CurrentScene.SceneFilePath == "res://Scenes/Levels/Base.tscn" && RunSetup.Weapon == WeaponPresetKind.SpiritSword && !GetTree().Paused, "关卡往返后装备选择与暂停状态正确");
	}
	private bool SafeVisual(Node node)
	{
		if (node is Unit or CollisionObject3D || node.GetGroups().Count > 0) return false;
		foreach (Node child in node.GetChildren()) if (!SafeVisual(child)) return false;
		return true;
	}
	private async Task SwitchAtStand(string id, Key choice = Key.None)
	{
		var stand = _base.GetNode<CharacterStand>($"CharacterRoster/{id}");
		_player.GlobalPosition = stand.GlobalPosition + new Vector3(0, 0.05f, 1.4f);
		_player.Velocity = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
		await Wait(0.2);
		await KeyPress(Key.F);
		await Wait(0.3);
		Check(_base.GetNode<StartScreen>("Screens/StartScreen").IsOpen, $"真实F打开{id}的武器页");
		if (choice == Key.None) return;
		await KeyPress(choice);
		await Wait(0.35);
	}
}
