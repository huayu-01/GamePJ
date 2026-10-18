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

/// <summary>J14：怜悯末刀模式与普渡末刀击杀的真实链路、范围、快照及来源过滤。</summary>
public partial class ScytheEvolutionProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private readonly List<AttackInstance> _hits = new();
	private readonly List<(int Step, int Count, string Ability)> _kills = new();
	private MeleeComboAbility Melee => (MeleeComboAbility)PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[ScytheEvolution] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void OnEvent(GameplayEvent evt)
	{
		if (evt.Kind == GameplayEventKind.Kill && evt.Source == _player) _kills.Add((evt.ComboStep, evt.ComboStepCount, evt.Tag));
	}
	private async Task Equip(WeaponPresetKind weapon = WeaponPresetKind.Scythe)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(action, false);
		RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, RunSetup.Character, weapon);
		_build = new RunBuild();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 0.95f, 26); _camera.LookAt(new Vector3(20, 0.95f, 0)); _camera.ResetPhysicsInterpolation();
		_hits.Clear(); _kills.Clear();
		await Wait(0.15);
	}
	private void Grant(string id)
	{
		var row = ConfigRows.ById<RewardConfig>(id);
		var def = RewardCatalog.Find(id);
		Check(row?.HasKnownHandler == true && def != null, $"真实表行／处理器有效：{id}");
		if (def == null) return;
		_build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(id), $"真实入构筑：{id}");
	}
	private Unit Target(string name, Vector3 position, float hp = 100000)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy, MaxHealth = hp, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.2f } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		unit.AddChild(new Label3D { Text = name, Position = Vector3.Up * 0.7f, FontSize = 40 });
		AddChild(unit); unit.GlobalPosition = position;
		unit.Damaged += (_, hit) => { if (hit.Attacker == _player && hit.Attack != null) _hits.Add(hit.Attack); };
		return unit;
	}
	private async Task Tap()
	{
		await Until(() => Melee.IsBusy || Melee.IsReady);
		int before = Melee.StrikeCount;
		Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
		Check(await Until(() => Melee.StrikeCount > before), $"真实ATK到达：段{Melee.ComboStep + 1}");
		// 等本刀表内伤害窗口关闭，再放下一刀的新靶；否则新靶会被上一刀仍生效的盒子杀死。
		await Wait(Melee.Series!.StepActiveTimes[Melee.ComboStep] + 0.04f);
	}
	private async Task Screenshot(string name, string caption)
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = caption, Position = _player.GlobalPosition + Vector3.Up * 2.5f, FontSize = 48 };
		AddChild(label);
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(6, 5, 7); _camera.LookAt(_player.GlobalPosition); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Scythe;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GameplayEventBus.Raised += OnEvent;
			await Mercy();
			await Salvation();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GameplayEventBus.Raised -= OnEvent;
		GD.Print($"[ScytheEvolution] === 镰刀末刀进化验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Mercy()
	{
		await Equip();
		var target = Target("NormalFront", new Vector3(20, 0.9f, 18.5f));
		await Wait(0.1);
		for (int i = 0; i < 3; i++) await Tap();
		Check(_hits.Count == 3 && _hits.Select(a => a.ComboStep).SequenceEqual(new[] { 0, 1, 2 }) && _hits.All(a => a.ComboStepCount == 3), "默认三连真实命中，快照段号0/1/2、总段数3");
		float final = _hits.Last().BaseDamage;
		target.QueueFree();
		await Equip();
		float global = _player.Stats.Get("DamageMultiplier");
		Grant("evo_mercy");
		var front = Target("MercyFront", new Vector3(20, 0.9f, 18.5f));
		var back = Target("MercyBack", new Vector3(20, 0.9f, 21.7f));
		await Wait(0.1);
		int strikes = Melee.StrikeCount;
		await Tap();
		Check(Melee.ComboStep == 2 && _hits.Count == 2 && _hits.All(a => a.ComboStep == 2 && a.ComboStepCount == 3), "怜悯首次真实ATK就是原末段，360度判定同时命中前／后靶");
		Check(_hits.All(a => Mathf.IsEqualApprox(a.BaseDamage, final * 1.5f)), $"怜悯末刀伤害{_hits.LastOrDefault()?.BaseDamage:0.###}＝原末刀{final:0.###}×1.5");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DamageMultiplier"), global), "怜悯不改玩家全局伤害乘区");
		for (int i = 0; i < 3; i++) { Press("weapon_skill_1", true); await Wait(0.01); Press("weapon_skill_1", false); await Wait(0.01); }
		await Wait(0.3);
		Check(Melee.StrikeCount == strikes + 1, "单次施法内连点不追加前两段，也不重复末刀ATK");
		await Screenshot("j14_mercy", $"Mercy: original step 3/3 only\nDamage {final * 1.5f:0.##} | front + back hit | global unchanged");
		await Until(() => Melee.IsReady);
		await Tap();
		Check(Melee.ComboStep == 2 && Melee.StrikeCount == strikes + 2, "下次真实施法仍从末刀开始");
		front.QueueFree(); back.QueueFree();
		await Equip();
		Grant("evo_mercy");
		strikes = Melee.StrikeCount;
		Press("weapon_skill_1", true); Press("skill_1", true); await Wait(0.03); Press("weapon_skill_1", false); Press("skill_1", false);
		await Wait(0.35);
		Check(Melee.StrikeCount == strikes, "怜悯仍服从真实前摇打断，没有计时伤害兜底");
		await Equip();
		target = Target("ResetFront", new Vector3(20, 0.9f, 18.5f)); await Wait(0.1); await Tap();
		Check(_hits.Count == 1 && _hits[0].ComboStep == 0, "重新配装后恢复普通首段，末刀模式无残留");
		target.QueueFree();
	}
	private async Task Salvation()
	{
		await Equip();
		Grant("evo_salvation");
		var enemy = Target("InjurySource", new Vector3(-20, 0.9f, 20));
		_player.ReceiveHit(new AttackInstance { Attacker = enemy, BaseDamage = 100f, Ability = "probe_injury" }.RollHit(_player, Vector3.Zero, _player.GlobalPosition));
		float hp = _player.Health;
		Check(hp < _player.MaxHealth - 32f, $"真实受击后生命{hp:0.###}，留出回血空间");
		var first = Target("FirstKill", new Vector3(20, 0.9f, 18.5f), 1); await Wait(0.1); await Tap();
		Check(first.IsDead && Mathf.IsEqualApprox(_player.Health, hp) && _kills.Last().Step == 0, "普渡：第一刀真实击杀不回血，击杀事件段号0");
		AttackInstance firstSnapshot = _hits.First();
		var second = Target("SecondKill", new Vector3(20, 0.9f, 18.5f), 1); await Wait(0.05); await Tap();
		Check(second.IsDead && Mathf.IsEqualApprox(_player.Health, hp) && _kills.Last().Step == 1, "普渡：第二刀真实击杀不回血，击杀事件段号1");
		var finalFront = Target("FinalFront", new Vector3(20, 0.9f, 18.5f), 1);
		var finalBack = Target("FinalBack", new Vector3(20, 0.9f, 21.7f), 1);
		await Wait(0.05); await Tap();
		Check(finalFront.IsDead && finalBack.IsDead && Mathf.IsEqualApprox(_player.Health, hp + 16f), $"普渡：同一末刀真实击杀两靶，生命{hp:0.###}→{_player.Health:0.###}，每靶+8");
		Check(_kills.TakeLast(2).All(k => k.Step == 2 && k.Count == 3 && k.Ability == "skl_b2_w1_1"), "末刀击杀事件携带冻结的2/3段快照和正确技能来源");
		await Screenshot("j14_salvation", $"Salvation: last-step kills only\n2 kills = +16 HP | step 1/2 kills = +0");
		float before = _player.Health;
		var late = Target("LateFirstStep", new Vector3(20, 8, 5), 1); await Wait(0.05);
		var projectile = new Projectile { Speed = 10f }; AddChild(projectile); projectile.GlobalPosition = new Vector3(20, 8, 10); projectile.Launch(firstSnapshot, Vector3.Forward);
		Check(await Until(() => late.IsDead) && Mathf.IsEqualApprox(_player.Health, before) && Melee.ComboStep == 2,
			"第一刀旧快照经真实弹体延迟击杀：当前连招已到末段仍不回血");
		foreach (var item in new[] { ("skl_b2_w1_2", _player, 2, 3), ("skl_b2_w1_1", enemy, 2, 3), ("skl_b2_w1_1", _player, -1, 0) })
		{
			var victim = Target("WrongSource", new Vector3(-15, 1, 0), 1);
			victim.ReceiveHit(new AttackInstance { Attacker = item.Item2, Ability = item.Item1, BaseDamage = 20f, ComboStep = item.Item3, ComboStepCount = item.Item4 }.RollHit(victim, Vector3.Zero, victim.GlobalPosition));
			Check(victim.IsDead && Mathf.IsEqualApprox(_player.Health, before), $"实际击杀过滤：来源{item.Item1}／是否本人{item.Item2 == _player}／段{item.Item3}/{item.Item4}不触发普渡");
			victim.QueueFree();
		}
		await Until(() => Melee.IsReady);
		Grant("evo_mercy");
		var together = Target("MercySalvation", new Vector3(20, 0.9f, 18.5f), 1); await Wait(0.1); await Tap();
		Check(together.IsDead && Melee.ComboStep == 2 && Mathf.IsEqualApprox(_player.Health, before + 8f), "怜悯＋普渡同时获得：跳到末刀的真实击杀仍回8血");
		foreach (var unit in new[] { enemy, first, second, finalFront, finalBack, late, together }) if (IsInstanceValid(unit)) unit.QueueFree();
	}
}
