using System;
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

/// <summary>真实输入和动画创建快照，再通过统一RollHit与ReceiveHit验证暴击结算。</summary>
public partial class AttackSnapshotProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!, _target = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private Projectile? _latestProjectile;
	private void Added(Node node) { if (node is Projectile projectile) _latestProjectile = projectile; }
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[AttackSnapshot] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds = 0.1) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> condition, double timeout = 5)
	{
		double elapsed = 0;
		while (!condition() && elapsed < timeout) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); elapsed += GetProcessDeltaTime(); }
		return condition();
	}
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Equip(WeaponPresetKind weapon)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "fall_attack" }) Press(action, false);
		var character = weapon is WeaponPresetKind.EnergyPalm or WeaponPresetKind.Crossbow or WeaponPresetKind.Bow ? PlayerCharacterKind.Hunter : PlayerCharacterKind.WeaponMaster;
		_loadout.Apply(_player, this, character, weapon);
		_player.Stats.SetBase("DamageMultiplier", 1.5f);
		_player.Stats.SetBase("CritChanceAdd", 1f);
		_player.Stats.SetBase("CritMultiplierAdd", 1f);
		_player.GlobalPosition = new Vector3(20f, 0.05f, 20f); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20f, 2f, 26f); _camera.LookAt(new Vector3(20f, 0.9f, 0f)); _camera.ResetPhysicsInterpolation();
		await Wait(0.2);
	}
	private async Task<AttackInstance> Fire(Ability ability)
	{
		var hitbox = ability.GetNode<AttackHitbox>("Attack");
		var before = hitbox.CurrentAttack;
		Press(ability.InputAction, true);
		if (ability.HoldToCharge) await Until(() => ability.IsCharging && ability.ChargeTime >= ability.ChargeBaseSecondsEffective + 0.01f);
		else await Wait(0.03);
		Press(ability.InputAction, false);
		if (!await Until(() => hitbox.CurrentAttack != null && !ReferenceEquals(before, hitbox.CurrentAttack))) throw new InvalidOperationException($"{ability.AbilityId} 未由真实动画生成新攻击");
		return hitbox.CurrentAttack!;
	}
	private void Verify(AttackInstance attack, string label)
	{
		Check(attack.CanCrit && Mathf.IsEqualApprox(attack.CritChance, 1f) && Mathf.IsEqualApprox(attack.CritMultiplier, 3f),
			$"{label}快照允许暴击并合并玩家属性：CanCrit={attack.CanCrit}／率{attack.CritChance:0.###}／倍率{attack.CritMultiplier:0.###}");
		var hit = attack.RollHit(_target, Vector3.Forward, _target.GlobalPosition);
		float health = _target.Health;
		_target.ReceiveHit(hit);
		Check(hit.Critical && Mathf.Abs(health - _target.Health - attack.BaseDamage * 3f) < 0.01f,
			$"{label}真实结算100%暴击：基础{attack.BaseDamage:0.###}／扣血{health - _target.Health:0.###}");
	}
	private async Task<AttackInstance> FireRanged(Ability ability)
	{
		_latestProjectile = null;
		Press(ability.InputAction, true);
		if (ability.HoldToCharge) await Until(() => ability.IsCharging && ability.ChargeTime >= ability.ChargeBaseSecondsEffective + 0.01f);
		else await Wait(0.03);
		Press(ability.InputAction, false);
		if (!await Until(() => _latestProjectile != null && IsInstanceValid(_latestProjectile) && _latestProjectile.CurrentAttack != null)) throw new InvalidOperationException($"{ability.AbilityId} 未发射真实投射物");
		return _latestProjectile!.CurrentAttack!;
	}
	public override async void _Ready()
	{
		try
		{
			await Until(() => RunController.CharacterRow(PlayerCharacterKind.WeaponMaster) != null);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GetTree().NodeAdded += Added;
			_target = new Unit { Faction = Faction.Enemy, MaxHealth = 100000f }; AddChild(_target); _target.GlobalPosition = new Vector3(70f, 5f, 70f);
			foreach (var weapon in new[] { WeaponPresetKind.EnergyPalm, WeaponPresetKind.Crossbow, WeaponPresetKind.Bow })
			{
				await Equip(weapon);
				var ability = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
				Verify(await FireRanged(ability), $"{weapon}真实远程弹体");
			}
			foreach (var weapon in new[] { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
			{
				await Equip(weapon);
				Check(Mathf.IsEqualApprox(_player.Stats.GetBase("BaseCritChance"), 0.05f) && Mathf.IsEqualApprox(_player.Stats.GetBase("BaseCritMultiplier"), 2f), $"{weapon}武器表统一基础5%／2倍");
				var melee = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
				Verify(await Fire(melee), $"{weapon}近战");
			}
			await Equip(WeaponPresetKind.SpiritSword);
			Verify(await FireRanged(PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_2")!), "拔刀式真实剑气");
			foreach (var weapon in new[] { WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
			{
				await Equip(weapon);
				var ability = (WeaponThrowAbility)PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_2")!;
				var attack = await Fire(ability);
				Verify(attack, $"{weapon}投掷");
				float expected = ability.GetNode<AttackHitbox>("Attack").Damage * _player.Stats.Get("DamageMultiplier")
					* (ability is SpearThrowAbility spear ? spear.LastPowerScale : 1f);
				Check(Mathf.IsEqualApprox(attack.BaseDamage, expected), $"{weapon}投掷合并一次玩家攻击力：{attack.BaseDamage:0.###}／期望{expected:0.###}");
				_player.Stats.SetBase("DamageMultiplier", 2f); _player.Stats.SetBase("CritChanceAdd", -1f); _player.Stats.SetBase("CritMultiplierAdd", 5f);
				if (ability is ScytheThrowAbility) await Wait(0.65);
				else { Press("weapon_skill_2", true); await Wait(0.03); Press("weapon_skill_2", false); await Wait(0.15); }
				var later = ability.GetNode<AttackHitbox>("Attack").CurrentAttack!;
				Check(Mathf.IsEqualApprox(later.BaseDamage, attack.BaseDamage) && Mathf.IsEqualApprox(later.CritChance, 1f) && Mathf.IsEqualApprox(later.CritMultiplier, 3f), $"{weapon}后续周期／回程沿用出手属性，不重读后来的增益");
			}
			await Equip(WeaponPresetKind.SpiritSword);
			Verify(await Fire(_player.GetNode<BlinkDashAbility>("AbilityBSkill1")), "闪");
			await Equip(WeaponPresetKind.SpiritSword);
			_player.GlobalPosition = new Vector3(20f, 5f, 20f); await Wait(0.03);
			var fall = _player.GetNode<FallAttackAbility>("AbilityFallAttack");
			var fallAttack = await Fire(fall); Verify(fallAttack, "下落攻击");
			_player.Stats.SetBase("CritChanceAdd", -1f); _player.Stats.SetBase("CritMultiplierAdd", 5f);
			var burst = _player.GetNode<LandingBurstAbility>("AbilityPassive2").GetNode<AttackHitbox>("Attack");
			Check(await Until(() => burst.CurrentAttack != null), "真实World落地生成落剑式");
			Verify(burst.CurrentAttack!, "落剑式继承下冲出手快照");
			await TableAndLimits();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().NodeAdded -= Added;
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "fall_attack" }) Press(action, false);
		GD.Print($"[AttackSnapshot] === 攻击快照验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task TableAndLimits()
	{
		var row = RunController.WeaponRow(WeaponPresetKind.SpiritSword)!;
		float chance = row.CritChance, multiplier = row.CritMultiplier;
		try
		{
			row.CritChance = 0.2f; row.CritMultiplier = 2.5f;
			await Equip(WeaponPresetKind.SpiritSword);
			_player.Stats.SetBase("CritChanceAdd", 0f); _player.Stats.SetBase("CritMultiplierAdd", 0f);
			var offense = AttackOffense.Capture(_player);
			Check(Mathf.IsEqualApprox(_player.Stats.GetBase("BaseCritChance"), 0.2f)
				&& Mathf.IsEqualApprox(offense.CritChance, Mathf.Clamp(0.2f + _player.Stats.Get("CritChanceAdd"), 0f, 1f))
				&& Mathf.IsEqualApprox(offense.CritMultiplier, 2.5f),
				$"只改当前武器行就统一修改基础暴击／暴伤，仍合并局外加成：{offense.CritChance:0.###}／{offense.CritMultiplier:0.###}");
			_player.Stats.SetBase("CritChanceAdd", -1f); _player.Stats.SetBase("DamageMultiplier", 0f);
			offense = AttackOffense.Capture(_player);
			Check(offense.CritChance == 0f && offense.DamageMultiplier == 0f, "负暴击夹到0，伤害倍率0保留有效值");
			var attack = await Fire(PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!);
			Check(attack.BaseDamage == 0f && attack.CritChance == 0f && !attack.RollHit(_target, Vector3.Zero, _target.GlobalPosition).Critical, "真实近战0倍率无伤害、0暴击不掷出暴击");
		}
		finally { row.CritChance = chance; row.CritMultiplier = multiplier; }
		await Equip(WeaponPresetKind.SpiritSword);
		var weapon = new Weapon { Damage = 10f, ProjectileScene = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Arrow.tscn") };
		_player.AddChild(weapon); _latestProjectile = null;
		Check(weapon.TryFire(weapon.GlobalPosition + Vector3.Forward * 10f), "武器组件仍能真实生成弹体");
		Verify(_latestProjectile!.CurrentAttack!, "玩家武器组件");
		weapon.QueueFree();
		var build = new RunBuild();
		build.Add(RewardCatalog.Find("evo_golden_qi")!, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
		_latestProjectile = null;
		var melee = await Fire(PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!);
		Verify(_latestProjectile!.CurrentAttack!, "金光术附加剑气");
		Check(Mathf.IsEqualApprox(_latestProjectile.CurrentAttack!.BaseDamage, melee.BaseDamage), "附加剑气继承当段基础伤害，攻击倍率不重复相乘");
		var hitbox = new AttackHitbox { Damage = 10f, CritChance = 0f }; AddChild(hitbox);
		hitbox.Activate(_player);
		Verify(hitbox.CurrentAttack!, "玩家通用命中盒默认路径");
		Check(Mathf.IsEqualApprox(hitbox.CurrentAttack!.BaseDamage, 15f), "通用玩家命中盒只乘一次攻击力");
		var enemy = new Unit { Faction = Faction.Enemy }; AddChild(enemy);
		enemy.Stats.SetBase("DamageMultiplier", 3f); enemy.Stats.SetBase("CritChanceAdd", 1f);
		hitbox.Activate(enemy);
		Check(hitbox.CurrentAttack!.BaseDamage == 10f && !hitbox.CurrentAttack.CanCrit && hitbox.CurrentAttack.CritChance == 0f, "敌人通用命中盒保持原始伤害／暴击语义");
		hitbox.QueueFree(); enemy.QueueFree();
	}
}
