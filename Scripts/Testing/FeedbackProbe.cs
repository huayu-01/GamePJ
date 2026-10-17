using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Run;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>G专项：真实输入、动画、物理和生产预制体，禁止手推动画事件。</summary>
public partial class FeedbackProbe : Node
{
	private int _pass, _fail;
	private Node _base = null!;
	private Unit _player = null!, _target = null!;
	private Camera3D _camera = null!;
	private int _spearHits, _fallHits, _burstHits;
	public override async void _Ready()
	{
		try { await Run(); } catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[Feedback] === G专项验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Feedback] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 2)
	{
		ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
		while (!predicate() && Time.GetTicksMsec() < end) await Wait(0.01);
		return predicate();
	}
	private void InputAction(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Tap(string action) { InputAction(action, true); await Wait(0.04); InputAction(action, false); await Wait(0.025); }
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
	private async Task Equip(WeaponPresetKind weapon)
	{
		RunSetup.Character = PlayerCharacterKind.WeaponMaster;
		RunSetup.Weapon = weapon;
		await Wait(0.25);
		Aim();
		await Wait(0.1);
	}
	private static bool Safe(Node node)
	{
		if (node is Unit or CollisionObject3D || node.GetGroups().Count > 0) return false;
		foreach (Node child in node.GetChildren()) if (!Safe(child)) return false;
		return true;
	}
	private async Task Run()
	{
		await Wait(0.25);
		RunSetup.HasSelection = true;
		RunSetup.Character = PlayerCharacterKind.WeaponMaster;
		RunSetup.Weapon = WeaponPresetKind.Spear;
		_base = RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate();
		GetTree().Root.AddChild(_base);
		GetTree().CurrentScene = _base;
		await Wait(0.7);
		_player = _base.GetNode<Unit>("Player");
		_player.IsInvulnerable = true;
		_player.GetNode<Node3D>("CameraRig").SetPhysicsProcess(false);
		_camera = new Camera3D { Current = true, Fov = 50 };
		AddChild(_camera);
		_target = new Unit { Name = "FeedbackTarget", Faction = Faction.Enemy, MaxHealth = 100000, KnockbackResistance = 1 };
		_target.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.5f } });
		_base.AddChild(_target);
		_target.GlobalPosition = Vector3.Zero;
		_target.HitReceived += hit =>
		{
			string id = hit.Attack?.Ability ?? "";
			if (id == "skl_b3_w1_2") _spearHits++;
			if (id == "skl_b_fall") _fallHits++;
			if (id == "skl_b1_w1_3") _burstHits++;
		};
		Aim();
		await Wait(0.15);
		await CheckSpear();
		await CheckMelee();
		await CheckLanding();
		await CheckLoot();
	}
	private async Task CheckSpear()
	{
		var spear = _player.GetNode<SpearThrowAbility>("AbilityWeapon6Skill2");
		var combo = _player.GetNode<MeleeComboAbility>("AbilityWeapon6Skill1");
		spear.ResetForLoadout();
		InputAction("weapon_skill_2", true);
		await Wait(0.45);
		Check(spear.IsCharging && !spear.InFlight && spear.DamageTickCount == 0 && _spearHits == 0, "按住未满基础蓄力：不飞出、不判定、不命中");
		InputAction("weapon_skill_2", false);
		await Wait(0.08);
		Check(!spear.IsActive && !spear.InFlight && !combo.ExternallyBlocked && spear.LastReleaseAborted, "提前松手取消，武器门无残留");
		foreach (int steps in new[] { 0, 3, 4 })
		{
			spear.ResetForLoadout();
			Aim();
			InputAction("weapon_skill_2", true);
			float desired = spear.ChargeBaseSeconds + steps * spear.ChargeStepSeconds + 0.025f;
			Check(await Until(() => spear.ChargeTime >= desired, 3), $"真实按住蓄到{steps}档，时间={spear.ChargeTime:0.###}s");
			Check(!spear.InFlight && spear.DamageTickCount == 0, "蓄力期间仍未抛出");
			InputAction("weapon_skill_2", false);
			Check(await Until(() => spear.InFlight, 0.2), "松手后才抛出");
			float expected = spear.MaxThrowDistance * Mathf.Pow(spear.ChargeStepScale, steps);
			Check(Mathf.IsEqualApprox(spear.LastHorizontalDistance, expected) && Mathf.IsEqualApprox(spear.LastPowerScale, Mathf.Pow(spear.ChargeStepScale, steps)), $"{steps}档：距离={spear.LastHorizontalDistance:0.####}，速度={spear.LastHorizontalSpeed:0.####}，倍率={spear.LastPowerScale:0.####}");
			Vector3 start = spear.WeaponPosition;
			await Wait(0.18);
			Check(spear.InFlight && spear.WeaponPosition.DistanceTo(start) > 1.5f && combo.ExternallyBlocked, "真实飞行超过1.5米，未被起手近距离判据误收回，武技1已锁");
			if (steps == 4)
			{
				await Shot("g_spear_charged");
				Check(await Until(() => new Vector2(spear.WeaponPosition.X - start.X, spear.WeaponPosition.Z - start.Z).Length() > 20, 0.6), "四档真实水平飞行超过20米，不再被基础12米上限截断");
				Check(await Until(() => !spear.IsActive) && !combo.ExternallyBlocked, "最远距离自动收回并解锁");
			}
			else
			{
				await Tap("weapon_skill_2");
				Check(await Until(() => !spear.IsActive) && !combo.ExternallyBlocked, "再次按键收回并解锁武技1");
			}
		}
		spear.ResetForLoadout();
		InputAction("weapon_skill_2", true);
		await Wait(0.2);
		await Equip(WeaponPresetKind.SpiritSword);
		InputAction("weapon_skill_2", false);
		Check(!spear.IsActive && !spear.InFlight && !combo.ExternallyBlocked, "蓄力时换装取消旧技能，无飞行/锁门残留");
	}
	private async Task CheckMelee()
	{
		foreach (var pair in new[] { (WeaponPresetKind.SpiritSword, 4), (WeaponPresetKind.Scythe, 5), (WeaponPresetKind.Spear, 6) })
		{
			await Equip(pair.Item1);
			var ability = _player.GetNode<MeleeComboAbility>($"AbilityWeapon{pair.Item2}Skill1");
			var fx = ability.GetNode<MeleeSwingFx>("SwingFx");
			ability.ResetForLoadout();
			int count = ability.StrikeCount, effects = fx.SpawnCount;
			float health = _target.Health;
			await Tap("weapon_skill_1");
			Check(await Until(() => fx.SpawnCount > effects), $"{pair.Item1}真实ATK事件播放Battle特效");
			Check(ability.StrikeCount - count == 1 && fx.SpawnCount - effects == 1 && _target.Health < health, "一次真实判定=一次特效，战斗仍造成伤害");
			Node3D effect = fx.LastEffect!;
			Check(Safe(effect), "挥砍特效不含碰撞/单位/分组");
			await Wait(0.12);
			var material = (ShaderMaterial)effect.GetNode<MeshInstance3D>("Swing").MaterialOverride;
			Check(material.GetShaderParameter("swing_factor").AsSingle() > 0.05f, "当前可见的私有材质实际收到挥砍动画参数");
			if (pair.Item2 == 4)
			{
				_camera.GlobalPosition = new Vector3(4, 6, 7);
				_camera.LookAt(new Vector3(0, 0.8f, 1));
				_camera.ResetPhysicsInterpolation();
				await Wait(0.02);
				await Shot("g_melee_swing");
			}
			ability.ResetForLoadout();
			await Wait(0.6);
			Check(!IsInstanceValid(effect) && fx.SpawnCount == effects + 1, "一次性动画结束自毁，取消不额外播放");
		}
	}
	private async Task CheckLanding()
	{
		await Equip(WeaponPresetKind.SpiritSword);
		var fall = _player.GetNode<FallAttackAbility>("AbilityFallAttack");
		var burst = (LandingBurstAbility)Ability.FindByAbilityId(_player, "skl_b1_w1_3")!;
		burst.ResetForLoadout();
		int count = burst.TriggerCount;
		for (int i = 0; i < 3; i++)
		{
			if (i == 2) await Wait(burst.EffectiveCooldown + 0.1);
			fall.ResetForLoadout();
			_player.GlobalPosition = new Vector3(0, 4, 0);
			_player.Velocity = Vector3.Zero;
			_player.ResetPhysicsInterpolation();
			await Wait(0.06);
			await Tap("fall_attack");
			Check(await Until(() => fall.HasLanded), $"第{i + 1}次真实离地→下落→落地");
			await Wait(0.22);
			int expected = count + (i == 2 ? 2 : 1);
			Check(burst.TriggerCount == expected, $"落剑式次数={burst.TriggerCount}，预期={expected}（CD内不重触发，CD后可触发）");
		}
		Check(_fallHits > 0 && _burstHits > 0, $"真实物理伤害：落={_fallHits}次，落剑式={_burstHits}次");
	}
	private async Task CheckLoot()
	{
		Aim();
		RewardNode Make(RewardNodeKind kind, Vector3 position)
		{
			var node = RuntimeResources.Load<PackedScene>("res://Scenes/Interactables/RewardNode.tscn").Instantiate<RewardNode>();
			node.Kind = kind;
			_base.AddChild(node);
			node.GlobalPosition = position;
			return node;
		}
		var high = Make(RewardNodeKind.Evolution, new Vector3(-3, 0, 6));
		var high2 = Make(RewardNodeKind.Mutation, new Vector3(3, 0, 6));
		var mid = Make(RewardNodeKind.Enhancement, new Vector3(0, 0, 6));
		await Wait(0.7);
		var fx = high.GetNode<RewardBeaconFx>("BeaconFx");
		var fx2 = high2.GetNode<RewardBeaconFx>("BeaconFx");
		Check(fx.Effect is { Visible: true } && fx2.Effect is { Visible: true } && mid.GetNode<RewardBeaconFx>("BeaconFx").Effect == null, "Loot仅高价值节点实例化，中价值不堆叠光柱");
		Check(!high.GetNode<Node3D>("Aura").Visible && Safe(fx.Effect!), "高价值旧光圈让位，信标无碰撞/单位/分组");
		var m1 = (ShaderMaterial)fx.Effect!.GetNode<MeshInstance3D>("Ring_01").MaterialOverride;
		var m2 = (ShaderMaterial)fx2.Effect!.GetNode<MeshInstance3D>("Ring_01").MaterialOverride;
		Check(m1 != m2 && m1.GetShaderParameter("primary_color").AsColor().IsEqualApprox(high.PresentationColor.Lightened(0.25f)), "实例私有材质，颜色来自价值标签");
		_camera.GlobalPosition = new Vector3(7, 6, 14);
		_camera.LookAt(new Vector3(0, 0.6f, 6));
		_camera.ResetPhysicsInterpolation();
		await Shot("g_loot_beacons");
		high.Enabled = false;
		await Wait(0.08);
		Check(!fx.Effect.Visible && !fx.Effect.GetNode<GpuParticles3D>("GPUParticles3D2").Emitting && fx2.Effect.Visible, "禁用立即隐藏/停粒子，不污染另一实例");
		high.Enabled = true;
		await Wait(0.08);
		high.Disappear();
		await Wait(0.6);
		Check(high.Vanished && !fx.Effect.Visible, "交互物消失后信标不残留");
		Node3D effect = fx.Effect;
		high.QueueFree(); high2.QueueFree(); mid.QueueFree();
		await Wait(0.08);
		Check(!IsInstanceValid(effect), "奖励节点卸载回收全部特效");
	}
}
