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
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>蓄力松手、真实释放ATK与变奏逐发连射的端到端验收。</summary>
public partial class ChargeReleaseProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private readonly RunBuild _build = new();
	private readonly List<double> _spawnTimes = new();
	private readonly List<ulong> _spawnFrames = new();
	private double _elapsed;
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Added(Node node)
	{
		if (node is Projectile) { _spawnTimes.Add(_elapsed); _spawnFrames.Add(Engine.GetPhysicsFrames()); }
	}
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[ChargeRelease] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图{name}");
	}
	private async Task<Ability> Equip(PlayerCharacterKind character, WeaponPresetKind weapon, string action)
	{
		Press("weapon_skill_1", false); Press("weapon_skill_2", false); Press("skill_1", false);
		RunSetup.Character = character; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 2.5f, 26);
		_camera.LookAt(new Vector3(20, 1, 10)); _camera.ResetPhysicsInterpolation();
		await Wait(0.15);
		return PlayerAbilityInput.FindByInputAction(_player, action)!;
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GetTree().NodeAdded += Added;
			foreach (var row in new[] {
				(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm, "weapon_skill_2"),
				(PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow, "weapon_skill_2"),
				(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow, "weapon_skill_1"),
				(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword, "weapon_skill_2") })
			{
				await ReleaseEvents(row.Item1, row.Item2, row.Item3);
				await MissingEvent(row.Item1, row.Item2, row.Item3);
				await EarlyRelease(row.Item1, row.Item2, row.Item3);
			}
			await PerfectSnapshot();
			await NormalAndChargedSnapshots();
			await InterruptedRelease();
			await Variation();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().NodeAdded -= Added;
		GD.Print($"[ChargeRelease] === 蓄力释放验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task ReleaseEvents(PlayerCharacterKind character, WeaponPresetKind weapon, string action)
	{
		Ability ability = await Equip(character, weapon, action);
		var target = new Unit { Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 1 } });
		AddChild(target); target.GlobalPosition = new Vector3(20, 0.9f, 12);
		AttackInstance? actualAttack = null;
		target.HitReceived += hit => actualAttack = hit.Attack;
		int before = _spawnTimes.Count;
		Press(action, true);
		Check(await Until(() => ability.IsCharging && ability.ChargeTime >= ability.ChargeBaseSecondsEffective + 0.05f), $"{weapon}真实输入蓄力完成");
		Check(_spawnTimes.Count == before, $"{weapon}按住越过起手ATK仍不提前出弹");
		ulong playback = _player.GetNode<UnitAnimator>("Animator").SkillPlaybackId;
		Press(action, false); await Wait(0.03);
		Check(_spawnTimes.Count == before, $"{weapon}松手后仍等待释放ATK，不立即出弹");
		Check(ability.IsActive && _player.GetNode<UnitAnimator>("Animator").SkillPlaybackId > playback, $"{weapon}松手启动独立的一轮真实释放动画");
		float snapshot = ability.ChargeTime;
		Press(action, true); await Wait(0.02); Press(action, false); await Wait(0.02);
		Check(Mathf.Abs(ability.ChargeTime - snapshot) < 0.00001f && !ability.IsCharging, $"{weapon}等待释放时再次按住不重置蓄力快照：{snapshot:0.####}→{ability.ChargeTime:0.####}");
		Check(await Until(() => _spawnTimes.Count > before), $"{weapon}真实释放ATK才生成投射物");
		await Until(() => !ability.IsActive);
		Check(_spawnTimes.Count - before == (weapon == WeaponPresetKind.Crossbow ? 3 : 1), $"{weapon}本次释放发数正确且不重复：{_spawnTimes.Count - before}");
		Check(await Until(() => actualAttack != null), $"{weapon}真实释放投射物命中物理靶");
		float shownDamage = ability is EnergyWaveAbility wave ? wave.LastShotDamage : ((ArrowVolleyAbility)ability).LastShotDamage;
		Check(actualAttack != null && actualAttack.Ability == ability.AbilityId && Mathf.IsEqualApprox(actualAttack.BaseDamage, shownDamage), $"{weapon}真实命中快照与已报告伤害一致：{actualAttack?.BaseDamage}");
		target.QueueFree();
	}
	private async Task MissingEvent(PlayerCharacterKind character, WeaponPresetKind weapon, string action)
	{
		Ability ability = await Equip(character, weapon, action);
		ability.AnimEvent = "MissingReleaseATK";
		int before = _spawnTimes.Count;
		Press(action, true);
		Check(await Until(() => ability.IsCharging && ability.ChargeBaseComplete), $"{weapon}缺事件夹具确实完成本次蓄力");
		Press(action, false);
		await Wait(2.3);
		Check(_spawnTimes.Count == before && !ability.IsActive, $"{weapon}缺释放ATK只取消，不补发或卡住");
	}
	private async Task EarlyRelease(PlayerCharacterKind character, WeaponPresetKind weapon, string action)
	{
		Ability ability = await Equip(character, weapon, action);
		int before = _spawnTimes.Count;
		Press(action, true); await Wait(0.04); Press(action, false); await Wait(0.6);
		Check(_spawnTimes.Count == before && !ability.IsActive, $"{weapon}未获变奏时提前松手不释放");
	}
	private async Task PerfectSnapshot()
	{
		var energy = (EnergyWaveAbility)await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm, "weapon_skill_2");
		Press(energy.InputAction, true);
		Check(await Until(() => energy.IsCharging && energy.ChargeTime >= energy.ChargeBaseSecondsEffective + 0.03f), "完美时机夹具完成本次真实蓄力");
		Press(energy.InputAction, false); await Wait(0.03);
		Check(energy.LastReleasePerfect, "能量掌按松手时刻冻结完美结果");
		await Until(() => !energy.IsActive);
		Check(energy.LastShotPerfect && Mathf.IsEqualApprox(energy.LastShotSpeed, energy.PerfectSpeed), $"完美结果实际发射仍保留：{energy.LastShotPerfect}，弹速{energy.LastShotSpeed}");
		await Shot("j6_energy_release");
	}
	private async Task NormalAndChargedSnapshots()
	{
		var energy = (EnergyWaveAbility)await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm, "weapon_skill_2");
		Press(energy.InputAction, true);
		Check(await Until(() => energy.IsCharging && energy.ChargeTime > energy.ChargeBaseSecondsEffective + energy.PerfectWindowSeconds + 0.05f), "普通能量波夹具实际经过完美窗口");
		Press(energy.InputAction, false); await Wait(0.03);
		Check(!energy.LastReleasePerfect, "经过完美窗口松手冻结为普通释放");
		await Until(() => !energy.IsActive);
		Check(!energy.LastShotPerfect && Mathf.IsEqualApprox(energy.LastShotSpeed, energy.Speed)
			&& Mathf.IsEqualApprox(energy.LastShotDamage, energy.Damage * _player.Stats.Get("DamageMultiplier")), $"普通释放实际伤害{energy.LastShotDamage}／弹速{energy.LastShotSpeed}");
		var crossbow = (ArrowVolleyAbility)await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow, "weapon_skill_2");
		int before = _spawnTimes.Count;
		Press(crossbow.InputAction, true);
		Check(await Until(() => crossbow.IsCharging && crossbow.ChargeTime >= 1.05f), "短弩后置蓄力三档确实完成");
		Press(crossbow.InputAction, false); await Until(() => !crossbow.IsActive);
		Check(crossbow.ChargeSteps == 3 && crossbow.LastShotCount == 9 && _spawnTimes.Count - before == 9, "短弩三档释放快照实际生成9箭，仍保持散射");
	}
	private async Task InterruptedRelease()
	{
		Ability ability = await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow, "weapon_skill_1");
		int before = _spawnTimes.Count;
		Press(ability.InputAction, true);
		Check(await Until(() => ability.IsCharging && ability.ChargeBaseComplete), "打断夹具完成本次真实蓄力");
		Press(ability.InputAction, false); await Wait(0.03);
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false); await Wait(0.7);
		Check(_spawnTimes.Count == before && !ability.IsActive, "释放ATK前被后撤打断，不补发旧箭");
	}
	private async Task Variation()
	{
		var bow = (ArrowVolleyAbility)await Equip(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow, "weapon_skill_1");
		_build.Add(RewardCatalog.Find("evo_variation")!, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has("evo_variation"), "通过真实RewardCatalog→RunBuild.Add获得变奏");
		VolleyRampTuning tuning = ConfigRows.ById<RewardConfig>("evo_variation")!.VolleyRamp!;
		Check(bow.RampMaxCount == tuning.MaxCount && Mathf.IsEqualApprox(bow.RampShotIntervalSeconds, tuning.ShotIntervalSeconds), "强类型资源的循环上限与逐发间隔实际接入");
		for (int i = 0; i < 6; i++)
		{
			await Until(() => bow.CooldownRemaining <= 0.0001f);
			int before = _spawnTimes.Count, expected = i % 3 + 1;
			Press(bow.InputAction, true); await Wait(0.04); Press(bow.InputAction, false);
			await Wait(0.03);
			Check(_spawnTimes.Count == before, $"变奏第{i + 1}次点按仍经真实释放ATK");
			if (i == 2)
			{
				await Until(() => _spawnTimes.Count > before || !bow.IsActive);
				await Shot("j6_variation_burst");
			}
			await Until(() => !bow.IsActive);
			Check(_spawnTimes.Count - before == expected && bow.LastShotCount == expected, $"变奏点按两轮1→2→3循环，第{i + 1}次实际{_spawnTimes.Count - before}箭，期望{expected}");
			if (_spawnTimes.Count - before >= 2)
			{
				float spacing = (float)(_spawnFrames[before + 1] - _spawnFrames[before]) / Engine.PhysicsTicksPerSecond;
				Check(spacing >= tuning.ShotIntervalSeconds - 0.001f && spacing <= tuning.ShotIntervalSeconds + 1f / Engine.PhysicsTicksPerSecond + 0.001f, $"变奏逐箭物理间隔{spacing:0.####}秒，表值{tuning.ShotIntervalSeconds}");
			}
		}
		await Until(() => bow.CooldownRemaining <= 0.0001f);
		int chargedBefore = _spawnTimes.Count;
		int fullTiers = bow.ChargeMaxStepsEffective;
		float fullHold = bow.ChargeBaseSecondsEffective + (bow.ChargeStepSecondsEffective * fullTiers) + 0.02f;
		Press(bow.InputAction, true); await Until(() => bow.IsCharging && bow.ChargeTime >= fullHold); Press(bow.InputAction, false);
		await Until(() => !bow.IsActive);
		Check(_spawnTimes.Count - chargedBefore == 1 && Mathf.IsEqualApprox(bow.LastShotSpeed, bow.Speed + (bow.SpeedPerChargeStep * fullTiers)), $"获得变奏后长按仍为满档单箭（{bow.ChargeSteps}/{fullTiers} 档，弹速 {bow.LastShotSpeed:0.##}），不吃快射连发");
		await Wait(1.3); await Until(() => bow.CooldownRemaining <= 0.0001f);
		int resetBefore = _spawnTimes.Count;
		Press(bow.InputAction, true); await Wait(0.04); Press(bow.InputAction, false); await Until(() => !bow.IsActive);
		Check(_spawnTimes.Count - resetBefore == 1, "超出连续射击窗口后点按回到第一箭");
		await Until(() => bow.CooldownRemaining <= 0.0001f);
		int interruptedBefore = _spawnTimes.Count;
		Press(bow.InputAction, true); await Wait(0.04); Press(bow.InputAction, false);
		Check(await Until(() => _spawnTimes.Count > interruptedBefore), "二连发第一箭已由真实ATK射出");
		Press("skill_1", true); await Wait(0.025); Press("skill_1", false); await Wait(0.4);
		Check(_spawnTimes.Count - interruptedBefore == 1 && !bow.IsActive, "连发中被后撤打断，已出箭保留，待发第二箭取消");
	}
}
