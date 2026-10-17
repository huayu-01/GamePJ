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
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>按原始角色设计重建验收；使用真实输入、动画、碰撞与生产配装器。</summary>
public partial class CharacterDesignProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[Design] {(ok ? "PASS" : "FAIL")} {message}"); }
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
	}
	private void Aim(Vector3 direction)
	{
		_camera.GlobalPosition = _player.GlobalPosition - direction * 7 + Vector3.Up * 2.2f;
		_camera.LookAt(_player.GlobalPosition + Vector3.Up * 0.9f);
		_camera.ResetPhysicsInterpolation();
	}
	private async Task Equip(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		RunSetup.Character = character;
		RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		await Wait(0.15);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.Character = PlayerCharacterKind.Hunter;
			RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			RunSetup.HasSelection = true;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			AddChild(_player);
			_player.GlobalPosition = new Vector3(0, 0.05f, 4);
			_player.IsInvulnerable = true;
			var rig = _player.GetNode<Node3D>("CameraRig");
			rig.SetPhysicsProcess(false);
			rig.SetProcessInput(false);
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			await DetachedFxLifetime();
			await Facing();
			await SpearCycle();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Design] === 原始设计验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task DetachedFxLifetime()
	{
		var blast = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn").Instantiate<Explosion>();
		AddChild(blast);
		Node fx = blast.GetNode("FX");
		await Wait(0.1);
		Check(fx.GetParent() == this, "爆炸特效已脱离短寿命战斗判定节点");
		// 专用验收刻意深载同一脚本，覆盖旧 await 协程被重载取消的时序。
		PackedScene reload = ResourceLoader.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn", cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
		await Wait(3.2);
		Check(!IsInstanceValid(fx), "场景深度重载后，旧特效仍由原生Timer按时回收");
		GC.KeepAlive(reload);
		if (IsInstanceValid(blast)) blast.QueueFree();
	}
	private async Task Facing()
	{
		foreach (var pair in new[] {
			(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm), (PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow), (PlayerCharacterKind.Hunter, WeaponPresetKind.Bow),
			(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear) })
		{
			await Equip(pair.Item1, pair.Item2);
			foreach (Vector3 direction in new[] { Vector3.Forward, Vector3.Right, Vector3.Back, Vector3.Left })
			{
				Aim(direction);
				await Wait(0.45);
				float alignment = (-_player.GlobalBasis.Z).Dot(direction);
				Check(alignment > 0.995f, $"{pair.Item2} 静止转镜头朝向{direction}，根前向点积={alignment:0.0000}");
			}
			var model = _player.GetNode<CharacterDisplay>("Appearance").ActiveModel!;
			var skeleton = NodeSearch.FindInSubtree<Skeleton3D>(model)!;
			Vector3 feet = Vector3.Zero;
			foreach (string side in new[] { "Left", "Right" })
				feet += skeleton.GetBoneGlobalRest(skeleton.FindBone(side + "ToeBase")).Origin - skeleton.GetBoneGlobalRest(skeleton.FindBone(side + "Foot")).Origin;
			Vector3 visibleForward = skeleton.GlobalBasis * feet;
			visibleForward.Y = 0;
			Check(visibleForward.Normalized().Dot(-_player.GlobalBasis.Z) > 0.8f, $"{pair.Item2} 模型脚尖前向与单位逻辑前向一致，点积={visibleForward.Normalized().Dot(-_player.GlobalBasis.Z):0.0000}");
			bool burst = _player.GetNode<LandingBurstAbility>("AbilityPassive2").Enabled;
			Check(burst == (pair.Item2 == WeaponPresetKind.SpiritSword), $"{pair.Item2} 仅灵剑有落剑式，实际启用={burst}");
			if (pair.Item2 == WeaponPresetKind.Scythe) await Shot("j_scythe_facing_passive");
		}
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Aim(Vector3.Forward);
		await Wait(0.5);
		Vector3 before = -_player.GlobalBasis.Z;
		Input.ParseInputEvent(new InputEventAction { Action = "skill_1", Pressed = true });
		await Wait(0.04);
		Input.ParseInputEvent(new InputEventAction { Action = "skill_1", Pressed = false });
		Aim(Vector3.Right);
		await Wait(0.1);
		var backdash = _player.GetNode<BackdashAbility>("AbilitySkill1");
		Check(backdash.IsBusy && _player.Movement!.IsFacingLocked && (-_player.GlobalBasis.Z).Dot(before) > 0.9999f, "后撤过程中转镜头，起手朝向保持不变");
		Check(backdash.LastImpulse.Dot(before) < -0.1f, $"真实Dodge动画事件向起手方向后方施力：{backdash.LastImpulse}");
		await Wait(0.7);
		Check(!_player.Movement!.IsFacingLocked && (-_player.GlobalBasis.Z).Dot(Vector3.Right) > 0.995f, "后撤结束释放自身朝向锁，恢复准星朝向");
	}

	private async Task SpearCycle()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		_player.GlobalPosition = new Vector3(0, 0.05f, 4);
		_player.Velocity = Vector3.Zero;
		Aim(Vector3.Forward);
		await Wait(0.5);
		var combo = _player.GetNode<MeleeComboAbility>("AbilityWeapon6Skill1");
		var victim = new Unit { Name = "DesignTarget", Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		victim.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.4f } });
		AddChild(victim);
		victim.GlobalPosition = _player.GlobalPosition + Vector3.Forward * 2.3f;
		var sequence = new List<int>();
		victim.HitReceived += hit => { if (hit.Attack?.Ability == combo.AbilityId) sequence.Add(combo.ComboStep); };
		ulong deadline = Time.GetTicksMsec() + 5500;
		while (sequence.Count < 8 && Time.GetTicksMsec() < deadline)
		{
			Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_1", Pressed = true });
			await Wait(0.06);
			Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_1", Pressed = false });
			await Wait(0.05);
		}
		Check(combo.ComboStepCount == 4 && combo.ComboLoop, $"长枪由表配置四段循环：段数={combo.ComboStepCount}");
		Check(string.Join(",", sequence) == "0,1,2,3,0,1,2,3", $"真实输入→动画ATK→连续命中两轮四连，段索引={string.Join(",", sequence)}");
		Check(victim.Health < 10000, $"两轮四连实际扣血：10000→{victim.Health}");
		await Shot("j_spear_four_cycle");
		victim.QueueFree();
	}
}
