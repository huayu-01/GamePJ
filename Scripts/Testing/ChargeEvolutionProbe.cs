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

/// <summary>专注／专一／花心／心流与乱金柝的真实输入、动画、投射物、命中和击杀验收。</summary>
public partial class ChargeEvolutionProbe : Node3D
{
	private int _pass, _fail, _atks;
	private float _baselineDamageMultiplier;
	private float ExpectedDamage(float raw) => raw * _baselineDamageMultiplier;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<Projectile> _shots = new();
	private readonly List<double> _shotTimes = new();
	private readonly List<Unit> _targets = new();
	private readonly List<AttackInstance> _hits = new();
	private Ability Skill(string action = "weapon_skill_2") => PlayerAbilityInput.FindByInputAction(_player, action)!;
	private ArrowVolleyAbility Volley => (ArrowVolleyAbility)Skill();
	private AirHoverController Hover => NodeSearch.FindInSubtree<AirHoverController>(_player)!;
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Added(Node node) { if (node is Projectile shot) { _shots.Add(shot); _shotTimes.Add(_elapsed); } }
	private void Event(string name, ulong id) { if (name == "ATK") _atks++; }
	private void Press(string action, bool held) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = held });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[ChargeEvolution] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(WeaponPresetKind weapon = WeaponPresetKind.Crossbow)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "jump", "sprint", "move_right" }) Press(action, false);
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		foreach (var shot in _shots) if (IsInstanceValid(shot)) shot.QueueFree();
		await Wait(0.06);
		_targets.Clear(); _shots.Clear(); _shotTimes.Clear(); _hits.Clear(); _atks = 0;
		RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, RunSetup.Character, weapon); _build = new RunBuild();
		// 夹具沿用隔离档的局外配装；验收直接伤害时包含其真实出手前倍率。
		_baselineDamageMultiplier = AttackOffense.Capture(_player).DamageMultiplier;
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.Rotation = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 0.95f, 26); _camera.LookAt(new Vector3(20, 0.95f, 0)); _camera.ResetPhysicsInterpolation();
		await Wait(0.15);
	}
	private Unit Target(string name, Vector3 position, float health = 100000, Faction faction = Faction.Enemy)
	{
		var unit = new Unit { Name = name, Faction = faction, MaxHealth = health, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.9f } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.65f, Height = 1.3f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = faction == Faction.Player ? Colors.CornflowerBlue : Colors.OrangeRed } });
		unit.AddChild(new Label3D { Name = "ProbeLabel", Text = name, Position = Vector3.Up * 1.2f, FontSize = 32, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		AddChild(unit); unit.GlobalPosition = position; _targets.Add(unit);
		unit.HitReceived += hit => { if (hit.Attack is { } attack && attack.Attacker == _player) _hits.Add(attack); };
		return unit;
	}
	private void Grant(string id)
	{
		var def = RewardCatalog.Find(id);
		Check(def != null && ConfigRows.ById<RewardConfig>(id)?.HasKnownHandler == true, $"真实表行与处理器有效：{id}");
		if (def == null) return;
		_build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(id), $"真实入构筑：{id}");
	}
	private RewardPool Pool()
	{
		var pool = new RewardPool(); pool.Initialize(NodeSearch.FindInSubtree<Weapon>(_player)!, "chr_hunter"); return pool;
	}
	private async Task Manual(float hold)
	{
		int count = _shots.Count;
		Press("weapon_skill_2", true);
		Check(await Until(() => Skill().ChargeTime >= hold), $"真实按住蓄力达到{hold:0.###}秒");
		Check(_shots.Count == count, "手动释放前没有越过蓄力动画ATK提前出弹");
		int atks = _atks;
		Press("weapon_skill_2", false); await Wait(0.025);
		Check(_shots.Count == count && Skill().ChargeReleased, "松手提交快照，仍等待独立释放动画");
		Check(await Until(() => _shots.Count > count) && _atks > atks, "真实释放ATK到达后才生成投射物");
		await Until(() => !Skill().IsActive);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.Crossbow;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GetTree().NodeAdded += Added; _player.GetNode<UnitAnimator>("Animator").SkillEvent += Event;
			await PerfectFocus();
			await SingleArrow();
			await GrowthSingleArrow();
			await Philander();
			await Flow();
			await HoverKill();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().NodeAdded -= Added;
		if (IsInstanceValid(_player)) _player.GetNode<UnitAnimator>("Animator").SkillEvent -= Event;
		GD.Print($"[ChargeEvolution] === 蓄力进化验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task PerfectFocus()
	{
		await Equip(WeaponPresetKind.EnergyPalm);
		float cooldown = Skill().EffectiveCooldown, window = Skill().PerfectWindowSeconds;
		Grant("evo_perfect_focus");
		Check(Mathf.IsEqualApprox(Skill().EffectiveCooldown, cooldown * 0.5f) && Skill().AlwaysPerfectRelease && Skill().PerfectWindowSeconds == window,
			"专注只将武技2冷却乘0.5，使用明确完美标志，保留原窗口秒数");
		Skill().ConfigureChargeBehavior(ConfigRows.ById<RewardConfig>("evo_perfect_focus")!.ChargeBehavior!);
		Check(Mathf.IsEqualApprox(Skill().EffectiveCooldown, cooldown * 0.5f), "重复应用专注配置仍是一次减半，不继续折半");
		Check(!Skill("weapon_skill_1").AlwaysPerfectRelease, "能量掌武技1未获专注的完美授权");
		Target("Perfect", new Vector3(20, 0.95f, 12)); await Wait(0.05);
		await Manual(1.0f);
		Check(await Until(() => _hits.Count > 0), "专注释放的真实能量波命中物理靶");
		var wave = (EnergyWaveAbility)Skill();
		Check(wave.LastShotPerfect && Mathf.IsEqualApprox(wave.LastShotDamage, ExpectedDamage(40)) && Mathf.IsEqualApprox(wave.LastShotSpeed, wave.PerfectSpeed)
			&& _hits.Count == 1 && Mathf.IsEqualApprox(_hits[0].BaseDamage, ExpectedDamage(40)), $"超过原完美窗口仍实际打出完美波：40基础伤害×局外倍率／{wave.PerfectSpeed}表定弹速，一次命中");
		Check(Skill().CooldownRemaining <= cooldown * 0.5f && Skill().CooldownRemaining > 0, "真实收招后的剩余冷却来自减半后的间隔");
		await Equip(WeaponPresetKind.EnergyPalm); Grant("evo_perfect_focus");
		Press("weapon_skill_2", true); await Until(() => Skill().IsCharging); await Wait(0.12); Press("weapon_skill_2", false); await Wait(0.3);
		Check(Skill().LastReleaseEarly && !Skill().LastReleasePerfect && _shots.Count == 1
			&& Mathf.IsEqualApprox(((EnergyWaveAbility)Skill()).LastShotDamage, ExpectedDamage(15)), "专注未满基础仍为15基础伤害低伤波，不把提前松手变成完美发射");
		await Equip(WeaponPresetKind.EnergyPalm);
		Check(!Skill().AlwaysPerfectRelease && Mathf.IsEqualApprox(Skill().EffectiveCooldown, cooldown), "换装移除专注完美与冷却改动");
	}
	private async Task SingleArrow()
	{
		foreach (var sample in new[] { (Hold: 0.45f, Damage: 36f), (Hold: 0.65f, Damage: 60f), (Hold: 1.05f, Damage: 108f) })
		{
			await Equip(); Grant("evo_focus"); Target("Single", new Vector3(20, 0.95f, 12)); await Wait(0.05);
			await Manual(sample.Hold);
			Check(_shots.Count == 1 && Volley.LastShotCount == 1, "专一实际只产生一支箭");
			Check(await Until(() => _hits.Count > 0) && Mathf.IsEqualApprox(_hits[0].BaseDamage, ExpectedDamage(sample.Damage)), $"专一实际命中伤害{_hits.FirstOrDefault()?.BaseDamage:0.###}，期望{ExpectedDamage(sample.Damage)}（含局外倍率）");
		}
		Check(!Pool().Roll(_build, 999).Any(d => d.Id is "evo_focus" or "evo_philander"), "真实候选池移除已获得专一，并以互斥排除花心");
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false); await Until(() => !Skill("weapon_skill_1").IsActive);
		Check(_shots.Count == 4 && ((ArrowVolleyAbility)Skill("weapon_skill_1")).LastShotCount == 3, "有专一时武技1仍实际发三箭，不受武技2单箭配置污染");
		Check(_player.Stats.Get("VolleyCountOverride") == 0 && _player.Stats.Get("VolleyDamagePerLostArrow") == 0, "专一没有写角色全局齐射覆盖／伤害补偿");
	}
	private async Task GrowthSingleArrow()
	{
		foreach (string[] order in new[] { new[] { "evo_focus", "mut_growth" }, new[] { "mut_growth", "evo_focus" } })
		{
			await Equip(); foreach (string id in order) Grant(id);
			Target("GrowthFocus", new Vector3(20, .95f, 12)); await Wait(.05); await Manual(.65f);
			Check(_shots.Count == 1 && Volley.LastShotCount == 1 && _player.GetNodeOrNull<ProjectileDispatch>("ProjectileDispatch")?.PendingCount is null or 0,
				"064专一与增生两种获取顺序均只生成一发，无待发漏刀");
			Check(await Until(() => _hits.Count > 0) && Mathf.IsEqualApprox(_hits[0].BaseDamage, ExpectedDamage(72)),
				"064五把蓄力飞刀加增生一把合并为72直接伤害");
		}
		await Equip(); Grant("mut_growth"); Grant("mut_growth_plus"); Grant("mut_growth_plus2"); Grant("evo_focus");
		Target("GrowthFocusMax", new Vector3(20, .95f, 12)); await Wait(.05); await Manual(1.05f);
		Check(_shots.Count == 1 && await Until(() => _hits.Count > 0) && Mathf.IsEqualApprox(_hits[0].BaseDamage, ExpectedDamage(144)),
			"064满蓄九把加三级增生三把完整合并为一发144伤害");
		await Equip(); Grant("mut_growth");
		await Manual(.45f); await Wait(.15);
		Check(_shots.Count == 4 && Volley.LastShotCount == 4 && Volley.LastSpreadAngleDegrees == 60,
			"062无专一时三把原扇形飞刀保留60度，增生一把另行延迟跟发");
		await Equip(); await Manual(.45f);
		Check(_shots.Count == 3 && Volley.LastShotCount == 3 && Mathf.IsEqualApprox(Volley.LastShotDamage, ExpectedDamage(12)) && _player.Stats.Get("ProjectileCountAdd") == 0,
			"064换装后实际发出原三把12基础伤害飞刀，清除专一和增生参数");
	}
	private async Task Philander()
	{
		await Equip(); Grant("evo_philander");
		Check(Mathf.IsEqualApprox(Volley.ChargeBaseSecondsEffective, 0.4f) && Mathf.IsEqualApprox(Volley.ChargeStepSecondsEffective, 0.1f)
			&& Volley.ChargeMaxStepsEffective == 3, "花心只缩后置0.2→0.1，基础0.4与三档上限保持");
		Check(!Pool().Roll(_build, 999).Any(d => d.Id == "evo_focus"), "真实候选池验证花心反向排除专一");
		var targets = new[] { Target("SeekLeft", new Vector3(13, 0.95f, 0)), Target("SeekCenter", new Vector3(20, 0.95f, 0)), Target("SeekRight", new Vector3(27, 0.95f, 0)) };
		var friend = Target("SeekFriend", new Vector3(20, 0.95f, 8), faction: Faction.Player);
		var dead = Target("SeekDead", new Vector3(18, 0.95f, 10), 1);
		dead.ReceiveHit(new AttackInstance { Attacker = _player, BaseDamage = 100, CanCrit = false }.RollHit(dead, Vector3.Forward, dead.GlobalPosition));
		var far = Target("SeekFar", new Vector3(20, 0.95f, -30));
		_hits.Clear(); await Wait(0.05); await Manual(0.75f);
		Check(_shots.Count == 9 && Volley.LastShotCount == 9, "花心满蓄仍为九支实际箭，未凭缩时增加档数");
		var assigned = _shots.Where(IsInstanceValid).Select(s => s.AssignedHomingTarget).ToArray();
		Check(assigned.Length == 9 && assigned.Distinct().Count() == 3 && targets.All(t => assigned.Count(a => a == t) == 3), "九箭实际分配给三个不同敌人，各三支，友军／死者／范围外不被分配");
		Check(await Until(() => targets.All(t => t.Health < t.MaxHealth), 2), "三个没有标记的不同敌人都受到真实追踪箭命中");
		Check(friend.Health == friend.MaxHealth && far.Health == far.MaxHealth, "友军与范围外目标没有被分配箭误伤");
		await Screenshot("j21_distinct_homing", "Philander: real arrows seek three distinct unmarked enemies");
		await Equip(); Grant("evo_philander");
		await Manual(0.45f);
		Check(_shots.Count == 3 && _shots.All(s => !IsInstanceValid(s) || s.AssignedHomingTarget == null), "没有有效目标时保留原散射飞行，不报错或凭空制造目标");
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		await Until(() => _shots.Count >= 6);
		await Until(() => !Skill("weapon_skill_1").IsActive);
		Check(_shots.Count == 6 && _shots.Skip(3).All(s => !IsInstanceValid(s) || !s.SeekAnyHostile), "有花心时武技1仍为原三箭，没有被授予追踪");
		await Equip(); Grant("evo_philander");
		var a = Target("LostTargetA", new Vector3(16, 0.95f, -10));
		var b = Target("LostTargetB", new Vector3(24, 0.95f, -10)); await Wait(0.05);
		await Manual(0.45f);
		var seeker = _shots.FirstOrDefault(IsInstanceValid);
		Unit? gone = seeker?.AssignedHomingTarget; Unit replacement = gone == a ? b : a;
		Check(seeker != null && gone != null, "真实追踪箭已有分配目标，准备移除该目标");
		gone?.QueueFree();
		Check(await Until(() => seeker != null && IsInstanceValid(seeker) && seeker.HomingTargetName == replacement.Name, 0.5), "被分配目标实际退出场景后，存活箭重新索敌，不读取已释放对象");
		await Equip(); Grant("evo_philander");
		var blocked = Target("BehindWorld", new Vector3(20, 0.95f, 8));
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 8, 0.5f) } });
		AddChild(wall); wall.GlobalPosition = new Vector3(20, 4, 12); await Wait(0.05);
		await Manual(0.45f); await Wait(0.6);
		Check(blocked.Health == blocked.MaxHealth && _hits.Count == 0 && _shots.All(s => !IsInstanceValid(s)), "无标记追踪箭真实撞World消失，墙后目标保持满血");
		wall.QueueFree();
	}
	private async Task Flow()
	{
		foreach (string[] order in new[] { new[] { "evo_flow" }, new[] { "evo_focus", "evo_flow" }, new[] { "evo_flow", "evo_focus" }, new[] { "evo_philander", "evo_flow" }, new[] { "evo_flow", "evo_philander" } })
		{
			await Equip(); foreach (string id in order) Grant(id);
			Target("Flow", new Vector3(20, 0.95f, 12)); await Wait(0.05);
			Check(!Volley.HoldToCharge && Volley.HoldToMaintain && Mathf.IsEqualApprox(Volley.HeldFireInterval, .3f), "心流改为按住连射，专一／花心的获取顺序不改变模式");
			Press("weapon_skill_2", true);
			Check(await Until(() => _shots.Count >= 4) && _atks >= 4 && Volley.LastShotCount == 1, "持续按住跨越普通冷却，每轮真实ATK产生一把飞刀");
			Check(_shotTimes.Zip(_shotTimes.Skip(1), (a,b) => b-a).All(t => t >= .27 && t <= .35), "实际飞刀发射间隔约0.3游戏秒");
			float damage = 6f; // 心流本身固定一发，专一不能补偿并非由自己减少的弹数。
			Check(await Until(() => _hits.Count > 0) && Mathf.IsEqualApprox(_hits[0].BaseDamage, ExpectedDamage(damage)), $"组合后的实际命中伤害{_hits.FirstOrDefault()?.BaseDamage:0.###}，期望{ExpectedDamage(damage)}（含局外倍率）");
			Press("weapon_skill_2", false);
			int stopped = _shots.Count; await Wait(.5);
			Check(_shots.Count == stopped && !Volley.IsActive, "松手立即结束，不继续出刀或漏出上一轮事件");
		}
		await Equip(); Grant("evo_flow");
		Volley.ChargeReleaseAnimSpeedScale = .25f;
		Press("weapon_skill_2", true); await Wait(.025); Press("weapon_skill_2", false); await Wait(.4);
		Check(_shots.Count == 0 && !Volley.IsActive, "本轮ATK前松手立即停止，没有延迟补刀");
		await Equip(); Grant("evo_flow"); Volley.AnimEvent = "MissingReleaseATK";
		Press("weapon_skill_2", true); await Wait(2.3); Press("weapon_skill_2", false);
		Check(_shots.Count == 0 && !Volley.IsActive, "连射缺真实ATK只取消，不用计时补发");
		await Equip(); Grant("evo_flow"); Volley.ChargeReleaseAnimSpeedScale = 0.25f;
		Press("weapon_skill_2", true); await Wait(.025);
		Press("skill_1", true); await Wait(0.03); Press("skill_1", false); await Wait(0.7);
		Check(_shots.Count == 0 && !Volley.IsActive, "心流连射被真实后撤打断，旧ATK不漏出飞刀");
		await Equip();
		Check(Volley.HeldFireInterval == 0 && Volley.HoldToCharge && !Volley.HoldToMaintain && Mathf.IsEqualApprox(Volley.ChargeBaseSecondsEffective, .4f), "换装清除连射，恢复原始蓄力与输入模式");
	}
	private async Task HoverKill()
	{
		await Equip(WeaponPresetKind.EnergyPalm); Grant("evo_stall_kill");
		int grants = Hover.GrantCount;
		var empty = Target("NoHoverKill", new Vector3(20, 0.95f, 12), 1); await Wait(0.05);
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		Check(await Until(() => empty.IsDead) && Hover.GrantCount == grants && !Hover.IsHovering, "没有滞空时，本人真实能量波击杀不会凭空获得滞空");
		Press("skill_2", true); await Wait(0.02); Press("skill_2", false);
		Check(await Until(() => Hover.IsHovering && !_player.Movement!.IsGrounded), "真实跃升Action使玩家起跳并获得滞空");
		float initialHover = Hover.HoverRemaining;
		_player.Movement!.SetExtraAirJumps(0, 0); Press("jump", true); await Wait(1.9);
		Check(Hover.IsSlowFalling && _player.Velocity.Y < 0 && _player.Velocity.Y > -0.3f
			&& Hover.HoverRemaining < initialHover - 1.7f && Hover.HoverRemaining > initialHover - 2.2f,
			"真实获得滞空后自动减缓下落，按当前技能配置实际消耗约1.9秒滞空");
		Press("jump", false); await Wait(.05);
		Check(Hover.Maintaining && Hover.IsSlowFalling, "现行自动滞空不要求持续按跳，松开跳跃继续维持");
		grants = Hover.GrantCount;
		var killer = Target("OtherKiller", new Vector3(25, 0.95f, 15), faction: Faction.Enemy);
		var victim = Target("OtherVictim", new Vector3(25, 0.95f, 13), 1, Faction.Player);
		victim.ReceiveHit(new AttackInstance { Attacker = killer, BaseDamage = 100, CanCrit = false }.RollHit(victim, Vector3.Forward, victim.GlobalPosition));
		Check(victim.IsDead && Hover.GrantCount == grants, "其他单位真实击杀不刷新玩家滞空");
		var mine = Target("OwnHoverKill", new Vector3(20, 0.95f, 12), 1); await Wait(0.05);
		Skill("weapon_skill_1").RefreshCooldown(); Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		Check(await Until(() => mine.IsDead), "滞空中本人真实能量波命中并击杀");
		Check(Hover.GrantCount == grants + 1 && Hover.HoverRemaining > 2.7f, $"乱金柝仅本人击杀刷新一次，剩余{Hover.HoverRemaining:0.###}秒");
		Press("fall_attack", true); await Wait(.05); Press("fall_attack", false);
		Check(!Hover.IsHovering && !Hover.IsSlowFalling, "真实下落输入取消自动滞空并停止减缓下落");
		await Equip(WeaponPresetKind.EnergyPalm);
		Check(!Hover.IsHovering, "换装移除旧滞空状态");
	}
	private async Task Screenshot(string name, string caption)
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		foreach (var target in _targets)
			if (IsInstanceValid(target)) target.GetNode<Label3D>("ProbeLabel").Text = $"{target.Name}: {target.Health:0.0}";
		var label = new Label3D { Text = caption, Position = _player.GlobalPosition + Vector3.Up * 2.5f, FontSize = 36, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label);
		if (name == "j21_distinct_homing") { _camera.GlobalPosition = new Vector3(2, 20, 10); _camera.LookAt(new Vector3(20, 0, 10)); }
		else { _camera.GlobalPosition = _player.GlobalPosition + new Vector3(-5, 12, 5); _camera.LookAt(_player.GlobalPosition + Vector3.Forward * 4); }
		_camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/screenshots/{name}.png")) == Error.Ok, $"截图{name}");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
}
