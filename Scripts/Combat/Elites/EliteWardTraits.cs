using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>多个庇护来源合并成一个挡伤名额，同一出手的短多弹窗口只消耗一次。</summary>
internal sealed class EliteHitWard : IIncomingHitRule
{
	private readonly Unit _unit;
	private readonly Dictionary<object, (float Until, Func<float> Clock)> _sources = new(ReferenceEqualityComparer.Instance);
	private Node3D? _visual;
	private long _blockedCast = -1;
	private float _blockedUntil;
	private Func<float>? _blockedClock;
	private object? _blockedSource;
	public int Priority => 10;
	public EliteHitWard(Unit unit) { _unit = unit; unit.AddIncomingHitRule(this); }
	public bool Active
	{
		get
		{
			foreach (var pair in _sources.ToArray()) if (pair.Value.Clock() >= pair.Value.Until) _sources.Remove(pair.Key);
			bool active = _sources.Count > 0;
			if (_visual != null && GodotObject.IsInstanceValid(_visual)) _visual.Visible = active;
			return active;
		}
	}
	public void Grant(object source, float duration, Func<float> clock)
	{
		_sources[source] = (clock() + Mathf.Max(.1f, duration), clock);
		_visual ??= EliteTraitVisual.Aura(_unit, .85f, new Color(.3f, .8f, 1), true);
		if (GodotObject.IsInstanceValid(_visual)) _visual.Visible = true;
	}
	public void Remove(object source)
	{
		_sources.Remove(source);
		if (ReferenceEquals(_blockedSource, source)) { _blockedClock = null; _blockedSource = null; _blockedCast = -1; }
		_ = Active;
	}
	public bool Blocks(HitInstance hit) => hit.Damage > 0 && (Active || hit.Attack != null && hit.Attack.CastId == _blockedCast && _blockedClock != null && _blockedClock() < _blockedUntil);
	public void OnBlocked(HitInstance hit)
	{
		if (_sources.Count == 0) return;
		var first = _sources.First(); _blockedSource = first.Key; _blockedClock = first.Value.Clock;
		_blockedCast = hit.Attack?.CastId ?? -1; _blockedUntil = _blockedClock() + .15f;
		_sources.Clear(); _ = Active;
	}
}

public sealed class SaviorTraitBehavior : EliteDefenseTraitBehavior
{
	private float _remaining;
	private readonly HashSet<Unit> _granted = new();
	private readonly Func<float> _clock;
	public int TotalGranted { get; private set; }
	public SaviorTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) => _clock = () => Wave.Time;
	public override void Attach() { base.Attach(); Aura(new Color(.35f, .8f, 1)); }
	public override void Tick(float dt)
	{
		foreach (var unit in _granted) if (GodotObject.IsInstanceValid(unit)) unit.RefreshEliteHitWard();
		_remaining -= dt; if (_remaining > 0) return; _remaining = Mathf.Max(.5f, Def.Cooldown);
		foreach (var unit in Wave.LiveMembers().Where(u => !ReferenceEquals(u, Owner) && u.Faction == Owner.Faction
			&& u.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= Def.Radius * Def.Radius).Take(Math.Max(1, Def.Count)))
		{
			unit.GrantEliteHitWard(this, Def.Duration, _clock); _granted.Add(unit); TotalGranted++;
		}
	}
	public override void Dispose()
	{
		foreach (var unit in _granted) if (GodotObject.IsInstanceValid(unit)) unit.RemoveEliteHitWard(this);
		_granted.Clear(); base.Dispose();
	}
}
