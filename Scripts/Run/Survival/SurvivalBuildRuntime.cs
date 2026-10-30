using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>协调事实分发；每项行为与状态由独立规则持有，不按奖励Id分支。</summary>
public partial class SurvivalBuildRuntime : Node
{
	public SurvivalDirector Director { get; init; } = null!;
	public Unit Player { get; init; } = null!;
	private readonly List<SurvivalAugmentRule> _rules = new();
	private SurvivalAugmentRule[] _ruleSnapshot = Array.Empty<SurvivalAugmentRule>();
	public bool Stopped { get; private set; }
	public int RuleCount => _rules.Count;
	public T? Find<T>() where T : SurvivalAugmentRule => _rules.OfType<T>().FirstOrDefault(r => !r.Stopped);
	public float ExperienceScale => _rules.OfType<RiskContractRule>().Where(r => !r.Stopped).Aggregate(1f, (value, rule) => value * rule.ExperienceScale);
	public override void _Ready() => GameplayEventBus.Raised += OnFact;
	internal void Register(SurvivalAugmentRule rule) { _rules.Add(rule); _ruleSnapshot = _rules.ToArray(); RefreshPickup(); }
	internal void Unregister(SurvivalAugmentRule rule) { if (_rules.Remove(rule)) _ruleSnapshot = _rules.ToArray(); RefreshPickup(); }
	private void RefreshPickup()
	{
		if (Director.ExperienceField is not { } field || !IsInstanceValid(field)) return;
		field.PickupRadius = Director.Definition!.PickupRadius * _rules.OfType<ScavengerCompassRule>().Where(r => !r.Stopped).Aggregate(1f, (v, r) => v * r.RadiusScale);
		field.PickupSpeedScale = _rules.OfType<ScavengerCompassRule>().Where(r => !r.Stopped).Aggregate(1f, (v, r) => v * r.SpeedScale);
	}
	public static bool IsPrimary(AttackInstance attack) => attack.IsManualWeaponAttack && !attack.IsSecondary && !attack.IsDamageOverTime;
	public static bool IsDirect(AttackInstance attack) => (attack.IsManualWeaponAttack || attack.IsDirectAbilityAttack) && !attack.IsSecondary && !attack.IsDamageOverTime;
	private void OnFact(GameplayEvent fact)
	{
		if (Stopped || !Director.Active || Player.IsDead || fact.Kind != GameplayEventKind.Damaged || fact.Source != Player || fact.Attack == null
			|| !IsPrimary(fact.Attack) || fact.DamageApplied <= 0 || fact.Target == null || !Player.IsHostileTo(fact.Target)) return;
		foreach (var rule in _ruleSnapshot) if (!rule.Stopped) rule.PrimaryHit(fact);
	}
	public void Stop()
	{
		if (Stopped) return; Stopped = true; GameplayEventBus.Raised -= OnFact;
		foreach (var rule in _ruleSnapshot) rule.Stop(); _rules.Clear(); _ruleSnapshot = Array.Empty<SurvivalAugmentRule>();
	}
	public override void _ExitTree() => Stop();
}

public abstract partial class SurvivalAugmentRule : Node, IOutgoingDamageScale
{
	protected SurvivalDirector Director { get; }
	protected Unit Player { get; }
	protected SurvivalBuildRuntime Runtime => Director.BuildRuntime!;
	public bool Stopped { get; private set; }
	private readonly HashSet<long> _seen = new();
	private readonly Queue<long> _order = new();
	protected SurvivalAugmentRule(SurvivalDirector director, Unit player) { Director = director; Player = player; }
	public override void _Ready() { Runtime.Register(this); Player.AddOutgoingDamageScale(this); Start(); }
	protected virtual void Start() { }
	protected virtual void Removed() { }
	protected virtual void Tick(float delta) { }
	public virtual void PrimaryHit(GameplayEvent fact) { }
	protected bool Once(AttackInstance attack)
	{
		if (!_seen.Add(attack.CastId)) return false;
		_order.Enqueue(attack.CastId); if (_order.Count > 256) _seen.Remove(_order.Dequeue()); return true;
	}
	public float ScaleAgainst(Unit target, AttackInstance attack) => Stopped || !Director.Active || !SurvivalBuildRuntime.IsDirect(attack) ? 1 : DirectScale(target);
	protected virtual float DirectScale(Unit target) => 1;
	public override void _PhysicsProcess(double delta)
	{
		if (!Stopped && Director.Active && Director.State != SurvivalDirector.PhaseState.Preparing && !Player.IsDead && Player.ImpactPause?.IsHolding != true) Tick((float)delta);
	}
	public void Stop()
	{
		if (Stopped) return; Stopped = true;
		if (IsInstanceValid(Player)) { Player.RemoveOutgoingDamageScale(this); Player.RemoveTemporaryShield(this); Player.Stats.RemoveBySource(this); Player.RefreshStats(); }
		Removed(); _seen.Clear(); _order.Clear();
		foreach (Node child in GetChildren()) if (!child.IsQueuedForDeletion()) child.QueueFree();
		if (Director.BuildRuntime != null && IsInstanceValid(Director.BuildRuntime)) Runtime.Unregister(this);
		SetPhysicsProcess(false);
	}
	public override void _ExitTree() => Stop();
}
