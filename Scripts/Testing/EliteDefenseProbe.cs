using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>通过真实单位命中、同波归属与来源撤销检查14项防御／属性特性。</summary>
public partial class EliteDefenseProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "EliteDefense";
	private Vector3 _point;
	private static bool Near(float a, float b) => Mathf.Abs(a - b) < .025f;
	private T Behavior<T>(Unit unit) where T : EliteTraitBehavior => (T)EliteTraitRuntime.Find(unit)!.Behavior;
	private HitInstance Hit(Unit target, float damage, Unit? attacker = null, AttackInstance? attack = null) => new()
	{
		Target = target, Attacker = attacker ?? Player, Attack = attack,
		Context = EventContext.Create(attacker ?? Player, target), Damage = damage,
		Direction = Vector3.Right, Position = target.GlobalPosition,
	};
	private static void Finish(EliteTraitWave wave, params Unit[] extra)
	{
		var members = wave.Members.Concat(extra).Distinct().ToArray(); wave.Stop(); wave.World.Clear(); wave.World.QueueFree(); wave.QueueFree();
		foreach (var unit in members) if (GodotObject.IsInstanceValid(unit)) unit.QueueFree();
	}
	protected override async Task RunChecks()
	{
		_point = Player.GlobalPosition + Vector3.Left * 1.5f + Vector3.Forward * 4;
		Attributes(); Wards(); Clanger(); Fog(); Shielding(); Suppression(); Links(); Avenger(); Juggernaut(); Vampiric(); Reactive(); Reforge();
		var display = MakeEnemy(_point); var wave = MakeWave(Definition("spiller"), display);
		await Wait(.12); await Shot("defense-fog"); Finish(wave);
	}
	private void Attributes()
	{
		var bruiser = MakeEnemy(_point); float health = bruiser.MaxHealth;
		var wave = MakeWave(Definition("bruiser"), bruiser);
		Check(bruiser.MaxHealth > health && Near(bruiser.Health, bruiser.MaxHealth), "巨壮增加生命并补齐新容量");
		wave.Stop(); Check(Near(bruiser.MaxHealth, health), "巨壮来源撤销恢复生命上限"); Finish(wave, bruiser);
		var slugger = MakeEnemy(_point); float damage = slugger.Stats.Get("DamageMultiplier");
		wave = MakeWave(Definition("slugger"), slugger);
		Check(slugger.Stats.Get("DamageMultiplier") > damage, "强击提高未来攻击快照倍率");
		wave.Stop(); Check(Near(slugger.Stats.Get("DamageMultiplier"), damage), "强击仅撤销本来源倍率"); Finish(wave, slugger);
	}
	private void Wards()
	{
		var source = MakeEnemy(_point); var ally = MakeEnemy(_point + Vector3.Right);
		var def = Definition("savior"); def.Duration = 2;
		var wave = MakeWave(def, source, ally); var behavior = Behavior<SaviorTraitBehavior>(source); behavior.Tick(.01f);
		Check(ally.HasEliteHitWard && !source.HasEliteHitWard, "庇护只给附近同波友军且不自套护罩");
		ally.ReceiveHit(Hit(ally, 0)); Check(ally.HasEliteHitWard, "零伤害命中不消耗一次挡伤名额");
		int recoil = ally.Reactions!.BlockStunCount;
		var volley = new AttackInstance { Attacker = Player, BaseDamage = 20 };
		var first = ally.ReceiveHit(Hit(ally, 20, attack: volley)); var second = ally.ReceiveHit(Hit(ally, 20, attack: volley));
		Check(first.Invulnerable && second.Invulnerable && !ally.HasEliteHitWard, "一次护罩抵消同出手的短多弹窗口并消耗名额");
		Check(ally.Reactions.BlockStunCount == recoil, "效果护罩不触发格挡者自身硬直");
		Check(ally.ReceiveHit(Hit(ally, 20, attack: new AttackInstance { Attacker = Player })).HealthLost > 0, "新出手不能重复使用已消耗护罩");
		object independent = new(); ally.GrantEliteHitWard(independent, 10, () => wave.Time);
		behavior.Tick(def.Cooldown + .1f); wave.Stop();
		Check(ally.HasEliteHitWard, "庇护撤销保留独立来源护罩"); ally.RemoveEliteHitWard(independent);
		Check(!ally.HasEliteHitWard, "全部来源撤销后护罩失效"); Finish(wave, source, ally);
	}
	private void Clanger()
	{
		var owner = MakeEnemy(_point); var protector = MakeEnemy(_point + Vector3.Right); var outsider = MakeEnemy(_point + Vector3.Left);
		var wave = MakeWave(Definition("clanger"), owner, protector);
		Check(owner.ReceiveHit(Hit(owner, 30)).Invulnerable, "压境保护对象存活时拒绝伤害");
		outsider.ForceKill(Player); Check(Behavior<ClangerTraitBehavior>(owner).Protected, "外部敌人死亡不改变压境组");
		protector.ForceKill(Player); Check(owner.ReceiveHit(Hit(owner, 30)).HealthLost > 0, "本波保护组清空后压境立即解除"); Finish(wave); outsider.QueueFree();
		owner = MakeEnemy(_point); wave = MakeWave(Definition("clanger"), owner);
		Check(!Behavior<ClangerTraitBehavior>(owner).Protected, "空保护组不制造无法解锁的无敌"); Finish(wave);
	}
	private void Fog()
	{
		Vector3 original = Player.GlobalPosition; var owner = MakeEnemy(_point); var wave = MakeWave(Definition("spiller"), owner);
		Player.GlobalPosition = owner.GlobalPosition + Vector3.Right * 7;
		Check(owner.ReceiveHit(Hit(owner, 30)).Invulnerable, "黑雾拒绝罩外攻击者伤害");
		Player.GlobalPosition = owner.GlobalPosition + Vector3.Right;
		Check(owner.ReceiveHit(Hit(owner, 30)).HealthLost > 0, "玩家进黑雾后可以造成真实伤害");
		owner.IsInvulnerable = true; wave.Stop(); Check(owner.IsInvulnerable, "移除黑雾不清除其他来源原有无敌");
		Player.GlobalPosition = original; Finish(wave, owner);
	}
	private void Shielding()
	{
		var owner = MakeEnemy(_point); var def = Definition("shielding"); var wave = MakeWave(def, owner); var behavior = Behavior<ShieldingTraitBehavior>(owner);
		Check(owner.ReceiveHit(Hit(owner, 20)).HealthLost > 0, "间歇无敌起手仍有开放期");
		behavior.Tick(def.WarningSeconds + .01f); Check(owner.ReceiveHit(Hit(owner, 20)).Invulnerable, "间歇无敌生效期挡伤");
		behavior.Tick(def.Duration + .01f); Check(owner.ReceiveHit(Hit(owner, 20)).HealthLost > 0, "间歇无敌结束后重新开放"); Finish(wave);
	}
	private void Suppression()
	{
		Vector3 original = Player.GlobalPosition; var owner = MakeEnemy(_point); var def = Definition("suppressor"); var wave = MakeWave(def, owner);
		Behavior<SuppressorTraitBehavior>(owner).Tick(def.WarningSeconds + .01f); Player.GlobalPosition = owner.GlobalPosition + Vector3.Right * 7;
		Check(owner.ReceiveHit(Hit(owner, 20, attack: new AttackInstance { Attacker = Player, Tags = new[] { "Projectile" } })).Invulnerable, "压制挡住来自罩外的投射物命中");
		Check(owner.ReceiveHit(Hit(owner, 20, attack: new AttackInstance { Attacker = Player, Tags = new[] { "Melee" } })).HealthLost > 0, "压制不误挡非投射攻击");
		Check(owner.ReceiveHit(Hit(owner, 20, attack: new AttackInstance { Attacker = Player, Tags = new[] { "Projectile", "Explosion" } })).HealthLost > 0, "独立爆炸按约定不作为直击投射物阻挡");
		Player.GlobalPosition = owner.GlobalPosition + Vector3.Right;
		Check(owner.ReceiveHit(Hit(owner, 20, attack: new AttackInstance { Attacker = Player, Tags = new[] { "Projectile" } })).HealthLost > 0, "压制允许罩内来源发射的投射物");
		Player.GlobalPosition = original; Finish(wave);
	}
	private void Links()
	{
		var owner = MakeEnemy(_point); var ally = MakeEnemy(_point + Vector3.Right); var invulnerable = MakeEnemy(_point + Vector3.Left); invulnerable.IsInvulnerable = true;
		var linkDefinition = Definition("health_link"); linkDefinition.MaxCarriers = 2;
		var wave = MakeWave(linkDefinition, owner, ally, invulnerable);
		using (var reservation = wave.Reserve(ally, true, float.MaxValue)) Check(reservation?.Commit(ally) == true, "同波第二链接承载者使用同一种特性");
		float beforeA = owner.Health, beforeB = ally.Health; int transferredHits = 0; bool secondary = false;
		void Observe(GameplayEvent fact)
		{
			if (!ReferenceEquals(fact.Target, ally)) return;
			if (fact.Kind == GameplayEventKind.Hit) transferredHits++;
			if (fact.Kind == GameplayEventKind.Damaged) secondary |= fact.IsSecondary;
		}
		GameplayEventBus.Raised += Observe;
		try { owner.ReceiveHit(Hit(owner, 90)); }
		finally { GameplayEventBus.Raised -= Observe; }
		Check(Near(beforeA - owner.Health, 45) && Near(beforeB - ally.Health, 45), "生命链接防御前等分且不递归，原始90点守恒");
		Check(Near(invulnerable.Health, invulnerable.MaxHealth), "链接排除无敌接收者，不吞掉伤害份额");
		Check(transferredHits == 0 && secondary, "链接转移标为衍生伤害，不触发新出手命中链");
		ally.GlobalPosition += Vector3.Right * 12; beforeA = owner.Health; beforeB = ally.Health;
		owner.ReceiveHit(Hit(owner, 30)); Check(Near(beforeA - owner.Health, 30) && Near(beforeB, ally.Health), "距离断开后不再分摊伤害"); Finish(wave);
	}
	private void Avenger()
	{
		var owner = MakeEnemy(_point); var a = MakeEnemy(_point + Vector3.Right); var b = MakeEnemy(_point + Vector3.Left); var c = MakeEnemy(_point + Vector3.Forward);
		var def = Definition("avenger"); def.Count = 2; var wave = MakeWave(def, owner, a, b, c); float before = owner.Stats.Get("DamageMultiplier");
		a.ForceKill(Player); b.ForceKill(Player); float capped = owner.Stats.Get("DamageMultiplier"); c.ForceKill(Player);
		Check(Behavior<AvengerTraitBehavior>(owner).Empowerments == 2 && capped > before && Near(capped, owner.Stats.Get("DamageMultiplier")), "复仇只按同波真实死亡成长并封顶");
		wave.Stop(); Check(Near(owner.Stats.Get("DamageMultiplier"), before), "复仇来源撤销不保留成长倍率"); Finish(wave, owner);
	}
	private void Juggernaut()
	{
		var owner = MakeEnemy(_point); var wave = MakeWave(Definition("juggernaut"), owner);
		int recoil = owner.Reactions!.BlockStunCount, hitStun = owner.Reactions.HitStunCount;
		Check(owner.HasTraitControlImmunity && !owner.ReceiveKnockback(Vector3.Right * 5), "强横独立来源阻止战斗击退");
		owner.ApplyStatus(new StatusEffect { Id = "EliteDefenseControl", Duration = 2, IsControl = true, PreventsActions = true });
		Check(!owner.HasStatus("EliteDefenseControl") && !owner.HasActionLock, "强横拒绝冻结和行动封锁状态");
		owner.ApplyStatus(new StatusEffect { Id = "EliteDefenseBurn", Duration = 2, DamagePerSecond = 1 });
		Check(owner.HasStatus("EliteDefenseBurn"), "强横仍可受到非控制持续伤害");
		owner.ReceiveHit(Hit(owner, 20)); Check(owner.Reactions.HitStunCount == hitStun, "强横无甲时也免普通受击硬直");
		owner.Reactions.BlockRecoil(.05f); Check(owner.Reactions.BlockStunCount == recoil + 1, "强横保留持盾者自身格挡硬直");
		wave.Stop(); Check(!owner.HasTraitControlImmunity && owner.ReceiveKnockback(Vector3.Right * 5), "强横来源撤销恢复击退入口"); Finish(wave, owner);
	}
	private void Vampiric()
	{
		var owner = MakeEnemy(_point); var def = Definition("vampiric"); var wave = MakeWave(def, owner); owner.ApplyDirectDamage(200, Player);
		float before = owner.Health; Player.IsInvulnerable = true;
		var attack = new AttackInstance { Attacker = owner, BaseDamage = 40, Tags = new[] { "Melee" } };
		Player.ReceiveHit(attack.RollHit(Player, Vector3.Right, Player.GlobalPosition));
		Check(Near(owner.Health, before), "吸血不从被无敌抵消的攻击回血"); Player.IsInvulnerable = false;
		Player.GrantArmor(100); Player.ReceiveHit(attack.RollHit(Player, Vector3.Right, Player.GlobalPosition));
		Check(Near(owner.Health, before), "吸血不从护甲损耗回血");
		float armorReduction = Player.ArmorDamageReduction; Player.ArmorDamageReduction = 0;
		Player.ReceiveHit(Hit(Player, Player.Armor, Player)); Player.ArmorDamageReduction = armorReduction;
		var result = Player.ReceiveHit(attack.RollHit(Player, Vector3.Right, Player.GlobalPosition));
		Check(result.HealthLost > 0 && Near(owner.Health - before, result.HealthLost * def.Magnitude), "吸血按实际生命损失恢复且不使用原始伤害");
		before = owner.Health; Player.ReceiveHit(attack.RollHit(Player, Vector3.Right, Player.GlobalPosition)); Check(Near(owner.Health, before), "吸血强化命中消费后有独立间隔"); Finish(wave);
	}
	private void Reactive()
	{
		var owner = MakeEnemy(_point); var def = Definition("pressure_rebound"); var wave = MakeWave(def, owner); var pressure = Behavior<PressureReboundTraitBehavior>(owner);
		owner.IsInvulnerable = true; owner.ReceiveHit(Hit(owner, 200)); Check(Near(pressure.Charge, 0), "蓄压不接受无敌抵消的虚假有效伤害"); owner.IsInvulnerable = false;
		owner.ReceiveHit(Hit(owner, def.Magnitude + 10)); pressure.Tick(.01f);
		Check(pressure.Pulses == 1 && wave.World.ActiveCount == 1 && Near(pressure.Charge, 0), "蓄压充满只生成一次带预警回震");
		owner.ReceiveHit(Hit(owner, def.Magnitude * 3)); pressure.Tick(.01f); Check(pressure.Pulses == 1, "蓄压冷却期不积压多次脉冲"); Finish(wave);
		owner = MakeEnemy(_point); def = Definition("armor_recoil"); owner.GrantArmor(20); owner.ArmorDamageReduction = 0;
		wave = MakeWave(def, owner); var recoil = Behavior<ArmorRecoilTraitBehavior>(owner); owner.ReceiveHit(Hit(owner, 20)); recoil.Tick(.01f);
		Check(recoil.Recoils == 1 && wave.World.ActiveCount == 1, "非致死破甲生成反冲危险"); recoil.Tick(def.WarningSeconds + .1f);
		float before = owner.Health; owner.ReceiveHit(Hit(owner, 20)); Check(recoil.Vulnerable && owner.Health < before - 20, "反冲后进入真实易伤窗口"); Finish(wave);
		owner = MakeEnemy(_point); owner.GrantArmor(20); owner.ArmorDamageReduction = 0; wave = MakeWave(Definition("armor_recoil"), owner); recoil = Behavior<ArmorRecoilTraitBehavior>(owner);
		owner.ReceiveHit(Hit(owner, owner.Health + 100)); Check(owner.IsDead && recoil.Recoils == 0 && wave.World.ActiveCount == 0, "致死破甲不执行活体反冲"); Finish(wave);
	}
	private void Reforge()
	{
		var owner = MakeEnemy(_point); owner.GrantArmor(20); owner.ArmorDamageReduction = 0;
		var def = Definition("reforge"); var wave = MakeWave(def, owner); var behavior = Behavior<ReforgeTraitBehavior>(owner);
		owner.ReceiveHit(Hit(owner, 20)); behavior.Tick(.01f); Check(behavior.Channeling && owner.HasActionLock, "破甲后修甲引导真实占用行动");
		owner.ReceiveHit(Hit(owner, 5)); Check(!behavior.Channeling && !owner.HasActionLock && behavior.Interruptions == 1 && owner.Armor == 0, "修甲可被有效普通攻击打断且不提前得甲");
		owner.Reactions!.Reset(); for (int i = 0; i < 160 && !behavior.Completed; i++) behavior.Tick(.1f);
		Check(behavior.Completed && Near(owner.Armor, def.Damage) && !owner.HasActionLock, "重铸仅修复配置量并释放行动锁");
		owner.ReceiveHit(Hit(owner, def.Damage)); owner.Reactions.Reset(); for (int i = 0; i < 100; i++) behavior.Tick(.1f);
		Check(owner.Armor == 0 && behavior.Completed, "每只最多成功一次，第二次破甲不再修复"); Finish(wave);
	}
}
