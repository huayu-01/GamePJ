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

/// <summary>三剑真实命中、有序高速突刺、目标／墙体边界、归位门控和击杀续时。</summary>
public partial class SpiritSwordsProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private SpiritSwordsAbility Swords => (SpiritSwordsAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
	private MeleeComboAbility Melee => (MeleeComboAbility)_player.AbilitySlots.Resolve("weapon_skill_1")!;
	private readonly List<AttackInstance> _hits = new();
	private readonly List<ulong> _launchFrames = new();
	private int _observedLaunchCount;
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_player) || _player.AbilitySlots.Resolve("weapon_skill_2") is not SpiritSwordsAbility swords) return;
		if (swords.LaunchCount < _observedLaunchCount) { _launchFrames.Clear(); _observedLaunchCount = 0; }
		// 在物理帧中观察实际出剑计数，截图写盘不能延后或合并出手节奏的取证。
		while (_observedLaunchCount < swords.LaunchCount) { _launchFrames.Add(Engine.GetPhysicsFrames()); _observedLaunchCount++; }
	}
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
		_camera.GlobalPosition = new Vector3(25, 3.8f, 25); _camera.LookAt(new Vector3(20, 1.8f, 19)); _camera.ResetPhysicsInterpolation();
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
		target.ForceKill(owner ?? _player);
	}
	private Vector3 NearPosition() => _player.GlobalPosition - _player.GlobalBasis.Z * 1.6f + Vector3.Up * .9f;
	private void HitForFilter(Unit target, bool dot = false, bool secondary = false, Unit? owner = null, string ability = "probe_hit")
	{
		var attack = new AttackInstance { Attacker = owner ?? _player, Ability = ability, BaseDamage = 1f, IsDamageOverTime = dot, IsSecondary = secondary };
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
		Transform3D before = _camera.GlobalTransform;
		if (name.Contains("hover")) await Wait(.07f);
		if (name.Contains("thrust"))
		{
			_camera.GlobalPosition = _player.GlobalPosition + _player.GlobalBasis.X * 5.5f + Vector3.Up * 3.2f;
			_camera.LookAt(_player.GlobalPosition - _player.GlobalBasis.Z * .9f + Vector3.Up * 1.8f);
			_camera.ResetPhysicsInterpolation();
		}
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"渲染截图{name}");
		_camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
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
			var blade = (CylinderMesh)Swords.Swords[0].GetNode<MeshInstance3D>("Blade").Mesh!;
			Check(blade.Height / (blade.BottomRadius * 2) > 12f, "剑身长宽比超过12，接近针形但仍有明确剑体宽度");
			await Shot("j43_swords_hover");
			await Attack(); await Wait(.4f);
			Check(Swords.VolleyCount == 0 && Swords.LaunchCount == 0, "挥空只有真实ATK但没有命中，三剑保持待机");
			Check(Swords.Swords.All(s => s.CurrentAttack == null), "待机剑没有开启伤害");
			await Until(() => Melee.IsReady);
			var target = Target(NearPosition());
			_hits.Clear(); await Attack();
			Check(await Until(() => Swords.VolleyCount == 1), "真实近战命中锁定目标并开始一轮追击");
			Check(Swords.LockedTarget == target && Swords.LaunchCount == 0, "命中后先短促蓄势，尚未同时把三剑弹出去");
			AttackInstance source = Melee.GetNode<AttackHitbox>("Attack").CurrentAttack!;
			_player.Stats.SetBase("DamageMultiplier", 4f); _player.Stats.SetBase("CritChanceAdd", -1f);
			for (int i = 0; i < 3; i++)
			{
				Check(await Until(() => _launchFrames.Count >= i + 1), $"第{i + 1}柄灵剑真实出手");
				if (i == 0)
				{
					await Until(() => Swords.ImpactCount >= 1);
					Check((Engine.GetPhysicsFrames() - _launchFrames[0]) / (float)Engine.PhysicsTicksPerSecond < .15f, "出手后近距离命中少于0.15秒，直刺不缓慢滑行");
					await Shot("j43_swords_thrust");
				}
			}
			Check(Swords.LaunchOrder.SequenceEqual(new[] { 1, 0, 2 }), "固定中→左→右顺序，独立出手");
			float firstGap = (_launchFrames[1] - _launchFrames[0]) / (float)Engine.PhysicsTicksPerSecond;
			float secondGap = (_launchFrames[2] - _launchFrames[1]) / (float)Engine.PhysicsTicksPerSecond;
			Check(firstGap >= .07f && secondGap >= .07f, $"出手有明确节奏间隔：{firstGap * 1000:0.0}／{secondGap * 1000:0.0}毫秒");
			Check(await Until(() => Swords.ImpactCount == 3), "三柄瘦长剑体逐个真实命中锁定敌人");
			var swordHits = _hits.Where(h => h.Ability == Swords.AbilityId).ToArray();
			Check(swordHits.Length == 3, "每柄剑每轮对同一目标恰好一次伤害，无停留重复命中");
			Check(swordHits.All(h => Mathf.IsEqualApprox(h.BaseDamage, source.BaseDamage * .3f) && h.CritChance == 1f && h.CritMultiplier == 3f),
				"三连刺保留触发命中时的伤害／暴击快照，不重读后来修改的属性");
			Check(swordHits.All(h => h.HasTag("SpiritSword") && h.HasTag("Projectile")) && swordHits[^1].FeedbackWeight > swordHits[0].FeedbackWeight
				&& swordHits[0].FeedbackWeight > 1f, "脱手刺击使用独立攻击身份，第三击反馈更重");
			Check(Swords.VolleyCount == 1, "灵剑自身命中不会递归触发下一轮灵剑");
			int volleys = Swords.VolleyCount; await Attack();
			Check(Swords.VolleyCount == volleys && !Swords.SwordsReady, "连续命中发生在飞行／归位期时不抢占或排队补发");
			int count = _hits.Count(h => h.Ability == Swords.AbilityId);
			await Wait(.15f);
			Check(_hits.Count(h => h.Ability == Swords.AbilityId) == count, "短促贯穿和缓慢归位阶段没有额外伤害");
			_player.GlobalPosition += Vector3.Right * 1.5f; _camera.GlobalPosition += Vector3.Right * 1.5f;
			Check(await Until(() => Swords.SwordsReady), "三柄剑全部归位后恢复就绪");
			Check(Mathf.Abs(Swords.Swords[1].GlobalPosition.Y - _player.GlobalPosition.Y - 3.13f) < .05f, "玩家移动后灵剑归到新的头顶位置");
			target.GlobalPosition = NearPosition(); await Until(() => Melee.IsReady); await Attack();
			Check(await Until(() => Swords.VolleyCount == 2), "完全归位后再次命中可以启动第二轮追击");
			target.QueueFree();
			await Equip(); await Summon();
			var dying = Target(new Vector3(60, 2, 60), 1f);
			var survivor = Target(_player.GlobalPosition - _player.GlobalBasis.Z * 6f + Vector3.Up * .9f);
			await Attack();
			AttackInstance sameSwing = Melee.GetNode<AttackHitbox>("Attack").CurrentAttack!;
			dying.ReceiveHit(sameSwing.RollHit(dying, Vector3.Forward, dying.GlobalPosition));
			Check(dying.IsDead && Swords.VolleyCount == 0 && Swords.SwordsReady, "致死命中不提前锁尸体，不浪费追击和归位周期");
			survivor.ReceiveHit(sameSwing.RollHit(survivor, Vector3.Forward, survivor.GlobalPosition));
			Check(Swords.VolleyCount == 1 && Swords.LockedTarget == survivor, "同次攻击先杀弱怪后命中活靶，三剑锁定存活目标");
			Check(await Until(() => Swords.ImpactCount == 3), "存活的第二目标仍收到真实三连刺");
			dying.QueueFree(); survivor.QueueFree();
			await Equip(); await Summon();
			target = Target(NearPosition()); await Attack(); await Until(() => Swords.VolleyCount == 1);
			Melee.InterruptByControl();
			target.GlobalPosition += new Vector3(1.5f, 0, -1.5f);
			var bystander = Target(Swords.Swords[1].GlobalPosition.Lerp(target.GlobalPosition, .5f));
			Check(await Until(() => Swords.ImpactCount == 3), "原攻击结束后，已出手灵剑仍能追中移动目标");
			Check(bystander.Health == bystander.MaxHealth, "追击只伤锁定敌人，不误伤路径旁观单位");
			target.QueueFree(); bystander.QueueFree();
			await Equip(); await Summon();
			target = Target(NearPosition()); await Attack(); await Until(() => Swords.VolleyCount == 1); Melee.InterruptByControl();
			var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(8, 6, .25f) } }); AddChild(wall);
			wall.GlobalTransform = new Transform3D(_player.GlobalBasis, _player.GlobalPosition - _player.GlobalBasis.Z * .8f + Vector3.Up * 1.5f);
			await Wait(.7f);
			Check(Swords.LaunchCount == 3 && Swords.ImpactCount == 0, "有World墙体时三剑被真实形状扫掠阻挡，不穿墙命中");
			Check(await Until(() => Swords.SwordsReady), "撞墙后收剑，无追踪卡死");
			wall.QueueFree(); target.QueueFree();
			await Equip(); await Summon();
			target = Target(NearPosition()); await Attack(); await Until(() => Swords.ImpactCount == 1); target.ForceKill();
			Check(await Until(() => Swords.SwordsReady) && Swords.LaunchCount == 1 && Swords.SwordCount == 3, "第一剑后目标死亡，未出手两剑取消并全部归位");
			target.QueueFree();
			await Equip(); await Summon();
			target = Target(NearPosition()); await Attack(); await Until(() => Swords.VolleyCount == 1); target.QueueFree();
			Check(await Until(() => Swords.SwordsReady) && Swords.LaunchCount == 0, "目标在蓄势期释放，安全取消追击");
			await Equip(); await Summon();
			target = Target(NearPosition()); await Attack(); await Until(() => Swords.LaunchCount > 0);
			var disabled = Swords; disabled.Enabled = false; await Wait(.06f);
			Check(disabled.SwordCount == 0 && !disabled.BarrageActive, "禁用期间清理飞行、待发和伤害");
			target.QueueFree();
			await Equip(); await Summon();
			Press("weapon_skill_1", true); Press("skill_1", true); await Wait(.03f); Press("weapon_skill_1", false); Press("skill_1", false); await Wait(.45f);
			Check(Swords.VolleyCount == 0, "近战前摇被闪打断，没有命中就不追击");
			await Equip(); await Summon();
			Melee.Series = (AbilitySeries)Melee.Series!.Duplicate(); Melee.Series.ComboNextEvent = "MissingSwordATK";
			target = Target(NearPosition());
			Press("weapon_skill_1", true); await Wait(.03f); Press("weapon_skill_1", false); await Wait(3f);
			Check(Swords.VolleyCount == 0 && !Melee.IsBusy, "缺失真实ATK只取消原攻击，不补发灵剑"); target.QueueFree();
			for (int i = 0; i < 5; i++)
			{
				await Equip(); Melee.ConfigureSingleStep(i, 1f, 1f); await Summon(); target = Target(NearPosition()); await Attack();
				Check(await Until(() => Swords.ImpactCount == 3) && Swords.LastSwordAttack?.ComboStep == i,
					$"第{i + 1}段真实命中都可触发三连刺，保留对应段快照");
				Check(Swords.VolleyCount == 1 && Swords.LaunchOrder.SequenceEqual(new[] { 1, 0, 2 }), $"第{i + 1}段每次命中只启动一轮有序追击");
				target.QueueFree();
			}
			await Equip(); await Summon(); target = Target(NearPosition());
			HitForFilter(target, dot: true); HitForFilter(target, secondary: true); HitForFilter(target, ability: Swords.AbilityId);
			await Wait(.3f);
			Check(Swords.VolleyCount == 0, "持续伤害、派生伤害和灵剑自身均不触发递归追击");
			var helper = Target(new Vector3(65, 2, 65), faction: Faction.Player);
			HitForFilter(target, owner: helper); await Wait(.1f);
			Check(Swords.VolleyCount == 0, "其他单位命中不冒领为玩家追击");
			helper.QueueFree(); target.QueueFree();
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
