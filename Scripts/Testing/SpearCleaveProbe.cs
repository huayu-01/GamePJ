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

/// <summary>金光大道：真实下劈段、前伸距离、独立伤害乘区与配装还原。</summary>
public partial class SpearCleaveProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<Unit> _targets = new();
	private readonly List<(Unit Target, AttackInstance Attack)> _hits = new();
	private MeleeComboAbility Melee => _player.GetNode<MeleeComboAbility>("AbilityWeapon6Skill1");
	private SpearThrowAbility Spear => _player.GetNode<SpearThrowAbility>("AbilityWeapon6Skill2");
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[SpearCleave] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(bool grant = false)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(action, false);
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		_targets.Clear(); _hits.Clear();
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		_build = new RunBuild();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 0, 0)); _camera.ResetPhysicsInterpolation();
		if (grant)
		{
			var row = ConfigRows.ById<RewardConfig>("evo_golden_road");
			var def = RewardCatalog.Find("evo_golden_road");
			Check(row?.SkillEnhancement?.TargetAbility?.Id == "skl_b3_w1_1" && row.SkillEnhancement.ComboStepIndex == 2 && row.HasKnownHandler && def != null,
				"金光大道强类型指定长枪武技1的index2下劈");
			if (def != null) _build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
			Check(_build.Has("evo_golden_road"), "奖励真实入构筑");
		}
		await Wait(0.2);
	}
	private Unit Target(string name, Vector3 position, float radius = 0.12f)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy, MaxHealth = 100000, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = radius } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		unit.AddChild(new Label3D { Text = name, Position = Vector3.Up * 0.6f, FontSize = 40 });
		AddChild(unit); unit.GlobalPosition = position; _targets.Add(unit);
		unit.Damaged += (_, hit) => { if (hit.Attacker == _player && hit.Attack != null) _hits.Add((unit, hit.Attack)); };
		return unit;
	}
	private async Task Tap()
	{
		await Until(() => Melee.IsBusy || Melee.IsReady);
		int before = Melee.StrikeCount;
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		Check(await Until(() => Melee.StrikeCount > before), $"真实动画ATK：段{Melee.ComboStep + 1}/4");
		await Wait(Melee.Series!.StepActiveTimes[Melee.ComboStep] + 0.04f);
	}
	private async Task<float> ThrowDamage(bool grant)
	{
		await Equip(grant);
		var target = Target("ThrowTarget", new Vector3(20, 1.5f, 16), 0.5f);
		await Wait(0.1);
		Press("weapon_skill_2", true); await Wait(1.12); Press("weapon_skill_2", false);
		Check(await Until(() => Spear.InFlight), "武技2真实释放ATK出手");
		Check(await Until(() => _hits.Any(h => h.Target == target)), "武技2真实出程命中");
		float damage = _hits.FirstOrDefault(h => h.Target == target).Attack?.BaseDamage ?? 0f;
		await Until(() => Spear.HasSettled);
		Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false);
		Check(await Until(() => !Spear.InFlight), "金光大道不改变武技2手动回手");
		return damage;
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Spear;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await Cleave();
			float normalThrow = await ThrowDamage(false), enhancedThrow = await ThrowDamage(true);
			Check(normalThrow > 0f && Mathf.IsEqualApprox(normalThrow, enhancedThrow), $"只改下劈：真实武技2伤害保持{normalThrow:0.###}→{enhancedThrow:0.###}");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[SpearCleave] === 金光大道验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Cleave()
	{
		await Equip();
		var near = Target("BaselineNear", new Vector3(20, 0.9f, 18));
		var far = Target("BaselineFar", new Vector3(20, 0.9f, 15.75f));
		await Wait(0.1);
		for (int i = 0; i < 4; i++) await Tap();
		Check(_hits.Where(h => h.Target == near).Select(h => h.Attack.ComboStep).SequenceEqual(new[] { 0, 1, 2, 3 }), "默认四连各段真实命中，段号0/1/2/3");
		Check(!_hits.Any(h => h.Target == far && h.Attack.ComboStep == 2), "默认下劈3.6m够不到4.25m小靶");
		float baseline = _hits.Single(h => h.Target == near && h.Attack.ComboStep == 2).Attack.BaseDamage;
		await Equip(true);
		near = Target("CleaveNear", new Vector3(20, 0.9f, 18));
		far = Target("Extended4.25m", new Vector3(20, 0.9f, 15.75f));
		var outside = Target("Outside5.1m", new Vector3(20, 0.9f, 14.9f));
		var side = Target("SideWidthUnchanged", new Vector3(21, 0.9f, 18));
		await Wait(0.1); int strikes = Melee.StrikeCount; await Tap();
		Check(Melee.ComboStep == 2 && _hits.Count == 2 && _hits.All(h => h.Attack.ComboStep == 2 && h.Attack.ComboStepCount == 4), "首次真实ATK就是原第3段下劈，只命中前方内／延伸靶");
		Check(_hits.All(h => Mathf.IsEqualApprox(h.Attack.BaseDamage, baseline * 1.5f)), $"下劈独立伤害{baseline:0.###}→{_hits.LastOrDefault().Attack?.BaseDamage:0.###}，恰为1.5倍");
		Check(Mathf.IsEqualApprox(Melee.CurrentStepBoxSize.Z, 4.68f) && Mathf.IsEqualApprox(Melee.CurrentStepBoxSize.X, 1.2f) && Mathf.IsEqualApprox(Melee.CurrentStepBoxSize.Y, 1.8f), "只沿前方延长：3.6→4.68m，宽1.2／高1.8不变");
		Check(far.Health < far.MaxHealth && outside.Health == outside.MaxHealth && side.Health == side.MaxHealth, "延长后的4.25m小靶真实命中，5.1m／侧方小靶不命中");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DamageMultiplier"), 1f) && Mathf.IsEqualApprox(_player.Stats.Get("AttackRangeMultiplier"), 1f), "全局伤害／范围乘区均保持1");
		await Screenshot();
		for (int i = 0; i < 3; i++) { Press("weapon_skill_1", true); await Wait(0.01); Press("weapon_skill_1", false); await Wait(0.01); }
		await Wait(0.3); Check(Melee.StrikeCount == strikes + 1, "连点不会追加第四段或重复ATK");
		await Until(() => Melee.IsReady); await Tap();
		Check(Melee.ComboStep == 2 && Melee.StrikeCount == strikes + 2, "下一次施法仍原第3段下劈");
		await Equip(true); strikes = Melee.StrikeCount;
		Press("weapon_skill_1", true); Press("skill_1", true); await Wait(0.03); Press("weapon_skill_1", false); Press("skill_1", false);
		await Wait(0.35); Check(Melee.StrikeCount == strikes, "真实前摇被闪打断时不生成下劈伤害");
		await Equip(); near = Target("ResetNear", new Vector3(20, 0.9f, 18)); await Wait(0.1); await Tap();
		Check(_hits.Count == 1 && _hits[0].Attack.ComboStep == 0 && Mathf.IsEqualApprox(Melee.CurrentStepBoxSize.Z, 4f), "换装恢复普通首段及原4m判定，无单段／加长残留");
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = "Golden road: original cleave step 3/4 only\nReach 3.6 -> 4.68m | damage x1.5 | width unchanged", Position = new Vector3(20, 3, 17), FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label);
		_camera.GlobalPosition = new Vector3(26, 6, 25); _camera.LookAt(new Vector3(20, 0.8f, 17)); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng("res://docs/screenshots/j17_golden_road.png") == Error.Ok, "截图 j17_golden_road");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
}
