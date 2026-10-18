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

/// <summary>真实伤害产生的逐杀掉落和加甲，不伪造Kill事件；覆盖攻击连锁边界与卸除。</summary>
public partial class KillFactProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private RunBuild _build = new();
	private readonly PlayerLoadout _loadout = new();
	private readonly List<Unit> _targets = new();
	private readonly List<GameplayEvent> _kills = new();
	private readonly List<ulong> _killFrames = new();
	private readonly HashSet<AttackInstance> _attacks = new();
	private int OwnKills => _kills.Count(kill => kill.Source == _player);
	private SoulPickup[] Souls => GetChildren().OfType<SoulPickup>().Where(soul => !soul.IsQueuedForDeletion()).ToArray();
	private SoulHarvestRule SoulRule => _player.GetChildren().OfType<SoulHarvestRule>().Single();
	private RuneArmorOnKillRule ArmorRule => _player.GetChildren().OfType<RuneArmorOnKillRule>().Single();
	public override void _Process(double delta) { _elapsed += delta; _build.TickTriggers((float)delta); }
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[KillFact] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void OnFact(GameplayEvent fact)
	{
		if (fact.Kind != GameplayEventKind.Kill) return;
		_kills.Add(fact);
		if (fact.Source == _player) _killFrames.Add(Engine.GetPhysicsFrames());
	}
	private async Task Reset()
	{
		foreach (var rule in _player.GetChildren().OfType<TriggerRule>().ToArray()) { _player.RemoveChild(rule); rule.QueueFree(); }
		_player.Stats.RemoveBySource(_build); _build = new RunBuild();
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		foreach (var soul in Souls) soul.QueueFree();
		await Wait(0.05); _targets.Clear(); _kills.Clear(); _killFrames.Clear(); _attacks.Clear();
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);
		_player.FillVitals();
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 1, 12)); _camera.ResetPhysicsInterpolation();
		await Wait(0.2);
	}
	private void Grant(RewardConfig row)
	{
		var definition = RewardEffectRegistry.Compile(row)!;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = _build });
		Check(_build.Has(row.Id), $"真实表编译并加入构筑：{row.Id}");
	}
	private static RewardConfig Copy(string id, params (string Key, float Value)[] values)
	{
		RewardConfig row = ConfigRows.ById<RewardConfig>(id) ?? (RewardConfig?)ConfigRows.ById<RuneConfig>(id) ?? throw new InvalidOperationException(id);
		var copy = (RewardConfig)row.Duplicate(true);
		foreach (var (key, value) in values) foreach (var parameter in copy.HandlerParams) if (parameter.Key == key) parameter.Value = value;
		return copy;
	}
	private Unit Target(float x = 30, float health = 1, float z = 30)
	{
		var target = new Unit { Faction = Faction.Enemy, MaxHealth = health, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.16f } });
		AddChild(target); target.GlobalPosition = new Vector3(x, 0.9f, z); _targets.Add(target);
		target.HitReceived += hit => { if (hit.Attack != null) _attacks.Add(hit.Attack); };
		return target;
	}
	private void Hit(Unit target, Unit? source = null, EventContext? context = null, float damage = 100)
	{
		source ??= _player;
		var attack = new AttackInstance { Attacker = source, BaseDamage = damage, CanCrit = false };
		var hit = context == null ? attack.RollHit(target, Vector3.Forward, target.GlobalPosition)
			: new HitInstance { Target = target, Attacker = source, Attack = attack, Context = context,
				Damage = damage, Direction = Vector3.Forward, Position = target.GlobalPosition };
		target.ReceiveHit(hit);
	}
	private void ChainKills(int count, EventContext root)
	{
		for (int i = 0; i < count; i++) { var target = Target(30 + i * 0.3f); Hit(target, context: root.Child(_player, target)!); }
	}
	private async Task MeleeFive()
	{
		for (int i = 0; i < 5; i++) Target(20 + (i - 2) * 0.25f, z: 18.8f);
		await Wait(0.05);
		var ability = PlayerAbilityInput.FindByInputAction(_player, "weapon_skill_1")!;
		Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_1", Pressed = true });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_1", Pressed = false });
		Check(await Until(() => OwnKills == 5), $"真实按键→动画ATK→物理靶五杀，实际{OwnKills}");
		await Until(() => !ability.IsActive);
		Check(_attacks.Count == 1 && _killFrames.Distinct().Count() == 1, "五次真实死亡同帧共用一个AttackInstance，不把自然多目标误称同链");
	}
	public override async void _Ready()
	{
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.SpiritSword;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera); GameplayEventBus.Raised += OnFact;
			await Wait(0.2);
			await ActualMelee(); await SharedChainAndDepth(); await SourcesAndRemoval(); await ProbabilityZero(); await TableAndPickup();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { GameplayEventBus.Raised -= OnFact; }
		GD.Print($"[KillFact] === 逐杀事实验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task ActualMelee()
	{
		await Reset(); await MeleeFive();
		Check(Souls.Length == 0 && !_player.GetChildren().OfType<RuneArmorOnKillRule>().Any(), "无增幅／符文时真实五杀无灵魂、无加甲规则");
		await Reset();
		using var soul = Copy("mut_soul_harvest", ("Chance", 1)); Grant(soul);
		Grant(ConfigRows.ById<RuneConfig>("rune_death_armor")!);
		float armor = _player.Armor; await MeleeFive();
		Check(SoulRule.RollCount == 5 && SoulRule.DropCount == 5, $"真实一刀五杀每杀掷点并必掉，掷{SoulRule.RollCount}／掉{SoulRule.DropCount}");
		Check(ArmorRule.KillTally == 5 && Mathf.IsEqualApprox(_player.Armor, armor), "死铠真实五杀全部记入，未满十杀不加甲");
		Check(Mathf.IsZeroApprox(_player.Stats.Get("HealMultiplier")), "死铠原有禁疗仍生效");
	}
	private async Task SharedChainAndDepth()
	{
		await Reset(); using var soul = Copy("mut_soul_harvest", ("Chance", 1)); Grant(soul); Grant(ConfigRows.ById<RuneConfig>("rune_death_armor")!);
		float armor = _player.Armor;
		var root = EventContext.Create(_player, null); ChainKills(5, root);
		Check(OwnKills == 5 && _kills.Select(kill => kill.Context?.ChainId).Distinct().Count() == 1, "共享根上下文五次真实致死命中，产生同链五个Kill事实");
		Check(SoulRule.RollCount == 5 && SoulRule.DropCount == 5, $"同链第五杀仍独立掷点掉灵魂，实读{SoulRule.RollCount}／{SoulRule.DropCount}");
		Check(ArmorRule.KillTally == 5, $"死铠同链第五杀计入，实读{ArmorRule.KillTally}");
		ChainKills(5, root);
		Check(OwnKills == 10 && ArmorRule.KillTally == 10 && ArmorRule.ArmorGrantTally == 1 && Mathf.IsEqualApprox(_player.Armor, armor + 1), $"同链十杀按真表加一次甲：计{ArmorRule.KillTally}／加{ArmorRule.ArmorGrantTally}，甲{armor}→{_player.Armor}");
		Check(SoulRule.RollCount == 10 && SoulRule.DropCount == 10 && Souls.Length == 10, "十个真实击杀各掉一个独立灵魂，不受每链四次门截断");
		await Reset(); Grant(soul); Grant(ConfigRows.ById<RuneConfig>("rune_death_armor")!);
		var target = Target(); var edge = EventContext.Create(_player, target);
		for (int i = 0; i < EventContext.MaxDepth; i++) edge = edge.Child(_player, target)!;
		Hit(target, context: edge);
		Check(OwnKills == 1 && _kills.Single().Context?.Depth == EventContext.MaxDepth, "合法链深度末层造成真实死亡与Kill");
		Check(SoulRule.RollCount == 1 && SoulRule.DropCount == 1 && ArmorRule.KillTally == 1, "合法末层击杀也计入掉落和死铠，不生成新攻击");
	}
	private async Task SourcesAndRemoval()
	{
		await Reset(); using var soul = Copy("mut_soul_harvest", ("Chance", 1)); Grant(soul); Grant(ConfigRows.ById<RuneConfig>("rune_death_armor")!);
		var other = new Unit { Faction = Faction.Player, MaxHealth = 100 }; AddChild(other);
		Hit(Target(), source: other); Hit(Target(31, 10000), damage: 1);
		Check(_kills.Count == 1 && OwnKills == 0 && SoulRule.RollCount == 0 && ArmorRule.KillTally == 0, "他人真实击杀和本人非致死命中都不计入"); other.QueueFree();
		var oldSoul = SoulRule; var oldArmor = ArmorRule;
		_player.RemoveChild(oldSoul); _player.RemoveChild(oldArmor); Hit(Target());
		Check(OwnKills == 1 && oldSoul.RollCount == 0 && oldArmor.KillTally == 0 && Souls.Length == 0, "摘除规则即退订，未释放的旧节点也不接之后的真实击杀");
		oldSoul.QueueFree(); oldArmor.QueueFree();
	}
	private async Task ProbabilityZero()
	{
		await Reset(); using var soul = Copy("mut_soul_harvest", ("Chance", 0)); Grant(soul); ChainKills(6, EventContext.Create(_player, null));
		Check(OwnKills == 6 && SoulRule.RollCount == 6, $"概率0仍逐杀计入六次掷点，实读{SoulRule.RollCount}");
		Check(SoulRule.DropCount == 0 && Souls.Length == 0 && !SoulRule.LastRollHit, "概率0不掉落，未把必掉夹具写进生产概率");
	}
	private async Task TableAndPickup()
	{
		await Reset();
		using var armor = Copy("rune_death_armor", ("Armor", 7), ("ArmorPerKills", 3), ("ArmorPerTrigger", 2.5f)); Grant(armor);
		float start = _player.Armor; ChainKills(7, EventContext.Create(_player, null));
		Check(ArmorRule.KillsPerTrigger == 3 && Mathf.IsEqualApprox(ArmorRule.ArmorPerTrigger, 2.5f) && ArmorRule.KillTally == 7 && ArmorRule.ArmorGrantTally == 2 && Mathf.IsEqualApprox(_player.Armor, start + 5), "只改表每三杀加2.5甲，七杀真实加两次、余一杀");
		string armorText = RewardEffectRegistry.Compile(armor)!.Description;
		Check(armorText.Contains("7") && armorText.Contains("3") && armorText.Contains("2.5") && !armorText.Contains('{'), $"死铠说明共读表：{armorText}");
		await Reset();
		using var soul = Copy("mut_soul_harvest", ("Chance", 1), ("Heal", 7.5f), ("PickupRadius", 0.5f), ("LifetimeSeconds", 2), ("PickupDelaySeconds", 0.15f)); Grant(soul);
		// 护甲奖励会累计，回满也不清除；先按实读护甲／护盾与减伤计算足以打掉25生命的伤害。
		float incomingDamage = (_player.Armor + _player.Shield + 25) / (1 - _player.CombinedDamageReduction());
		Hit(_player, source: Target(40, 1000), damage: incomingDamage);
		float health = _player.Health; Check(health < _player.MaxHealth && !_player.IsDead, "真实挨打产生可回复生命缺口");
		var victim = Target(); Hit(victim); var pickup = Souls.Single();
		Check(pickup.GlobalPosition.IsEqualApprox(victim.GlobalPosition) && Mathf.IsEqualApprox(pickup.HealAmount, 7.5f) && Mathf.IsEqualApprox(pickup.PickupRadius, 0.5f) && Mathf.IsEqualApprox(pickup.LifetimeSeconds, 2) && Mathf.IsEqualApprox(pickup.PickupDelaySeconds, 0.15f), "真实死者位置掉落，回血量／拾取范围／存活／延迟都采用表内副本");
		Check(!pickup.GetChildren().OfType<CollisionObject3D>().Any() && pickup.GetGroups().Count == 0, "灵魂仍为无碰撞、无分组掉落物");
		string soulText = RewardEffectRegistry.Compile(soul)!.Description;
		Check(soulText.Contains("100%") && soulText.Contains("7.5") && !soulText.Contains('{'), $"灵魂说明共读概率与回血：{soulText}");
		_player.GlobalPosition = pickup.GlobalPosition; _player.Velocity = Vector3.Zero;
		Check(await Until(() => _player.Health > health, 1) && Mathf.IsEqualApprox(_player.Health, health + 7.5f), $"真实玩家接触拾取按表回血{health}→{_player.Health}，只回一次");
		Check(Souls.Length == 0, "拾取后灵魂回收");
		Hit(Target(35)); var expiring = Souls.Single(); Check(Souls.Length == 1, "第二次真实击杀在玩家五米外产生独立灵魂");
		Check(await Until(() => Souls.Length == 0, 3) && expiring.Expired && !expiring.PickedUp, "无人接触的灵魂按副本两秒寿命回收，区别于拾取消失");
	}
}
