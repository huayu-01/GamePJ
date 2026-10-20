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

/// <summary>J13：真实入构筑、真实按键/ATK、真实剑气和真实落地验收。</summary>
public partial class SpiritSwordEvolutionProbe : Node3D
{
	private int _pass, _fail, _spawns;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private MeleeComboAbility Melee => (MeleeComboAbility)PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
	private SpiritSwordsAbility Draw => (SpiritSwordsAbility)PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_2")!;
	private FallAttackAbility Fall => _player.GetNode<FallAttackAbility>("AbilityFallAttack");
	private LandingBurstAbility Burst => _player.GetNode<LandingBurstAbility>("AbilityPassive2");
	private readonly List<(string Target, string Ability, float Damage, float BaseDamage, bool Qi)> _hits = new();
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[SwordEvolution] {(ok ? "PASS" : "FAIL")} {text}");
	}
	private void Added(Node node) { if (node is Projectile) _spawns++; }
	private async Task Equip(WeaponPresetKind weapon = WeaponPresetKind.SpiritSword)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "fall_attack" }) Press(action, false);
		await Until(() => Projectile.LiveCount == 0, 4);
		RunSetup.Character = PlayerCharacterKind.WeaponMaster;
		RunSetup.Weapon = weapon;
		_loadout.Apply(_player, this, RunSetup.Character, weapon);
		_build = new RunBuild();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20);
		_player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 0.95f, 26);
		_camera.LookAt(new Vector3(20, 0.95f, 0));
		_camera.ResetPhysicsInterpolation();
		await Wait(0.2);
	}
	private void Grant(string id)
	{
		var row = ConfigRows.ById<RewardConfig>(id);
		var definition = RewardCatalog.Find(id);
		Check(row?.HasKnownHandler == true && definition != null, $"真实表行与处理器可读：{id}");
		if (definition == null) return;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(id), $"真实 RunBuild.Add 已获得 {id}");
	}
	private Unit Target(string name, Vector3 position, float radius = 0.2f)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy, MaxHealth = 100000, KnockbackResistance = 1 };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = radius } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		unit.AddChild(new Label3D { Text = name, Position = Vector3.Up * 0.7f, FontSize = 40 });
		AddChild(unit);
		unit.GlobalPosition = position;
		unit.Damaged += (result, hit) => _hits.Add((name, hit.Attack?.Ability ?? "", result.FinalDamage, hit.Attack?.BaseDamage ?? 0, hit.Attack?.Tags.Contains("SwordQi") == true));
		return unit;
	}
	private async Task Screenshot(string name, string caption)
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = caption, Position = _player.GlobalPosition + Vector3.Up * 2.5f, FontSize = 48 };
		AddChild(label);
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(6, 5, 7);
		_camera.LookAt(_player.GlobalPosition + new Vector3(0, 0.4f, -2));
		_camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
		label.QueueFree();
		_camera.GlobalTransform = before;
		_camera.ResetPhysicsInterpolation();
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true;
			RunSetup.Character = PlayerCharacterKind.WeaponMaster;
			RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
			AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			GetTree().NodeAdded += Added;
			await GoldenQi();
			await GreatDraw();
			await DivineDescent();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().NodeAdded -= Added;
		GD.Print($"[SwordEvolution] === 灵剑进化验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task TapMelee()
	{
		await Until(() => Melee.IsBusy || Melee.IsReady, 4);
		int strikes = Melee.StrikeCount;
		Press("weapon_skill_1", true);
		await Wait(0.02);
		Press("weapon_skill_1", false);
		Check(await Until(() => Melee.StrikeCount > strikes), $"真实近战 ATK 到达，第 {Melee.ComboStep + 1} 段");
	}
	private async Task GoldenQi()
	{
		await Equip();
		int start = _spawns;
		await TapMelee();
		await Until(() => !Melee.IsBusy);
		Check(_spawns == start && Melee.ExtraProjectileCount == 0, "默认未获金光术：真实近战ATK不出额外弹体");
		await Equip();
		Grant("evo_golden_qi");
		start = _spawns;
		// 同帧输入后来的闪，真实前台交接必须在ATK之前取消近战。
		Press("weapon_skill_1", true); Press("skill_1", true);
		await Wait(0.03);
		Press("weapon_skill_1", false); Press("skill_1", false);
		await Wait(0.35);
		Check(_spawns == start && Melee.ExtraProjectileCount == 0, "金光术前摇被真实闪打断：无ATK就无额外剑气");
		await Equip();
		Grant("evo_golden_qi");
		var target = Target("QiFar", new Vector3(20, 0.9f, 5));
		await Wait(0.1);
		start = _spawns;
		int hitStart = _hits.Count;
		for (int i = 0; i < 5; i++)
		{
			await TapMelee();
			Check(Melee.ComboStep == i && Melee.ExtraProjectileCount == i + 1 && _spawns == start + i + 1,
				$"金光术第{i + 1}段：真实ATK恰好额外一发，累计{Melee.ExtraProjectileCount}");
			if (i == 2) await Screenshot("j13_golden_qi", "Golden Qi: 1 real ATK = 1 projectile\n3 of 5 combo steps fired");
		}
		await Wait(0.6);
		var hits = _hits.Skip(hitStart).Where(h => h.Qi).ToArray();
		Check(hits.Length == 5 && hits.All(h => h.Target == "QiFar" && h.Ability == "skl_b1_w1_1"), $"五段剑气真实命中远靶{hits.Length}次，来源均为破剑式");
		var box = Melee.GetNode<AttackHitbox>("Attack");
		for (int i = 0; i < Math.Min(5, hits.Length); i++)
		{
			float expected = box.Damage * Melee.Series!.StepDamageScales[i] * _player.Stats.Get("DamageMultiplier");
			Check(Mathf.IsEqualApprox(hits[i].BaseDamage, expected), $"剑气第{i + 1}段伤害快照{hits[i].BaseDamage:0.###}＝当段{expected:0.###}，玩家倍率只乘一次");
		}
		Check(_hits.Skip(hitStart).All(h => h.Qi), "远靶超出近战盒：受伤来自真实额外剑气");
		target.QueueFree();
		await Equip();
		Grant("evo_golden_qi");
		start = _spawns;
		await TapMelee();
		Check(_spawns == start + 1, "挥空也按真实ATK出一道剑气，不依赖命中事件");
		await Equip(WeaponPresetKind.Scythe);
		start = _spawns;
		await TapMelee();
		Check(_spawns == start && Melee.ExtraProjectileCount == 0, "换成镰刀后无金光术残留，不污染共用近战类");
	}
	private async Task GreatDraw()
	{
		// J42：这部分改验灵能汲取；金光术与神降术的原验收继续保留。
		await Equip(); Grant("evo_golden_qi");
		var target = Target("QiFar", new Vector3(20, .9f, 5));
		await TapMelee(); await Wait(.4);
		float before = _hits.Last(h => h.Qi).BaseDamage;
		float global = _player.Stats.Get("DamageMultiplier");
		Grant("evo_great_draw");
		Check(Mathf.IsEqualApprox(Draw.KillExtensionSeconds, 2f), "灵能汲取配置真实落到三剑技能");
		Check(Mathf.IsEqualApprox(_player.Stats.Get("DamageMultiplier"), global), "灵能汲取不改变全局伤害");
		await Until(() => !Melee.IsBusy);
		await TapMelee(); await Wait(.4);
		Check(Mathf.IsEqualApprox(before, _hits.Last(h => h.Qi).BaseDamage), "金光术剑气不吃旧大拔刀术的伤害倍率");
		Press("weapon_skill_2", true); await Wait(.03); Press("weapon_skill_2", false);
		Check(Draw.Summoned && Draw.SwordCount == 3, "三剑即时召唤，不发射旧拔刀剑气");
		Check(Draw.CooldownRemaining > 14f && !Draw.IsBusy, "召唤后立即起算冷却且不占前台");
		float remaining = Draw.RemainingSeconds;
		target.ForceKill(_player);
		Check(Mathf.IsEqualApprox(Draw.RemainingSeconds, remaining + 2f), "真实本人敌对击杀增加2秒持续时间");
		Check(Draw.ExtensionCount == 1, "同一次死亡只增加一次续时");
		target.QueueFree();
		await Until(() => !Melee.IsBusy); await TapMelee();
		Check(Draw.LastSwordAttack?.Ability == "skl_b1_w1_2", "三剑独立技能归属，不冒用金光术");
		Check(Draw.FollowCount == 1, "真实近战ATK触发一次三剑协同");
		Check(Mathf.IsEqualApprox(Draw.LastSwordAttack!.BaseDamage, Melee.GetNode<AttackHitbox>("Attack").CurrentAttack!.BaseDamage * .3f), "灵能汲取保留三剑原始伤害比例");
		await Until(() => !Draw.Following);
		Check(!Draw.SwordsReady, "挥击后缓慢回位，未立即恢复攻击");
		Check(await Until(() => Draw.SwordsReady), "完全归位后才能再次协同");
		await Equip();
		Check(!Draw.Summoned && Draw.SwordCount == 0 && Draw.KillExtensionSeconds == 0f, "重配装清理剑体和续时");
		Check(Draw.SwordDamageScale == .3f && Draw.DurationSeconds == 10f, "重配装保留新基线，不残留旧增强");
	}
	private async Task FallOnce()
	{
		await Until(() => Fall.IsReady, 4);
		_player.GlobalPosition = new Vector3(20, 4, 20);
		_player.Velocity = Vector3.Zero;
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		_player.GlobalPosition = new Vector3(20, 4, 20);
		_player.Velocity = Vector3.Zero;
		Press("fall_attack", true);
		Check(await Until(() => Fall.IsActive && !Fall.HasLanded), "真实下落按键已进入本次起手");
		Press("fall_attack", false);
		Check(await Until(() => Fall.HasLanded && _player.Movement!.IsGrounded), "下落经过真实Action并碰到World地面");
		await Wait(0.05);
	}
	private async Task DivineDescent()
	{
		await Equip();
		var near = Target("BurstNear", new Vector3(22.5f, 0.35f, 20), 0.15f);
		var outer = Target("BurstOuter", new Vector3(24.2f, 0.35f, 20), 0.15f);
		await Wait(0.1);
		int start = _hits.Count, triggers = Burst.TriggerCount;
		await FallOnce();
		Check(Burst.TriggerCount == triggers + 1 && Mathf.IsEqualApprox(Burst.BurstRadius, 3.2f), "未获神降术：真实落地额外一圈，半径3.2");
		Check(_hits.Skip(start).Any(h => h.Target == "BurstNear" && h.Ability == "skl_b1_w1_3")
			&& !_hits.Skip(start).Any(h => h.Target == "BurstOuter" && h.Ability == "skl_b1_w1_3"), "基线内圈靶命中，4.2m外圈靶不被额外一圈命中");
		float commonScale = Fall.DamageMultiplierForHeight(4f);
		Grant("evo_divine_descent");
		Check(Mathf.IsEqualApprox(Burst.BurstRadius, 4.8f) && Mathf.IsEqualApprox(Burst.Cooldown, 2f), "神降术真实碰撞半径4.8，技能冷却2秒");
		var fresh = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		Check(Mathf.IsEqualApprox(((SphereShape3D)fresh.GetNode<CollisionShape3D>("AbilityPassive2/Attack/Shape").Shape!).Radius, 3.2f), "神降术未改共用场景球形资源，新实例仍为3.2");
		fresh.Free();
		await Until(() => Burst.IsReady, 6);
		start = _hits.Count; triggers = Burst.TriggerCount;
		await FallOnce();
		Check(Burst.TriggerCount == triggers + 1 && _hits.Skip(start).Any(h => h.Target == "BurstOuter" && h.Ability == "skl_b1_w1_3"), "神降术真实落地命中4.2m外圈靶，范围已实际扩大");
		Check(Mathf.IsEqualApprox(Burst.LastBurstDamage, Fall.CurrentBasisDamage * 1.6f), $"额外落地圈伤害{Burst.LastBurstDamage:0.###}＝本次上游{Fall.CurrentBasisDamage:0.###}×1.6");
		Check(Mathf.IsEqualApprox(Fall.DamageMultiplierForHeight(4f), commonScale), "神降术未改共通下落攻击伤害公式");
		await Screenshot("j13_divine_descent", $"Divine Descent: radius {Burst.BurstRadius:0.##}m\nOuter target 4.2m hit | damage {Burst.LastBurstDamage:0.##} | CD 2s");
		await Wait(0.2);
		await FallOnce();
		Check(Burst.TriggerCount == triggers + 1, "2秒内再次真实落地：额外落地圈被真实冷却拦住");
		await Until(() => Burst.IsReady, 4);
		await FallOnce();
		Check(Burst.TriggerCount == triggers + 2, "2秒冷却经过后真实落地再次触发额外一圈");
		near.QueueFree(); outer.QueueFree();
		await Equip(WeaponPresetKind.Scythe);
		Check(!Burst.Enabled && Mathf.IsEqualApprox(Burst.BurstRadius, 3.2f), "换镰刀后额外落剑式关闭且形状恢复3.2");
	}
}
