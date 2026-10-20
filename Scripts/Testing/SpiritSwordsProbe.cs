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

/// <summary>三剑真实输入、五段动画轨迹、外圈物理伤害、归位门控、击杀续时及换装清理。</summary>
public partial class SpiritSwordsProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private SpiritSwordsAbility Swords => (SpiritSwordsAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
	private MeleeComboAbility Melee => (MeleeComboAbility)_player.AbilitySlots.Resolve("weapon_skill_1")!;
	private readonly List<AttackInstance> _hits = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[SpiritSwords] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Wait(float seconds) { float time = 0; while (time < seconds) { await Frame(); time += (float)GetProcessDeltaTime(); } }
	private async Task<bool> Until(Func<bool> condition, float timeout = 4f)
	{
		float time = 0; while (!condition() && time < timeout) { await Frame(); time += (float)GetProcessDeltaTime(); }
		return condition();
	}
	private async Task Equip(WeaponPresetKind weapon = WeaponPresetKind.SpiritSword)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(action, false);
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.WeaponMaster, weapon);
		_player.GlobalPosition = new Vector3(20, .05f, 20); _player.Rotation = Vector3.Zero; _player.Velocity = Vector3.Zero;
		_player.Stats.SetBase("DamageMultiplier", 1.5f); _player.Stats.SetBase("CritChanceAdd", 1f); _player.Stats.SetBase("CritMultiplierAdd", 1f);
		_camera.GlobalPosition = new Vector3(27, 5, 28); _camera.LookAt(new Vector3(20, 1.6f, 20)); _camera.ResetPhysicsInterpolation();
		await Wait(.2f);
	}
	private async Task Summon()
	{
		Press("weapon_skill_2", true); await Frame(); await Frame(); Press("weapon_skill_2", false);
		Check(await Until(() => Swords.Summoned), "真实右键召唤成功");
	}
	private async Task Attack()
	{
		int before = Melee.StrikeCount;
		Press("weapon_skill_1", true); await Frame(); await Frame(); Press("weapon_skill_1", false);
		Check(await Until(() => Melee.StrikeCount > before), "真实近战ATK到达");
	}
	private Unit Target(Vector3 position, float health = 100000f, Faction faction = Faction.Enemy)
	{
		var unit = new Unit { Faction = faction, MaxHealth = health, KnockbackResistance = 1f };
		unit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .22f } });
		unit.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = .22f, Height = .44f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		AddChild(unit); unit.GlobalPosition = position;
		unit.HitReceived += hit => { if (hit.Attack is { } attack) _hits.Add(attack); };
		return unit;
	}
	private void Kill(Unit target, Unit? owner = null)
	{
		var attack = new AttackInstance { Attacker = owner ?? _player, Ability = "probe_kill", BaseDamage = 100000f };
		target.ReceiveHit(attack.RollHit(target, Vector3.Zero, target.GlobalPosition));
	}
	private void Grant()
	{
		var build = new RunBuild();
		build.Add(RewardCatalog.Find("evo_great_draw")!, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
		Check(build.Has("evo_great_draw") && Mathf.IsEqualApprox(Swords.KillExtensionSeconds, 2f), "真实奖励目录和RunBuild获得灵能汲取，每杀延长2秒");
		var row = ConfigRows.ById<RewardConfig>("evo_great_draw")!;
		Check(row.DisplayName == "灵能汲取" && ConfigText.Format(row.DescriptionTemplate, row, row.Params).Contains("2秒"), "新名称与说明读取同一个续时数值");
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"渲染截图{name}");
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(.15f); RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await Equip(); await Summon();
			Check(Swords.SwordCount == 3 && Swords.SwordsReady && Swords.DurationSeconds == 10f && Swords.Cooldown == 15f, "三柄待机灵剑，持续10秒／冷却15秒");
			Check(!Swords.IsBusy && !Swords.IsCharging && Swords.CooldownRemaining > 14f, "背景召唤不占动作、不蓄力，立即开始冷却");
			Check(!Swords.TryPress() && Swords.SwordCount == 3, "持续中拒绝重复召唤，不叠组");
			await Shot("j42_swords_hover");
			var target = Target(new Vector3(70, 5, 70));
			void PlaceOnOuter(AttackInstance _) { target.GlobalPosition = Swords.Swords[2].GlobalPosition; GD.Print($"[SpiritSwords] 外圈靶={target.GlobalPosition} 玩家={_player.GlobalPosition}"); }
			Melee.StrikeStarted += PlaceOnOuter;
			await Attack(); Melee.StrikeStarted -= PlaceOnOuter;
			Check(Swords.FollowCount == 1 && Swords.Following, "第一段真实ATK启动一次协同");
			var snapshot = Swords.LastSwordAttack!;
			Check(snapshot.Ability == "skl_b1_w1_2" && snapshot.ComboStep == 0 && snapshot.ComboStepCount == 5
				&& Mathf.IsEqualApprox(snapshot.BaseDamage, Melee.GetNode<AttackHitbox>("Attack").CurrentAttack!.BaseDamage * .3f)
				&& snapshot.CritChance == 1f && snapshot.CritMultiplier == 3f, "三剑继承当段30%伤害和出手暴击，攻击力不重复相乘");
			Vector3 start = Swords.Swords[0].GlobalPosition;
			await Shot("j42_swords_attack");
			Check(await Until(() => _hits.Any(h => h.Ability == Swords.AbilityId)), "真实剑体命中外圈物理靶");
			Check(!_hits.Any(h => h.Ability == Melee.AbilityId), "外圈靶超出手中剑命中范围，确由灵剑扩大攻击距离");
			Check(_hits.Count(h => h.Ability == Swords.AbilityId) <= 3, "每柄每次挥击对同一目标至多一次");
			Check(await Until(() => !Swords.Following), "原挥击结束进入缓慢归位");
			Check(!Swords.SwordsReady && Swords.Swords[0].GlobalPosition.DistanceTo(start) > .01f, "灵剑真实移动而非仅扩大攻击盒，归位途中未就绪");
			int follow = Swords.FollowCount; await Attack();
			Check(Swords.FollowCount == follow, "连续第二段发生在归位期，不重复协同");
			_hits.Clear(); target.GlobalPosition = Swords.Swords[0].GlobalPosition;
			await Wait(.15f); Check(!_hits.Any(h => h.Ability == Swords.AbilityId), "归位过程关闭伤害");
			target.QueueFree();
			Check(await Until(() => Swords.SwordsReady), "三剑完全归位后恢复就绪");
			await Until(() => Melee.IsReady); await Attack();
			Check(Swords.FollowCount == follow + 1, "归位后下一次攻击再次协同");
			await Equip(); await Summon();
			int beforeFollow = Swords.FollowCount;
			Press("weapon_skill_1", true); Press("skill_1", true); await Wait(.03f); Press("weapon_skill_1", false); Press("skill_1", false); await Wait(.45f);
			Check(Swords.FollowCount == beforeFollow, "近战前摇被闪打断，没有ATK就不协同");
			await Equip(); await Summon();
			Melee.Series = (AbilitySeries)Melee.Series!.Duplicate(); Melee.Series.ComboNextEvent = "MissingSwordATK";
			Press("weapon_skill_1", true); await Wait(.03f); Press("weapon_skill_1", false); await Wait(3f);
			Check(Swords.FollowCount == 0 && !Melee.IsBusy, "缺失真实ATK只取消原攻击，不计时补发灵剑");
			// 分别选择五段真实动画，验证外圈方向和手中武器保持一致。
			for (int i = 0; i < 5; i++)
			{
				await Equip(); Melee.ConfigureSingleStep(i, 1f, 1f); await Summon(); await Attack();
				var weapon = _player.GetNode<ActRoguelike.Visual.CharacterDisplay>("Appearance").EquippedWeapon!;
				Check(Swords.LastSwordAttack?.ComboStep == i && Swords.Swords[0].GlobalBasis.Y.Dot(weapon.GlobalBasis.Orthonormalized().Y) > .99f,
					$"第{i + 1}段动画外圈灵剑跟随握持剑的空间姿态");
				Vector3 point = Swords.Swords[0].GlobalPosition;
				await Wait(.05f);
				Check(Swords.Swords[0].GlobalPosition.DistanceTo(point) > .01f, $"第{i + 1}段挥击实际形成运动路径");
			}
			await Equip(); await Summon();
			var victim = Target(new Vector3(60, 2, 60), 1f); float remaining = Swords.RemainingSeconds; Kill(victim);
			Check(victim.IsDead && Swords.RemainingSeconds == remaining && Swords.ExtensionCount == 0, "没有灵能汲取时本人击杀不续时"); victim.QueueFree();
			Check(await Until(() => !Swords.Summoned, 11f) && Swords.SwordCount == 0 && Swords.CooldownRemaining > 3f,
				"10秒实际到期清除全部剑体，冷却仍剩约5秒");
			Check(await Until(() => Swords.IsReady, 6f), "召唤起算15秒后技能重新可用");
			Grant();
			victim = Target(new Vector3(60, 2, 60), 1f); Kill(victim); victim.QueueFree();
			Check(!Swords.Summoned && Swords.RemainingSeconds == 0f, "效果到期后击杀不复活灵剑、不储存续时");
			await Summon(); remaining = Swords.RemainingSeconds;
			victim = Target(new Vector3(60, 2, 60), 1f); Kill(victim); victim.QueueFree();
			Check(Mathf.IsEqualApprox(Swords.RemainingSeconds, remaining + 2f) && Swords.ExtensionCount == 1, "本人真实击杀立即精确增加2秒");
			var other = Target(new Vector3(65, 2, 65), faction: Faction.Player);
			victim = Target(new Vector3(60, 2, 60), 1f); remaining = Swords.RemainingSeconds; Kill(victim, other); victim.QueueFree();
			Check(Swords.RemainingSeconds == remaining && Swords.ExtensionCount == 1, "其他单位击杀不冒领续时"); other.QueueFree();
			victim = Target(new Vector3(60, 2, 60), 1f); victim.ApplyDirectDamage(100000f); victim.QueueFree();
			Check(Swords.ExtensionCount == 1, "无归属的环境死亡不冒领续时");
			for (int i = 0; i < 4; i++) { victim = Target(new Vector3(60, 2, 60), 1f); Kill(victim); victim.QueueFree(); }
			Check(Swords.ExtensionCount == 5, "连续敌对击杀逐次增加持续时间");
			await Until(() => Swords.CooldownRemaining <= 0f, 16f);
			Check(Swords.Summoned && !Swords.TryPress() && Swords.SwordCount == 3, "续时超过15秒，冷却已好仍不能叠出第二组");
			SpiritSwordsAbility old = Swords;
			await Equip(WeaponPresetKind.Scythe);
			Check(old.SwordCount == 0 && !old.Summoned && old.KillExtensionSeconds == 0f, "换武器清剑体、状态和灵能汲取参数");
			await Attack(); Check(!old.Summoned && old.SwordCount == 0, "换装后旧订阅不跟随镰刀出手");
			await Equip(); await Summon();
			_player.ForceKill();
			await Wait(.05f); Check(Swords.SwordCount == 0 && !Swords.Summoned, "玩家死亡清理剑体与事件订阅");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1" }) Press(action, false);
		GD.Print($"[SpiritSwords] === 三剑协同验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}
