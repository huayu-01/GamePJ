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
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task<bool> Until(Func<bool> predicate, double seconds = 2)
	{
		// 蓄力使用游戏帧时间；渲染卡顿时不能拿墙钟两秒误判尚未走完的一秒蓄力。
		double elapsed = 0;
		while (!predicate() && elapsed < seconds)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			elapsed += GetProcessDeltaTime();
		}
		return predicate();
	}
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
			await Throws();
			await BowCharge();
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
	private void AimAt(Vector3 point)
	{
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 5, 7);
		_camera.LookAt(point);
		_camera.ResetPhysicsInterpolation();
	}
	private Unit TargetAt(Vector3 position)
	{
		var target = new Unit { Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.12f } });
		AddChild(target);
		target.GlobalPosition = position;
		return target;
	}
	private async Task Throws()
	{
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_player.GlobalPosition = new Vector3(-20, 0.05f, 10);
		_player.Velocity = Vector3.Zero;
		AimAt(new Vector3(-20, 0, 2));
		await Wait(0.5);
		var scythe = _player.GetNode<ScytheThrowAbility>("AbilityWeapon5Skill2");
		var scytheCombo = _player.GetNode<MeleeComboAbility>("AbilityWeapon5Skill1");
		var display = _player.GetNode<CharacterDisplay>("Appearance");
		Press("weapon_skill_2", true);
		await Wait(0.025);
		Check(!scythe.InFlight, "镰刀起手未到ATK不会提前抛出");
		Press("weapon_skill_2", false);
		Check(await Until(() => scythe.InFlight), "镰刀真实动画ATK出手");
		Vector3 start = scythe.WeaponPosition;
		await Wait(0.2);
		Check(scythe.WeaponPosition.DistanceTo(start) > 2 && scytheCombo.ExternallyBlocked, "镰刀真实向前飞行，武技1锁定");
		Check(!scythe.IsBusy && !display.EquippedWeapon!.Visible, "投出后释放动作占用，手中模型隐藏");
		Check(await Until(() => scythe.HasSettled), "镰刀到射程后悬停");
		Node3D flyingModel = scythe.GetNode<Node3D>("WeaponVisual");
		Check(flyingModel.GetChildCount() == 1 && flyingModel.GetChild<Node3D>(0).SceneFilePath == display.CurrentWeaponPath, "飞镰刀与手持使用同一武器模型资源");
		Vector3 settled = scythe.WeaponPosition;
		await Wait(0.8);
		Check(scythe.InFlight && !scythe.IsReturning && scythe.WeaponPosition.IsEqualApprox(settled), $"镰刀默认停留而不自动回收：{settled}");
		Unit spinTarget = TargetAt(settled - Vector3.Up * 0.9f);
		var spinTimes = new List<ulong>();
		spinTarget.HitReceived += hit => { if (hit.Attack?.Ability == scythe.AbilityId) spinTimes.Add(Time.GetTicksMsec()); };
		await Wait(1.15);
		Check(spinTimes.Count >= 2 && spinTimes[1] - spinTimes[0] >= 450, $"镰刀停留时真实周期命中，次数={spinTimes.Count}，间隔={(spinTimes.Count > 1 ? spinTimes[1] - spinTimes[0] : 0)}ms");
		await Shot("j2_scythe_stays");
		Press("weapon_skill_2", true);
		await Wait(0.04);
		Press("weapon_skill_2", false);
		Check(scythe.IsReturning && scythe.InFlight && scytheCombo.ExternallyBlocked, "镰刀回程尚未到手时仍锁武技1");
		Check(await Until(() => !scythe.IsActive) && !scytheCombo.ExternallyBlocked, "镰刀实际回手后才解锁");
		await Wait(0.04);
		Check(display.EquippedWeapon!.Visible, "回手恢复同源手持模型");
		spinTarget.QueueFree();
		scythe.ResetForLoadout();
		Press("weapon_skill_2", true); await Wait(0.04); Press("weapon_skill_2", false);
		Check(await Until(() => scythe.HasSettled), "第二次飞镰刀到前方停留");
		_player.GlobalPosition = scythe.WeaponPosition + new Vector3(0.7f, -0.9f, 0);
		Check(await Until(() => !scythe.IsActive), "角色靠近飞镰刀触发召回");
		await Equip(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		var spear = _player.GetNode<SpearThrowAbility>("AbilityWeapon6Skill2");
		var combo = _player.GetNode<MeleeComboAbility>("AbilityWeapon6Skill1");
		for (int steps = 0; steps <= 4; steps++)
		{
			spear.ResetForLoadout();
			_player.GlobalPosition = new Vector3(-20, 0.05f, 10);
			_player.Velocity = Vector3.Zero;
			Vector3 target = new(-20, 0, steps == 0 ? 2 : -30);
			Unit roundTripTarget = TargetAt(new Vector3(-20, 0, 7));
			int roundTripHits = 0;
			roundTripTarget.HitReceived += hit => { if (hit.Attack?.Ability == spear.AbilityId) roundTripHits++; };
			AimAt(target);
			await Wait(0.15);
			Press("weapon_skill_2", true);
			if (steps == 1) { Press("weapon_skill_2", false); Press("weapon_skill_2", true); }
			Check(await Until(() => spear.ChargeTime >= 1.06f + steps * 0.25f, 3), $"长枪蓄至{steps}档，时间={spear.ChargeTime:0.###}，同帧再按时以最终按住态为准");
			Check(!spear.InFlight && spear.LastPathPoints > 2, "蓄力期间预览路径，不提前出手");
			Press("weapon_skill_2", false);
			Check(await Until(() => spear.InFlight), "合格松手后等待真实ATK抛出");
			float expected = 1 + steps * 0.2f;
			Check(Mathf.IsEqualApprox(spear.LastPowerScale, expected) && Mathf.IsEqualApprox(spear.LastMaxDistance, 12 * expected), $"{steps}档基础加算：倍率={spear.LastPowerScale:0.###}、射程={spear.LastMaxDistance:0.###}");
			Check(steps == 0 ? spear.TargetPoint.DistanceTo(target) < 0.05f : Mathf.Abs(spear.LastHorizontalDistance - spear.LastMaxDistance) < 0.02f, $"准星落点／超射程同向截断：{spear.TargetPoint}");
			Check(await Until(() => spear.HasSettled), "长枪与真实World地面碰撞落地");
			Check(roundTripHits == 1, $"飞出路径窄目标只命中一次：{roundTripHits}");
			Vector3 landed = spear.WeaponPosition;
			await Wait(0.4);
			Check(spear.InFlight && !spear.IsReturning && landed.IsEqualApprox(spear.WeaponPosition) && Mathf.Abs(landed.Y) < 0.02f, "长枪落地静止、保持在外，不靠近收回");
			if (steps == 4)
			{
				await Shot("j2_spear_grounded");
				_camera.GlobalPosition = landed + new Vector3(3, 2, 4);
				_camera.LookAt(landed + Vector3.Up * 0.6f);
				_camera.ResetPhysicsInterpolation();
				await Wait(0.1);
				await Shot("j2_spear_grounded_detail");
			}
			Unit returnTarget = TargetAt(new Vector3(-20, 0, 7));
			int returnHits = 0;
			returnTarget.HitReceived += hit => { if (hit.Attack?.Ability == spear.AbilityId) returnHits++; };
			await Wait(0.05);
			float originalSpeed = spear.ReturnSpeed;
			spear.ReturnSpeed = 600;
			Press("weapon_skill_2", true);
			await Wait(0.025);
			Press("weapon_skill_2", false);
			Check(await Until(() => !spear.IsActive) && !combo.ExternallyBlocked, "长枪再次使用实际回手后解锁");
			Check(returnHits == 1, $"600m/s回程扫过窄目标仍命中一次：{returnHits}");
			Check(roundTripHits == 2, $"往返同一窄目标各受击一次：{roundTripHits}");
			spear.ReturnSpeed = originalSpeed;
			returnTarget.QueueFree();
			roundTripTarget.QueueFree();
		}
	}
	private async Task BowCharge()
	{
		await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);
		var bow = _player.GetNode<ArrowVolleyAbility>("AbilityWeapon3Skill1");
		// 蓄到**表里能蓄的最长**（J10：基础 0.5s + 3 个后置档 × 0.3s = 1.4s，共 4 个威力档）。
		float full = bow.ChargeBaseSecondsEffective + (bow.ChargeStepSecondsEffective * bow.ChargeMaxStepsEffective);
		Press("weapon_skill_1", true);
		Check(await Until(() => bow.ChargeTime >= full + 0.02f), $"弓真实输入蓄满（{full:0.##}s = 基础 {bow.ChargeBaseSecondsEffective:0.##}s + {bow.ChargeMaxStepsEffective} 档 × {bow.ChargeStepSecondsEffective:0.##}s）");
		Press("weapon_skill_1", false);
		await Wait(0.25);
		int tiers = bow.ChargeMaxStepsEffective;
		float power = 1f + (bow.ChargeStepScale - 1f) * tiers;
		Check(bow.ChargeSteps == tiers && Mathf.IsEqualApprox(bow.ChargePowerScale, power), $"弓满档按基础加算：{tiers} 个后置档 → ×{bow.ChargePowerScale:0.###} 倍（1 + {bow.ChargeStepScale - 1f:0.##}×{tiers}）");
		Check(Mathf.IsEqualApprox(bow.LastShotDamage, bow.Damage * power * _player.Stats.Get("DamageMultiplier")) && Mathf.IsEqualApprox(bow.LastShotSpeed, bow.Speed + (bow.SpeedPerChargeStep * tiers)), $"弓真实发射快照：伤害={bow.LastShotDamage}、弹速={bow.LastShotSpeed}");
	}
}
