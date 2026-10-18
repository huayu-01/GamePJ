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
using Godot;

namespace ActRoguelike.Testing;

/// <summary>状态自然扣血产生真实死亡和Kill事实，验证来源与逐杀收益。</summary>
public partial class DotAttributionProbe : Node3D
{
	private int _pass, _fail;
	private Unit _owner = null!;
	private readonly List<GameplayEvent> _kills = new();
	private readonly List<HitInstance> _dotHits = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[DotSource] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds = 0.05) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private void Fact(GameplayEvent fact) { if (fact.Kind == GameplayEventKind.Kill) _kills.Add(fact); }
	private Unit Target(float health = 5f)
	{
		var unit = new Unit { Faction = Faction.Enemy, MaxHealth = health }; AddChild(unit);
		unit.GlobalPosition = new Vector3(30f, 5f, 30f); unit.SetPhysicsProcess(false);
		unit.Damaged += (_, hit) => { if (hit.Damage > 0f) _dotHits.Add(hit); };
		return unit;
	}
	private void Apply(Unit target, Unit? owner, float duration = 3f)
	{
		var attack = new AttackInstance { Attacker = owner, Ability = "SourceProbe", AbilityKind = "source_probe", BaseDamage = 0f,
			Status = new StatusEffect { Id = "ProbeBurn", Duration = duration, DamagePerSecond = 6f } };
		target.ReceiveHit(attack.RollHit(target, Vector3.Zero, target.GlobalPosition));
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			_owner = new Unit { Faction = Faction.Player }; AddChild(_owner);
			var soul = new SoulHarvestRule { RewardId = "dot_soul_probe", Chance = 1f, PickupDelaySeconds = 10f }; _owner.AddChild(soul);
			var armor = new RuneArmorOnKillRule { RewardId = "dot_armor_probe", KillsPerTrigger = 1 }; _owner.AddChild(armor);
			GameplayEventBus.Raised += Fact;
			var target = Target(); Apply(target, _owner); target._PhysicsProcess(1d);
			Check(target.IsDead, "状态每秒6伤害真实击杀5血目标");
			Check(_dotHits.Count == 1 && _dotHits[0].Attacker == _owner, "DoT命中保存原施加者");
			Check(_kills.Count == 1 && _kills[0].Source == _owner && _kills[0].Context?.OriginalSource == _owner, "DoT死亡产生本人Kill与正确上下文");
			Check(soul.RollCount == 1 && soul.DropCount == 1, "燃烧击杀触发一次真实灵魂收割");
			Check(armor.KillTally == 1 && armor.ArmorGrantTally == 1, "燃烧击杀计入一次死铠加甲");
			Check(_dotHits[0].Attack?.Ability == "Status:ProbeBurn" && _dotHits[0].Attack?.AbilityKind == "source_probe"
				&& _dotHits[0].Attack?.IsDamageOverTime == true && !_dotHits[0].Critical, "保留独立状态身份／来源技能身份，DoT不伪装成一次主攻击或暴击");
			Check(_kills.Count == 1 && _kills[0].IsDamageOverTime && _kills[0].ComboStepCount == 0, "DoT击杀事实带类型标记，不继承末刀段号");
			target.QueueFree(); await Wait();
			var burn = new BurnOnHitRule { Duration = 1.1f }; _owner.AddChild(burn);
			target = Target();
			var hit = new AttackInstance { Attacker = _owner, BaseDamage = 1f, Ability = "BurnRuleProbe" }.RollHit(target, Vector3.Zero, target.GlobalPosition);
			target.ReceiveHit(hit); target._PhysicsProcess(1d);
			Check(_kills.Count == 2 && _kills[1].Source == _owner, "生产BurnOnHitRule挂燃烧后保持施加者归属");
			target.QueueFree(); burn.QueueFree(); await Wait();
			await Sources(soul, armor);
			await NoRefreshLoop();
			await EngineTick();
			await GuardKeepsOldDotBehavior();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GameplayEventBus.Raised -= Fact;
		GD.Print($"[DotSource] === 持续伤害归属验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Sources(SoulHarvestRule soul, RuneArmorOnKillRule armor)
	{
		int kills = _kills.Count, drops = soul.DropCount, tally = armor.KillTally;
		var other = new Unit { Faction = Faction.Player }; AddChild(other);
		var target = Target(); Apply(target, _owner); Apply(target, other); target._PhysicsProcess(1d);
		Check(_kills.Count == kills + 1 && _kills[kills].Source == other, "同名状态由最近一次施加者获得后续击杀");
		Check(soul.DropCount == drops && armor.KillTally == tally, "其他来源DoT击杀不会奖励给本人");
		target.QueueFree(); await Wait();
		kills = _kills.Count;
		target = Target(); Apply(target, _owner);
		target.ApplyStatus(new StatusEffect { Id = "ProbeBurn", Duration = 3f, DamagePerSecond = 6f });
		target._PhysicsProcess(1d);
		Check(target.IsDead && _kills.Count == kills && _dotHits[^1].Attacker == null, "未知来源刷新清除旧归属，不猜测玩家");
		Check(soul.DropCount == drops && armor.KillTally == tally, "环境状态击杀不冒领灵魂／死铠收益");
		target.QueueFree(); await Wait();
		var transient = new Unit { Faction = Faction.Player }; AddChild(transient);
		target = Target(); Apply(target, transient); transient.Free(); target._PhysicsProcess(1d);
		Check(target.IsDead && _kills.Count == kills && _dotHits[^1].Attacker == null, "施加者已释放仍安全扣血，未知归属不抛异常或错认他人");
		target.QueueFree(); other.QueueFree(); await Wait();
		target = Target(100f); Apply(target, _owner);
		_owner.Stats.SetBase("DamageMultiplier", 4f); _owner.Stats.SetBase("CritChanceAdd", 1f);
		target._PhysicsProcess(1d);
		Check(Mathf.IsEqualApprox(target.Health, 94f) && _dotHits[^1].Damage == 6f && !_dotHits[^1].Critical,
			"归属修复保留表内固定DPS，不重复乘攻击力／暴击或巨人额外伤害");
		target.IsInvulnerable = true; target._PhysicsProcess(1d);
		Check(Mathf.IsEqualApprox(target.Health, 94f), "DoT继续经过原有无敌与受伤入口");
		target.IsInvulnerable = false; target._PhysicsProcess(1d);
		Check(Mathf.IsEqualApprox(target.Health, 88f) && !target.HasStatus("ProbeBurn"), "到期最后一次Tick后撤销状态，不延长原持续时间");
		target.QueueFree(); await Wait();
	}
	private async Task NoRefreshLoop()
	{
		var burn = new BurnOnHitRule { Duration = 1.1f }; _owner.AddChild(burn);
		var counter = new DotHitCounterRule(); _owner.AddChild(counter);
		var target = Target(100f);
		target.ReceiveHit(new AttackInstance { Attacker = _owner, BaseDamage = 1f }.RollHit(target, Vector3.Zero, target.GlobalPosition));
		Check(counter.Count == 1 && target.HasStatus("Burn"), "正常主攻击仍触发一次命中规则并挂燃烧");
		target._PhysicsProcess(1d);
		Check(counter.Count == 1 && target.Statuses[0].Remaining < 0.11f, "DoT有来源后不再次触发主攻击命中规则，不刷新自身燃烧");
		target._PhysicsProcess(0.2d);
		Check(!target.HasStatus("Burn") && Mathf.IsEqualApprox(target.Health, 93f), "生产燃烧仍按1.1秒到期，不能自循环永久续期");
		target.QueueFree(); burn.QueueFree(); counter.QueueFree(); await Wait();
		counter = new DotHitCounterRule(); _owner.AddChild(counter);
		target = Target(100f); Apply(target, _owner); target._PhysicsProcess(1d);
		Check(counter.Count == 1, "缺省命中规则仍只接主攻击一次");
		counter.IncludeDamageOverTimeHits = true; target._PhysicsProcess(1d);
		Check(counter.Count == 2, "显式允许的专用规则可以订阅DoT命中事实");
		target.QueueFree(); counter.QueueFree(); await Wait();
	}
	private async Task EngineTick()
	{
		int kills = _kills.Count;
		var target = Target(); Apply(target, _owner); target.SetPhysicsProcess(true);
		await Wait(1.15);
		Check(target.IsDead && _kills.Count == kills + 1 && _kills[kills].Source == _owner,
			"引擎自然物理帧累计一秒后真实DoT击杀归属本人");
		target.QueueFree(); await Wait();
	}
	private async Task GuardKeepsOldDotBehavior()
	{
		RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
		var player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(player);
		player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
		new PlayerLoadout().Apply(player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		player.SetPhysicsProcess(false);
		var enemy = new Unit { Faction = Faction.Enemy }; AddChild(enemy);
		Apply(player, enemy);
		var guard = player.GetNode<GuardStanceAbility>("AbilityBSkill2");
		Input.ParseInputEvent(new InputEventAction { Action = "skill_2", Pressed = true });
		await Wait(0.03);
		Input.ParseInputEvent(new InputEventAction { Action = "skill_2", Pressed = false });
		for (int i = 0; i < 120 && !guard.StanceActive; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(guard.StanceActive, "真实GuardOn动画开启架势，随后测试已有燃烧");
		float armor = player.Armor, health = player.Health;
		float expected = 6f * (1f - player.CombinedDamageReduction());
		player._PhysicsProcess(1d);
		Check(guard.StanceActive && guard.ParryCount == 0 && Mathf.Abs(armor + health - player.Armor - player.Health - expected) < 0.001f,
			"恢复敌方DoT来源不会把原本不可架住的持续伤害变成一次新招架");
		var jumps = new KillAirJumpRule { ExtraAirJumps = 1, JumpCap = 3 }; player.AddChild(jumps);
		var victim = Target(); Apply(victim, player); victim._PhysicsProcess(1d);
		Check(jumps.TriggerTally == 1 && player.Movement!.ExtraAirJumps >= 1, "DoT击杀同样计入践踏死亡的逐杀事实");
		Check(player.Movement!.AirJumpsLeft >= 1, "DoT击杀获得的空中跳跃当场可用");
		victim.QueueFree();
		player.QueueFree(); enemy.QueueFree(); await Wait();
	}
}

public partial class DotHitCounterRule : TriggerRule
{
	public int Count { get; private set; }
	public override bool Matches(GameplayEvent fact) => fact.Kind == GameplayEventKind.Hit && fact.Source == GetParent<Unit>();
	public override void Execute(GameplayEvent fact) => Count++;
}
