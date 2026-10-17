using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>仅测试场景使用：不改表、不落用户存档，真实输入与伤害事件的平衡采样。</summary>
public partial class BalanceProbe : Node3D
{
	private int _pass, _fail, _hits, _crits;
	private float _rawDamage, _incomingDamage;
	private bool _sampling;
	private Unit _player = null!, _target = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly List<string> _csv = new() { "weapon,trial,seconds,hits,crits,base_damage,actual_damage,base_dps,actual_dps" };
	private readonly List<string> _defense = new() { "character,meta_health_bonus,health,armor,speed,incoming_damage,hits_to_kill,first_armor_loss,first_health_loss" };
	private readonly List<string> _summary = new();
	private string _sampleAbility = "";
	private MeleeComboAbility? _sampleCombo;
	private readonly HashSet<int> _hitSteps = new();
	public override async void _Ready()
	{
		try { await Run(); } catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[Balance] === H专项验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[Balance] {(ok ? "PASS" : "FAIL")} {message}"); }
	private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private void InputAction(bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_1", Pressed = pressed });
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
	}
	private void Aim()
	{
		_player.GlobalPosition = new Vector3(0, 0.05f, 2.1f);
		_player.Velocity = Vector3.Zero;
		_player.Rotation = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(0, 2.5f, 6.5f);
		_camera.LookAt(new Vector3(0, 0.9f, 0));
		_camera.ResetPhysicsInterpolation();
	}
	private async Task Run()
	{
		await Wait(0.25);
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		AddChild(_player);
		_player.GetNode<Node3D>("CameraRig").SetPhysicsProcess(false);
		_camera = new Camera3D { Current = true, Fov = 50 };
		AddChild(_camera);
		_target = new Unit { Name = "BalanceTarget", Faction = Faction.Enemy, MaxHealth = 100000, KnockbackResistance = 1 };
		_target.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.5f } });
		_target.AddChild(new MeshInstance3D { Position = Vector3.Up * 0.9f, Mesh = new CapsuleMesh { Height = 1.8f, Radius = 0.5f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.65f, 0.72f), Roughness = 1 } });
		AddChild(_target);
		_target.GlobalPosition = Vector3.Zero;
		_target.HitReceived += OnHit;
		await Wait(0.1);
		foreach (var pair in new[] {
			(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm), (PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow), (PlayerCharacterKind.Hunter, WeaponPresetKind.Bow),
			(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear) })
		{
			var rates = new List<double>();
			for (int trial = 1; trial <= 2; trial++)
			{
				InputAction(false);
				RunSetup.HasSelection = true;
				RunSetup.Character = pair.Item1;
				RunSetup.Weapon = pair.Item2;
				_loadout.Apply(_player, this, pair.Item1, pair.Item2);
				_player.IsInvulnerable = true;
				Aim();
				await Wait(0.25);
				Ability ability = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
				Check(ability != null && ability.Enabled && _target.Armor == 0 && _target.Shield == 0, $"{pair.Item2}第{trial}轮：真实技能已启用，靶子无甲无盾");
				ability!.ResetForLoadout();
				_sampleCombo = ability as MeleeComboAbility;
				if (_sampleCombo != null && trial == 1)
				{
					int count = _sampleCombo.StrikeCount;
					InputAction(true); await Wait(0.02); InputAction(false); await Wait(0.02);
					InputAction(true); await Wait(0.02); InputAction(false);
					Check(_sampleCombo.ComboStep == 0 && _sampleCombo.StrikeCount == count, "ATK前快速连点只缓冲，不跳段/不提前判定");
					ability.ResetForLoadout();
				}
				_sampleAbility = ability.AbilityId;
				_hitSteps.Clear();
				_hits = _crits = 0; _rawDamage = _incomingDamage = 0;
				float before = _target.Health;
				GD.Seed((ulong)(20261017 + trial));
				_sampling = true;
				ulong start = Time.GetTicksUsec();
				while (Time.GetTicksUsec() - start < 6000000)
				{
					if (ability.HoldToCharge)
					{
						if (ability.IsReady) InputAction(true);
						if (ability.IsCharging && ability.ChargeTime >= ability.ChargeMaxSeconds + 0.03f) InputAction(false);
						await Wait(0.02);
					}
					else
					{
						InputAction(true); await Wait(0.04);
						InputAction(false); await Wait(0.08);
					}
				}
				double seconds = (Time.GetTicksUsec() - start) / 1000000.0;
				_sampling = false;
				float actual = before - _target.Health;
				InputAction(false);
				ability.ResetForLoadout();
				Check(_hits > 0 && _rawDamage > 0 && Mathf.Abs(actual - _incomingDamage) < 0.05f, $"{pair.Item2}第{trial}轮：命中{_hits}，暴击{_crits}，基础伤害{N(_rawDamage)}，实扣{N(actual)}");
				if (_sampleCombo != null) Check(_hitSteps.Count == _sampleCombo.ComboStepCount, $"{pair.Item2}全部{_sampleCombo.ComboStepCount}段均由真实动画命中，段号=[{string.Join(",", _hitSteps)}]");
				double rate = _rawDamage / seconds;
				rates.Add(rate);
				string line = $"{pair.Item2},{trial},{N(seconds)},{_hits},{_crits},{N(_rawDamage)},{N(actual)},{N(rate)},{N(actual / seconds)}";
				_csv.Add(line);
				GD.Print("[BalanceSample] " + line);
				await Wait(0.65);
			}
			_summary.Add($"{pair.Item2,-12} {rates[0]:0.00} / {rates[1]:0.00}");
		}
		Check(MetaProgress.BonusMaxHealth >= 0 && Mathf.IsEqualApprox(MetaProgress.BonusDamageMultiplier, 1), $"当前元进度：生命+{MetaProgress.BonusMaxHealth}，伤害×{MetaProgress.BonusDamageMultiplier}，移速×{MetaProgress.BonusMoveSpeedMultiplier}，暴击+{MetaProgress.BonusCritChance}");
		RunDefense();
		Write("res://docs/balance-samples.csv", _csv);
		Write("res://docs/balance-defense.csv", _defense);
		var layer = new CanvasLayer { Layer = 5 };
		AddChild(layer);
		var panel = new PanelContainer { Position = new Vector2(35, 120), Size = new Vector2(650, 330) };
		layer.AddChild(panel);
		var label = new Label { Text = "平衡采样 · 武技1基础DPS（两轮）\n\n" + string.Join("\n", _summary) + "\n\n固定靶 / 2.1米 / 6秒窗口 / 含真实动画与冷却" };
		label.AddThemeFontSizeOverride("font_size", 23);
		panel.AddChild(label);
		await Wait(0.1);
		await Shot("h_balance_samples");
	}
	private void OnHit(HitInstance hit)
	{
		if (!_sampling || hit.Attacker != _player || hit.Attack?.Ability != _sampleAbility) return;
		_hits++; if (hit.Critical) _crits++;
		if (_sampleCombo != null) _hitSteps.Add(_sampleCombo.ComboStep);
		_rawDamage += hit.Attack.BaseDamage;
		_incomingDamage += hit.Damage;
	}
	private void RunDefense()
	{
		foreach (string id in new[] { "chr_hunter", "chr_weapon_master" })
		{
			CharacterConfig row = ConfigRows.ById<CharacterConfig>(id)!;
			Check(row != null && (id == "chr_hunter" ? row.MaxHealth == 200 && row.StartingArmor == 20 && row.MoveSpeed == 6.5f : row.MaxHealth == 220 && row.StartingArmor == 25 && row.MoveSpeed == 6.8f), $"{id}基础表值保留");
			foreach (float bonus in new[] { 0, MetaProgress.BonusMaxHealth })
			foreach (float damage in new[] { 10f, 20f, 40f })
			{
				var victim = new Unit { Name = "DefenseFixture", Faction = Faction.Player, MaxHealth = row!.MaxHealth + bonus, StartingArmor = row.StartingArmor, ArmorDamageReduction = row.ArmorDamageReduction, DamageReduction = row.DamageReduction, MoveSpeed = row.MoveSpeed };
				AddChild(victim);
				var attack = new AttackInstance { Attacker = _target, BaseDamage = damage, CanCrit = false, Ability = "BalanceFixedHit" };
				int count = 0; float firstArmor = 0, firstHealth = 0;
				while (!victim.IsDead && count < 200)
				{
					DamageResult result = victim.ReceiveHit(attack.RollHit(victim, Vector3.Zero, Vector3.Zero));
					if (count == 0) { firstArmor = result.ArmorLost; firstHealth = result.HealthLost; }
					count++;
				}
				Check(victim.IsDead && count > 1, $"{id}生命{victim.MaxHealth}/甲{row.StartingArmor}，固定{damage}伤害致死={count}次（生命加成{bonus}）");
				string line = $"{id},{N(bonus)},{N(row.MaxHealth + bonus)},{N(row.StartingArmor)},{N(row.MoveSpeed)},{N(damage)},{count},{N(firstArmor)},{N(firstHealth)}";
				_defense.Add(line);
				GD.Print("[DefenseSample] " + line);
				victim.QueueFree();
			}
		}
	}
	private static void Write(string path, List<string> lines)
	{
		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		file.StoreString(string.Join("\n", lines) + "\n");
	}
}
