using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>只读平衡审计与真实动作采样；不保存配置、不修改玩家档案、不把木桩输出等同整局强度。</summary>
public partial class BalanceAuditProbe : Node3D
{
	private readonly Dictionary<ulong, Dictionary<string, object?>> _resources = new();
	private readonly List<Resource> _resourceRoots = new();
	private readonly List<object> _enemies = new(), _loadouts = new(), _samples = new();
	private Unit? _player, _target;
	private Camera3D _camera = null!;
	private bool _sampling;
	private float _raw, _applied;
	private int _hits, _crits;
	private readonly Dictionary<string, float> _damageByAbility = new();
	private readonly List<Node> _fixtures = new();
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static IEnumerable<PropertyInfo> Exports(object owner) => owner.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
		.Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.GetCustomAttribute<ExportAttribute>() != null);
	private object? Value(object? value)
	{
		if (value == null) return null;
		if (value is Resource resource)
		{
			if (resource.GetType().Namespace?.StartsWith("ActRoguelike", StringComparison.Ordinal) == true)
			{ Capture(resource); return new { reference = resource.GetInstanceId(), path = resource.ResourcePath, type = resource.GetType().Name }; }
			return resource.ResourcePath;
		}
		if (value is Variant variant) return variant.VariantType switch
		{
			Variant.Type.Int => variant.AsInt64(), Variant.Type.Float => variant.AsDouble(), Variant.Type.Bool => variant.AsBool(),
			Variant.Type.String => variant.AsString(), _ => variant.ToString()
		};
		if (value is Enum) return value.ToString();
		if (value is string || value is bool || value is int || value is long || value is float || value is double) return value;
		if (value is IEnumerable list) { var result = new List<object?>(); foreach (var item in list) result.Add(Value(item)); return result; }
		return value.ToString();
	}
	private Dictionary<string, object?> Numbers(GodotObject owner)
	{
		var result = new Dictionary<string, object?>();
		foreach (var property in Exports(owner)) result[property.Name] = Value(property.GetValue(owner));
		foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
			if (field.GetCustomAttribute<ExportAttribute>() != null) result[field.Name] = Value(field.GetValue(owner));
		return result;
	}
	private void Capture(Resource resource)
	{
		ulong key = resource.GetInstanceId(); if (_resources.ContainsKey(key)) return;
		_resourceRoots.Add(resource);
		var data = new Dictionary<string, object?> { ["instance"] = key, ["path"] = resource.ResourcePath, ["type"] = resource.GetType().Name };
		_resources.Add(key, data); data["properties"] = Numbers(resource);
	}
	private void Scan(string folder)
	{
		foreach (string child in DirAccess.GetDirectoriesAt(folder)) if (child != "Retired") Scan(folder + "/" + child);
		foreach (string file in DirAccess.GetFilesAt(folder))
			if (file.EndsWith(".tres", StringComparison.Ordinal))
			{
				var resource = ResourceLoader.Load(folder + "/" + file);
				if (resource != null && resource.GetType().Namespace?.StartsWith("ActRoguelike", StringComparison.Ordinal) == true) Capture(resource);
			}
	}
	private async Task ClearFixtures()
	{
		_sampling = false;
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2" }) Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
		foreach (var node in _fixtures) if (IsInstanceValid(node)) node.QueueFree(); _fixtures.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("projectiles")) node.QueueFree();
		await Wait(.2); _player = _target = null;
	}
	private object AbilitySnapshot(Ability ability) => new { id = ability.AbilityId, type = ability.GetType().Name, ability.Enabled, ability.InputAction,
		ability.EffectiveCooldown, ability.EffectiveRechargeSeconds, ability.ChargeBaseSecondsEffective, ability.ChargeStepSecondsEffective,
		ability.ChargeMaxStepsEffective, properties = Numbers(ability) };
	private async Task Enemies()
	{
		foreach (UnitConfig row in ConfigIndex.Settings.Enemies.Where(e => !e.IsGlobal).Cast<UnitConfig>().Concat(ConfigIndex.Settings.Bosses))
		foreach (bool elite in row is BossConfig ? new[] { false } : new[] { false, true })
		{
			string path = row is EnemyConfig enemy ? enemy.ScenePath : ((BossConfig)row).ScenePath;
			var unit = RuntimeResources.Load<PackedScene>(path).Instantiate<Unit>(); row.ApplyTo(unit);
			if (elite) unit.AddChild(new EliteModifier()); unit.ProcessMode = ProcessModeEnum.Disabled; AddChild(unit);
			ConfigIndex.ScaleEnemy(unit, this, row is BossConfig); _fixtures.Add(unit);
			var abilities = new List<Ability>(); NodeSearch.CollectInSubtree(unit, abilities);
			var boxes = new List<AttackHitbox>(); NodeSearch.CollectInSubtree(unit, boxes);
			_enemies.Add(new { id = row.Id, row.DisplayName, elite, boss = row is BossConfig, baseHealth = row.MaxHealth,
				unit.MaxHealth, unit.Armor, unit.ArmorDamageReduction, unit.DamageReduction, unit.MoveSpeed, unit.KnockbackResistance,
				damageScale = unit.Stats.Get("DamageMultiplier"), speedScale = unit.Stats.Get("MoveSpeedMultiplier"),
				cost = (row as EnemyConfig)?.SpawnResourceCost, abilities = abilities.Select(AbilitySnapshot).ToArray(),
				hitboxes = boxes.Select(b => new { b.Damage, b.Knockback, b.HitInterval, policy = b.Policy.ToString() }).ToArray() });
			await ClearFixtures();
		}
	}
	private async Task MakePlayer(PlayerCharacterKind character, WeaponPresetKind weapon, float distance)
	{
		RunSetup.Character = character; RunSetup.Weapon = weapon; RunSetup.HasSelection = true;
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player); _fixtures.Add(_player);
		new PlayerLoadout().Apply(_player, this, character, weapon);
		_player.IsInvulnerable = true; _player.SetPhysicsProcess(false); _player.Movement!.SetPhysicsProcess(false);
		var rig = _player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		_player.GlobalPosition = new Vector3(0, .05f, distance); _player.ResetPhysicsInterpolation(); _player.Rotation = Vector3.Zero;
		_target = new Unit { Name = "BalanceTarget", Faction = Faction.Enemy, MaxHealth = 1000000, KnockbackResistance = 1 };
		_target.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .55f, Height = 1.8f } });
		AddChild(_target); _fixtures.Add(_target); _target.Damaged += OnDamaged;
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 1.8f, 4);
		_camera.LookAt(Vector3.Up * .9f); _camera.MakeCurrent(); _camera.ResetPhysicsInterpolation();
		await Wait(.25);
	}
	private void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (!_sampling || hit.Attacker != _player || hit.Attack == null) return;
		_hits++; if (hit.Critical) _crits++; _raw += hit.Attack.BaseDamage; _applied += result.HealthLost + result.ArmorLost + result.ShieldLost;
		_damageByAbility.TryGetValue(hit.Attack.Ability, out float old); _damageByAbility[hit.Attack.Ability] = old + hit.Attack.BaseDamage;
	}
	private sealed class InputDriver
	{
		private readonly Ability _ability; private readonly string _action, _chargeMode;
		private bool _held, _sawFlight; private ulong _nextEdge, _flightAt;
		public int Requests { get; private set; }
		public InputDriver(Ability ability, string action, string chargeMode) { _ability = ability; _action = action; _chargeMode = chargeMode; }
		private void Set(bool pressed)
		{
			Input.ParseInputEvent(new InputEventAction { Action = _action, Pressed = pressed }); _held = pressed;
			if (pressed) Requests++;
		}
		public void Tick()
		{
			ulong now = Time.GetTicksUsec();
			if (_ability is WeaponThrowAbility { InFlight: true } thrown)
			{
				if (!_sawFlight) { _sawFlight = true; _flightAt = now; }
				if (_held) Set(false);
				else if (!thrown.IsReturning && now - _flightAt >= 900000) { Set(true); _nextEdge = now + 40000; }
				return;
			}
			_sawFlight = false;
			if (_ability.HoldToMaintain) { if (!_held && _ability.IsReady) Set(true); return; }
			if (_ability.HoldToCharge)
			{
				// 新按下交给下一次输入帧启动；不能拿上次释放快照在同一帧立即松开。
				if (!_held && _ability.IsReady) { Set(true); return; }
				float desired = _chargeMode == "partial" ? Mathf.Max(.05f, _ability.ChargeBaseSecondsEffective * .35f)
					: _chargeMode == "perfect" ? _ability.ChargeBaseSecondsEffective + .03f
					: _ability.ChargeMaxSeconds > 0 ? _ability.ChargeMaxSeconds + .02f : _ability.ChargeBaseSecondsEffective + _ability.ChargeStepSecondsEffective * _ability.ChargeMaxStepsEffective + .03f;
				if (_held && (_ability.IsCharging && _ability.ChargeTime >= desired || _ability.ChargeReleased || !_ability.IsActive && !_ability.IsReady)) Set(false);
				return;
			}
			if (_held && now >= _nextEdge) { Set(false); _nextEdge = now + 40000; }
			else if (!_held && now >= _nextEdge)
			{
				if (_ability.IsReady || _ability is MeleeComboAbility) { Set(true); _nextEdge = now + 40000; }
			}
		}
	}
	private async Task Sample(PlayerCharacterKind character, WeaponPresetKind weapon, string action, string chargeMode, float distance, bool withPrimary = false)
	{
		await MakePlayer(character, weapon, distance);
		var player = _player!; var ability = player.AbilitySlots.Resolve(action) ?? throw new InvalidOperationException($"槽位未启用 {weapon}/{action}");
		var primary = player.AbilitySlots.Resolve("weapon_skill_1")!;
		_raw = _applied = 0; _hits = _crits = 0; _damageByAbility.Clear();
		var driver = new InputDriver(ability, action, chargeMode);
		var primaryDriver = withPrimary ? new InputDriver(primary, "weapon_skill_1", "full") : null;
		GD.Seed(20261027); _sampling = true;
		ulong began = Time.GetTicksUsec();
		float window = withPrimary ? 12f : ability.HoldToCharge ? 12f : 8f;
		while (Time.GetTicksUsec() - began < window * 1_000_000)
		{
			driver.Tick(); primaryDriver?.Tick();
			await Wait(.02);
		}
		double seconds = (Time.GetTicksUsec() - began) / 1_000_000.0; _sampling = false;
		_samples.Add(new { character = character.ToString(), weapon = weapon.ToString(), action, chargeMode, distance, withPrimary,
			seconds, requests = driver.Requests, hits = _hits, crits = _crits, baseDamage = _raw, actualDamage = _applied, baseDps = _raw / seconds, actualDps = _applied / seconds,
			byAbility = new Dictionary<string, float>(_damageByAbility), endedActive = ability.IsActive, ability.ChargeTime,
			ability.CooldownRemaining, note = "当前真实输入／动画／命中，白板无局外／能力卡／符文，无甲静止单靶。保留实际暴击读数，BaseDps仅统计真正命中的攻击BaseDamage。短窗口和单靶不能代表群体、走位、满构筑或整局强弱。" });
		GD.Print($"[BalanceAudit] SAMPLE {weapon}/{action}/{chargeMode} range={distance} baseDps={_raw/seconds:0.##} hits={_hits}");
		await ClearFixtures();
	}
	public override async void _Ready()
	{
		bool passed = false;
		try
		{
			await Wait(.2);
			GD.Print("[BalanceAudit] START clean-profile");
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			if (MetaProgress.BonusMaxHealth != 0 || MetaProgress.BonusDamageMultiplier != 1 || AbilityStore.ActiveCount != 0)
				throw new InvalidOperationException("必须使用空白隔离档，不能带玩家局外与能力卡采样。");
			GD.Print("[BalanceAudit] START inventory");
			Capture(ConfigIndex.Settings); Scan("res://Config");
			GD.Print($"[BalanceAudit] INVENTORY resources={_resources.Count}");
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } }); AddChild(floor);
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await Enemies();
			string? selection = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--audit-weapons="))?.Split('=')[1];
			foreach (var weapon in new[] { WeaponPresetKind.EnergyPalm, WeaponPresetKind.Crossbow, WeaponPresetKind.Bow, WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
			{
				if (selection != null && !selection.Split(',').Contains(weapon.ToString())) continue;
				var character = (int)weapon <= (int)WeaponPresetKind.Bow ? PlayerCharacterKind.Hunter : PlayerCharacterKind.WeaponMaster;
				await MakePlayer(character, weapon, 2.1f);
				_loadouts.Add(new { character = character.ToString(), weapon = weapon.ToString(), _player!.MaxHealth, _player.Armor, _player.MoveSpeed,
					offense = AttackOffense.Capture(_player), stats = new { damage = _player.Stats.Get("DamageMultiplier"), attackSpeed = _player.Stats.Get("AttackSpeedMultiplier"), crit = _player.Stats.Get("CritChanceAdd") },
					slots = _player.AbilitySlots.Equipped.Select(e => new { slot = e.InputSlot, ability = AbilitySnapshot(e.Ability) }).ToArray() });
				await ClearFixtures();
				await Sample(character, weapon, "weapon_skill_1", "full", 2.1f);
				if (character == PlayerCharacterKind.Hunter) await Sample(character, weapon, "weapon_skill_1", "full", 10);
				await Sample(character, weapon, "weapon_skill_2", "full", 2.1f, weapon is WeaponPresetKind.SpiritSword or WeaponPresetKind.Bow);
				if (weapon == WeaponPresetKind.Bow) await Sample(character, weapon, "weapon_skill_1", "partial", 2.1f);
				if (weapon == WeaponPresetKind.EnergyPalm) await Sample(character, weapon, "weapon_skill_2", "partial", 2.1f);
			}
			var report = new { date = DateTimeOffset.Now, engine = Engine.GetVersionInfo()["string"].AsString(), productionValuesChanged = false,
				resources = _resources.Values.ToArray(), enemies = _enemies, loadouts = _loadouts, samples = _samples };
			string path = ValidationArtifacts.Path($"res://Logs/evidence/balance-audit/balance-audit-20261027{(selection == null ? "" : "-" + selection.Replace(',', '-'))}.json");
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
			System.IO.File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
			GD.Print($"[BalanceAudit] COMPLETE resources={_resources.Count} enemies={_enemies.Count} samples={_samples.Count}"); passed = true;
		}
		catch (Exception error) { GD.Print("[BalanceAudit] FAIL " + error); }
		// 大批反射产生的临时 Variant 必须在原生运行时仍存活时完成终结，避免审计退出顺序干扰证据。
		await ClearFixtures();
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		await Wait(.2);
		GameExit.Request(GetTree(), passed ? 0 : 1);
	}
}
