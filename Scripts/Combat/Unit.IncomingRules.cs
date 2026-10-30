using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>命中事实与伤害分开；先作纯防御检查，通过后才分配防御前伤害。</summary>
public interface IIncomingHitRule
{
	int Priority => 0;
	bool Blocks(HitInstance hit) => false;
	void OnBlocked(HitInstance hit) { }
	float AdjustDamage(HitInstance hit, float damage) => damage;
}

public partial class Unit
{
	public void RaiseBlocked(HitInstance hit) => GameplayEventBus.Raise(new GameplayEvent
	{
		Kind = GameplayEventKind.Blocked, Source = this, Target = hit.Attacker, Value = hit.Damage,
		Attack = hit.Attack, IsSecondary = hit.Attack?.IsSecondary == true,
		Position = GlobalPosition, Context = hit.Context,
	});
	private readonly List<IIncomingHitRule> _incomingHitRules = new();
	private IIncomingHitRule[] _incomingHitSnapshot = Array.Empty<IIncomingHitRule>();
	private readonly HashSet<object> _traitControlSources = new(ReferenceEqualityComparer.Instance);
	private readonly HashSet<object> _actionLockSources = new(ReferenceEqualityComparer.Instance);
	public bool HasTraitControlImmunity => _traitControlSources.Count > 0;
	public bool HasActionLock
	{
		get
		{
			if (_actionLockSources.Count > 0) return true;
			foreach (var status in _statuses) if (status.Remaining > 0 && status.Effect.PreventsActions) return true;
			return false;
		}
	}
	public void AddActionLock(object source) => _actionLockSources.Add(source);
	public void RemoveActionLock(object source) => _actionLockSources.Remove(source);
	public void AddIncomingHitRule(IIncomingHitRule rule)
	{
		if (_incomingHitRules.Contains(rule)) return;
		_incomingHitRules.Add(rule); _incomingHitRules.Sort((a, b) => a.Priority.CompareTo(b.Priority)); _incomingHitSnapshot = _incomingHitRules.ToArray();
	}
	public void RemoveIncomingHitRule(IIncomingHitRule rule) { if (_incomingHitRules.Remove(rule)) _incomingHitSnapshot = _incomingHitRules.ToArray(); }
	public void AddTraitControlImmunity(object source)
	{
		_traitControlSources.Add(source);
		for (int i = _statuses.Count - 1; i >= 0; i--)
			if (IsControlStatus(_statuses[i].Effect)) { Stats.RemoveBySource(_statuses[i]); _statuses.RemoveAt(i); }
		RefreshStats();
		StatusesChanged?.Invoke();
	}
	public void RemoveTraitControlImmunity(object source) => _traitControlSources.Remove(source);
	/// <summary>供链接选择接收者；不消耗一次性护罩，也不触发命中订阅。</summary>
	public bool CanReceiveDamageFrom(HitInstance hit)
	{
		if (IsDead || Health <= 0 || IsQueuedForDeletion() || IsInvulnerable || hit.Negated) return false;
		foreach (var rule in _incomingHitSnapshot) if (rule.Blocks(hit)) return false;
		return true;
	}
	private bool BlockIncomingByRule(HitInstance hit)
	{
		foreach (var rule in _incomingHitSnapshot)
			if (rule.Blocks(hit)) { rule.OnBlocked(hit); return true; }
		return false;
	}
	private float AdjustIncomingByRules(HitInstance hit, float damage)
	{
		if (hit.Transferred) return damage;
		foreach (var rule in _incomingHitSnapshot) damage = Mathf.Max(0, rule.AdjustDamage(hit, damage));
		return damage;
	}
	private static bool IsControlStatus(StatusEffect effect)
	{
		if (effect.IsControl || effect.PreventsActions) return true;
		foreach (var modifier in effect.Modifiers)
			if (modifier.Stat is "MoveSpeedMultiplier" or "AttackSpeedMultiplier"
				&& (modifier.Operation == ModifierOperation.Add ? modifier.Value < 0 : modifier.Value < 1)) return true;
		return false;
	}
}
