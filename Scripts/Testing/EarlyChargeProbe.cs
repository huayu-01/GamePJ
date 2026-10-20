using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实槽位输入与ATK：三项未蓄满低伤、倍率快照、回程、正常蓄满及打断边界。</summary>
public partial class EarlyChargeProbe : Node3D
{
	private int _pass, _fail, _shots;
	private Unit _player = null!, _target = null!;
	private Camera3D _camera = null!;
	private readonly List<AttackInstance> _hits = new();
	private void Added(Node node) { if (node is Projectile) _shots++; }
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[EarlyCharge] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Press(string action, bool held) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = held });
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Wait(float seconds)
	{
		float elapsed = 0; while (elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}
	private async Task Until(Func<bool> condition, float seconds = 4)
	{
		float elapsed = 0; while (!condition() && elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}
	private static float Damage(Ability ability) => ability switch
	{
		EnergyWaveAbility wave => wave.Damage,
		ArrowVolleyAbility volley => volley.Damage,
		_ => ability.GetNode<AttackHitbox>("Attack").Damage
	};
	private async Task<Ability> Equip(WeaponPresetKind weapon, string action)
	{
		foreach (string input in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(input, false);
		await Frame(); await Frame();
		if (IsInstanceValid(_target)) _target.QueueFree();
		new PlayerLoadout().Apply(_player, this, weapon >= WeaponPresetKind.SpiritSword ? PlayerCharacterKind.WeaponMaster : PlayerCharacterKind.Hunter, weapon);
		_player.GlobalPosition = new Vector3(20, .05f, 20); _player.Velocity = Vector3.Zero; _player.Rotation = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_player.Stats.SetBase("DamageMultiplier", 1f); _player.Stats.SetBase("CritChanceAdd", -100f);
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, .95f, 15)); _camera.ResetPhysicsInterpolation();
		_target = new Unit { Position = new Vector3(20, .95f, 15), Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		_target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .6f } }); AddChild(_target);
		var ability = _player.AbilitySlots.Resolve(action)!;
		_target.HitReceived += hit => { if (hit.Attack is { } attack && attack.Attacker == _player && attack.Ability == ability.AbilityId) _hits.Add(attack); };
		await Wait(.1f); _hits.Clear(); _shots = 0; return ability;
	}
	private async Task Release(Ability ability, string action, float seconds)
	{
		Press(action, true); await Until(() => ability.IsCharging && ability.ChargeTime >= seconds); Press(action, false);
		await Frame(); await Frame();
	}
	public override async void _Ready()
	{
		try
		{
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } }); AddChild(floor);
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera); GetTree().NodeAdded += Added; await Wait(.1f);
			foreach (var sample in new[] { (WeaponPresetKind.EnergyPalm, "weapon_skill_2"), (WeaponPresetKind.Bow, "weapon_skill_1"), (WeaponPresetKind.Spear, "weapon_skill_2") })
				await Verify(sample.Item1, sample.Item2);
			foreach (var sample in new[] { (WeaponPresetKind.Crossbow, "weapon_skill_2"), (WeaponPresetKind.SpiritSword, "weapon_skill_2") })
			{
				var ability = await Equip(sample.Item1, sample.Item2);
				if (ability is SpiritSwordsAbility swords)
				{
					Press(sample.Item2, true); await Frame(); await Frame(); Press(sample.Item2, false); await Wait(.1f);
					Check(!ability.HoldToCharge && swords.SwordCount == 3 && _shots == 0 && _hits.Count == 0, "灵剑武技2已改为即时三剑召唤，不进入低伤蓄力发射链");
				}
				else
				{
					await Release(ability, sample.Item2, .04f); await Wait(.4f);
					Check(!ability.AllowEarlyChargeRelease && ability.LastReleaseAborted && _shots == 0 && _hits.Count == 0, $"{sample.Item1}未获提前释放权限，仍取消");
				}
			}
			var bow = await Equip(WeaponPresetKind.Bow, "weapon_skill_1");
			Press("weapon_skill_1", true); Press("weapon_skill_1", false); await Until(() => _hits.Count > 0);
			Check(_shots == 1 && _hits.Count == 1 && bow.LastReleaseEarly && Mathf.IsEqualApprox(_hits[0].BaseDamage, Damage(bow) * .5f), "同一帧按下又松开也只低伤发射一箭，不滞留在施法态");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { GetTree().NodeAdded -= Added; foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(action, false); }
		GD.Print($"[EarlyCharge] === 未蓄满释放验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Verify(WeaponPresetKind weapon, string action)
	{
		var ability = await Equip(weapon, action); float raw = Damage(ability);
		Check(ability.AllowEarlyChargeRelease && Mathf.IsEqualApprox(ability.EarlyReleaseDamageScale, .5f), $"{weapon}当前配装启用50%未蓄满伤害");
		await Release(ability, action, .06f);
		Check(ability.LastReleaseEarly && !ability.LastReleasePerfect && !ability.LastReleaseAborted && ability.ChargeSteps == 0, $"{weapon}未满基础可释放，不冒领档位或完美");
		Check(_shots == 0 && _hits.Count == 0 && ability.IsActive, $"{weapon}松手后仍等真实释放ATK，不立即出伤害");
		ability.EarlyReleaseDamageScale = .9f; ability.OnInputReleased(); ability.OnInputReleased();
		await Until(() => _hits.Count > 0);
		Check(_hits.Count == 1 && Mathf.IsEqualApprox(_hits[0].BaseDamage, raw * .5f)
			&& Mathf.Abs(_target.MaxHealth - _target.Health - raw * .5f) < .01f, $"{weapon}真实命中半伤；松手后改配置／重复释放不篡改快照");
		Check(_hits.Count == 1 && _hits[0].Ability == ability.AbilityId && _hits[0].Attacker == _player
			&& (ability is WeaponThrowAbility thrown ? thrown.InFlight : _shots == 1), $"{weapon}只出一发且保留当前技能伤害归属");
		bool speed = ability switch
		{
			EnergyWaveAbility wave => Mathf.IsEqualApprox(wave.LastShotSpeed, wave.Speed),
			ArrowVolleyAbility volley => Mathf.IsEqualApprox(volley.LastShotSpeed, volley.Speed),
			SpearThrowAbility spear => Mathf.IsEqualApprox(spear.LastMaxDistance, spear.MaxThrowDistance) && Mathf.IsEqualApprox(spear.LastPowerScale, 1),
			_ => false
		};
		Check(speed, $"{weapon}低伤倍率不降低基础弹速或长枪射程");
		if (ability is SpearThrowAbility returning)
		{
			await Wait(.16f); returning.TryPress(); await Until(() => !returning.InFlight);
			Check(_hits.Count == 2 && Mathf.IsEqualApprox(_hits[1].BaseDamage, raw * .5f), "未满蓄力长枪回程继续沿用半伤快照，每程一次");
		}
		ability = await Equip(weapon, action);
		Check(!ability.LastReleaseEarly && ability.LastReleaseDamageScale == 1f, $"{weapon}再次配装清理低伤快照");
		await Release(ability, action, ability.ChargeBaseSecondsEffective + (weapon == WeaponPresetKind.EnergyPalm ? .26f : .03f));
		await Until(() => _hits.Count > 0);
		Check(_hits.Count == 1 && !ability.LastReleaseEarly && ability.LastReleaseDamageScale == 1
			&& Mathf.IsEqualApprox(_hits[0].BaseDamage, raw), $"{weapon}完成基础蓄力仍为原伤害，不继承上一发半伤");

		ability = await Equip(weapon, action); ability.EarlyReleaseDamageScale = .25f;
		await Release(ability, action, .06f); await Until(() => _hits.Count > 0);
		Check(_hits.Count == 1 && Mathf.IsEqualApprox(_hits[0].BaseDamage, raw * .25f), $"{weapon}倍率改成25%后实际命中同样改变");
		ability = await Equip(weapon, action); await Release(ability, action, .06f);
		Press("skill_1", true); await Wait(.03f); Press("skill_1", false); await Wait(.6f);
		Check(_shots == 0 && _hits.Count == 0 && !ability.IsActive, $"{weapon}提前释放等待ATK时被新动作打断，不补发旧攻击");
		ability = await Equip(weapon, action); ability.AnimEvent = "MissingEarlyATK";
		await Release(ability, action, .06f); await Wait(2.3f);
		Check(_shots == 0 && _hits.Count == 0 && !ability.IsActive, $"{weapon}提前释放缺ATK只取消，不按计时补伤害");
	}
}
