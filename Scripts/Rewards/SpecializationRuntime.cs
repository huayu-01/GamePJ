using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

/// <summary>单个玩家的特化状态；只在获得特化后挂载，退树成对解除所有订阅。</summary>
public partial class SpecializationRuntime : Node, IOutgoingDamageScale, IDeathAvoid
{
	public const string NodeName = "Specializations";
	private Unit _owner = null!;
	private readonly Dictionary<K, RewardConfig> _rows = new();
	private readonly List<Ability> _boundAbilities = new();
	private readonly List<GameplayEvent> _pendingHits = new();
	public int ChainCount { get; private set; }
	public int ThunderCount { get; private set; }
	public int SpearCount { get; private set; }
	public int AfterimageCount { get; private set; }
	public int FieldCount { get; private set; }
	public bool Has(K kind) => _rows.ContainsKey(kind);
	public float Value(K kind, string key, float fallback = 0f) => _rows.TryGetValue(kind, out var row)
		? row.HandlerParams.FirstOrDefault(p => p.Key == key)?.AsFloat(fallback) ?? fallback : fallback;
	public static SpecializationRuntime Ensure(Unit owner)
	{
		if (owner.GetNodeOrNull<SpecializationRuntime>(NodeName) is { } runtime) return runtime;
		var created = new SpecializationRuntime { Name = NodeName }; owner.AddChild(created); return created;
	}
	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		_owner.AddOutgoingDamageScale(this); _owner.AddDeathAvoid(this);
		_owner.Damaged += OnDamaged;
		GameplayEventBus.Raised += OnFact;
	}
	public void Apply(RewardConfig row)
	{
		if (row.Specialization == K.None || !_rows.TryAdd(row.Specialization, row)) return;
		if (row.Specialization == K.BloodArmor) _owner.ConfigureArmorHealthConversion(Value(K.BloodArmor, "ArmorHealthRatio", 1f));
		if (row.Specialization == K.WarHeart)
			_owner.Stats.AddConversion(new StatConversion("DamageReductionAdd", "CritMultiplierAdd", Value(K.WarHeart, "ConversionRatio"), row.ConvertedRewards.Select(r => r.Id).ToHashSet(), this));
		if (row.Specialization == K.ThunderPool)
			foreach (var target in row.SpecializationTargets)
				if (Ability.FindByAbilityId(_owner, target.Id) is { } ability && !_boundAbilities.Contains(ability))
				{
					_boundAbilities.Add(ability); ability.Activated += OnAbility;
				}
		if (row.Specialization == K.ThunderRecall)
		{
			var scythe = row.SpecializationTargets.Select(t => Ability.FindByAbilityId(_owner, t.Id)).OfType<ScytheThrowAbility>().FirstOrDefault();
			var fall = row.SpecializationTargets.Select(t => Ability.FindByAbilityId(_owner, t.Id)).OfType<FallAttackAbility>().FirstOrDefault();
			if (scythe != null && fall != null) scythe.ConfigureThunderRecall(Value(K.ThunderRecall, "SpeedScale"), fall, Value(K.ThunderRecall, "LandingDamageScale"));
		}
		if (row.Specialization == K.WindDomain)
		{
			_scythe = row.SpecializationTargets.Select(t => Ability.FindByAbilityId(_owner, t.Id)).OfType<ScytheThrowAbility>().FirstOrDefault();
			if (_scythe != null) _scythe.Settled += OnWeaponSettled;
		}
		if (row.Specialization == K.WildWind && _rows.TryGetValue(K.WindSword, out var sword))
			foreach (var projectile in GetTree().GetNodesInGroup("projectiles").OfType<Projectile>())
				if (projectile.CurrentAttack?.Attacker == _owner && projectile.CurrentAttack.Ability == sword.Id)
				{
					projectile.SweepRadius = Value(K.WindSword, "Radius") * WindSizeScale;
					projectile.Scale = Vector3.One * WindSizeScale;
				}
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner.IsDead) return;
		float dt = (float)delta;
		foreach (var fact in _pendingHits.ToArray()) ProcessPrimaryHit(fact);
		_pendingHits.Clear();
		TickThunder(dt); TickWar(dt); TickWind(dt);
	}
	private void OnFact(GameplayEvent fact)
	{
		if (_owner.IsDead) return;
		if (fact.Kind == GameplayEventKind.Kill && fact.Source == _owner) OnKill();
		if (fact.Kind == GameplayEventKind.Parried && fact.Source == _owner && fact.Target is { } attacker && Has(K.ThunderArmor))
			Retaliate(attacker);
		if (fact.Source != _owner || fact.IsDamageOverTime || fact.IsSecondary) return;
		if (fact.Kind == GameplayEventKind.Hit && fact.Target is { } hitTarget && IsInstanceValid(hitTarget) && _owner.IsHostileTo(hitTarget)) _pendingHits.Add(fact);
		if (fact.Kind == GameplayEventKind.Critical && Has(K.WarSpear)) FireProjectile(K.WarSpear, true);
	}
	private void ProcessPrimaryHit(GameplayEvent fact)
	{
		Unit? living = fact.Target is { } candidate && IsInstanceValid(candidate) && candidate.IsValidTarget && _owner.IsHostileTo(candidate) ? candidate : null;
		if (living != null && Has(K.ThunderMark)) Mark(living);
		if (Has(K.ThunderHeart)) Chain(living?.GlobalPosition ?? fact.Position, living, K.ThunderHeart);
		if (fact.Target is { } target && IsInstanceValid(target) && _owner.IsHostileTo(target))
		{
			if (target.IsValidTarget && Has(K.WarAfterimage) && GD.Randf() < Value(K.WarAfterimage, "Chance"))
			{
				AfterimageCount++;
				// 完整复制这一次攻击伤害，不能再乘一次出手倍率或巨人额外伤害。
				DirectHit(target, fact.Value, "WarAfterimage", fact.Context);
			}
		}
	}
	public float ScaleAgainst(Unit target, AttackInstance attack)
	{
		float scale = 1f;
		if (Has(K.BloodBattle)) scale *= 1f + (1f - Mathf.Clamp(_owner.Health / _owner.MaxHealth, 0f, 1f)) * Value(K.BloodBattle, "MaxDamageBonus");
		if (Has(K.Storm))
		{
			float speed = _owner.MoveSpeed * _owner.Stats.Get("MoveSpeedMultiplier");
			if (_owner.GetNodeOrNull<SprintController>("Sprint") is { IsSprinting: true } sprint) speed = sprint.EffectiveSpeed(speed);
			scale *= 1f + speed * Value(K.Storm, "DamagePerSpeed");
		}
		return scale;
	}
	private void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (Has(K.ThunderArmor) && (result.ArmorLost > 0f || result.Blocked) && hit.Attacker is { } attacker && _owner.IsHostileTo(attacker)) Retaliate(attacker);
	}
	private void OnAbility(Ability ability)
	{
		if (Has(K.ThunderPool) && ability.IsActive) SpawnField(K.ThunderPool, _owner.GlobalPosition);
	}
	private void DirectHit(Unit target, float damage, string tag, EventContext? context = null)
	{
		if (!target.IsValidTarget || !_owner.IsHostileTo(target) || damage <= 0f) return;
		EventContext? next = context == null ? EventContext.Create(_owner, target) : context.Child(_owner, target);
		if (next == null) return;
		var attack = new AttackInstance { Attacker = _owner, Ability = tag, BaseDamage = damage * (tag == "WarAfterimage" ? 1f : AttackOffense.Capture(_owner).DamageMultiplier), IsSecondary = true };
		float resolved = tag == "WarAfterimage" ? damage : attack.RollHit(target, Vector3.Zero, target.GlobalPosition).Damage;
		target.ReceiveHit(new HitInstance { Target = target, Attacker = _owner, Attack = attack, Context = next, Damage = resolved, Position = target.GlobalPosition });
	}
	public IEnumerable<Unit> Enemies(Vector3 center, float radius) => _owner.GetTree().GetNodesInGroup("enemy_units").OfType<Unit>()
		.Where(u => u.IsValidTarget && _owner.IsHostileTo(u) && u.GlobalPosition.DistanceSquaredTo(center) <= radius * radius).ToArray();
	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnFact;
		foreach (var target in _marks.Keys) if (IsInstanceValid(target)) target.RemoveStatus("ThunderMark");
		foreach (var ability in _boundAbilities) if (IsInstanceValid(ability)) ability.Activated -= OnAbility;
		if (_scythe != null && IsInstanceValid(_scythe)) _scythe.Settled -= OnWeaponSettled;
		if (!IsInstanceValid(_owner)) return;
		_owner.Damaged -= OnDamaged; _owner.RemoveOutgoingDamageScale(this); _owner.RemoveDeathAvoid(this);
		_owner.Stats.RemoveBySource(this); _owner.Stats.RemoveBySource(_sprintArmorSource); _owner.Stats.RemoveBySource(_bloodthirstSource);
	}
}
