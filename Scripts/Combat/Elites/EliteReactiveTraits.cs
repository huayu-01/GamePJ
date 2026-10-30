using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Combat.Elites;

public sealed class PressureReboundTraitBehavior : EliteDefenseTraitBehavior
{
	private float _cooldown;
	private bool _pending;
	private EliteHazardHandle? _pulse;
	public float Charge { get; private set; }
	public int Pulses { get; private set; }
	public PressureReboundTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach() { base.Attach(); Aura(new Color(1, .55f, .1f)); ShowAura(false); }
	public override void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (result.Killed || _pending || _cooldown > 0 || Owner.Health <= 0) return;
		Charge = Mathf.Min(Mathf.Max(1, Def.Magnitude), Charge + result.ArmorLost + result.ShieldLost + result.HealthLost);
		if (Charge >= Mathf.Max(1, Def.Magnitude)) { _pending = true; ShowAura(true); }
	}
	public override void Tick(float dt)
	{
		_cooldown = Mathf.Max(0, _cooldown - dt);
		if (!_pending || _cooldown > 0 || Owner.Health <= 0) return;
		_pulse = Wave.World.Circle(Owner, Owner.GlobalPosition, Def.Radius, Def.Damage, Def.WarningSeconds, .08f);
		if (_pulse == null) { _cooldown = .25f; return; }
		_pulse.Knockback = 3; Pulses++; _pending = false; Charge = 0; _cooldown = Mathf.Max(1, Def.Cooldown); ShowAura(false);
	}
	public override void Dispose() { _pulse?.Dispose(); _pulse = null; base.Dispose(); }
}

public sealed class ArmorRecoilTraitBehavior : EliteDefenseTraitBehavior
{
	private bool _pending, _vulnerable;
	private float _cooldown, _delay, _remaining;
	private EliteHazardHandle? _pulse;
	public int Recoils { get; private set; }
	public bool Vulnerable => _vulnerable;
	public ArmorRecoilTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach() { base.Attach(); Aura(new Color(1, .6f, .1f)); ShowAura(false); }
	public override void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (result.ArmorBroken && !result.Killed && Owner.Health > 0 && _cooldown <= 0) _pending = true;
	}
	public override void Tick(float dt)
	{
		_cooldown = Mathf.Max(0, _cooldown - dt);
		if (_pending && Owner.Health > 0)
		{
			_pulse = Wave.World.Circle(Owner, Owner.GlobalPosition, Def.Radius, Def.Damage, Def.WarningSeconds, .08f);
			if (_pulse != null) { _pulse.Knockback = 6; _pending = false; Recoils++; _cooldown = Mathf.Max(1, Def.Cooldown); _delay = Def.WarningSeconds + .08f; ShowAura(true); }
		}
		if (_delay > 0)
		{
			_delay -= dt;
			if (_delay <= 0) { Modifier("IncomingDamageMultiplier", Mathf.Clamp(Def.Magnitude, 1.1f, 1.5f)); _vulnerable = true; _remaining = Mathf.Max(.5f, Def.Duration); }
		}
		else if (_vulnerable)
		{
			_remaining -= dt;
			if (_remaining <= 0) { Owner.Stats.RemoveBySource(this); _vulnerable = false; ShowAura(false); }
		}
	}
	public override void Dispose() { _pulse?.Dispose(); _pulse = null; base.Dispose(); }
}

public sealed class ReforgeTraitBehavior : EliteDefenseTraitBehavior
{
	private readonly List<Ability> _abilities = new();
	private bool _pending;
	private float _remaining, _retry;
	public bool Channeling { get; private set; }
	public bool Completed { get; private set; }
	public int Interruptions { get; private set; }
	public ReforgeTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach()
	{
		base.Attach(); NodeSearch.CollectInSubtree(Owner, _abilities); Aura(new Color(.85f, .9f, 1)); ShowAura(false);
	}
	public override void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (result.Killed || Owner.Health <= 0) { EndChannel(); return; }
		if (Channeling && hit.Attack?.IsDamageOverTime != true && result.ArmorLost + result.ShieldLost + result.HealthLost > 0)
		{
			Interruptions++; EndChannel(); _pending = true; _retry = Mathf.Max(1, Def.Cooldown);
		}
		if (result.ArmorBroken && !Completed) _pending = true;
	}
	public override void Tick(float dt)
	{
		if (Completed) return;
		_retry = Mathf.Max(0, _retry - dt);
		if (!Channeling)
		{
			if (!_pending || _retry > 0 || Owner.Armor > 0 || Owner.Reactions?.IsStaggered == true || Owner.HasActionLock
				|| Owner.Movement?.IsDashing == true || Owner.Movement?.HasVelocityOverride == true) return;
			if (_abilities.Any(a => a.IsBusy && !a.IsInterruptibleNow)) return;
			foreach (var ability in _abilities) if (ability.IsBusy) ability.InterruptByControl();
			Owner.AddActionLock(this); Owner.Movement?.Stop(); Channeling = true; _remaining = Mathf.Max(Def.WarningSeconds, Def.Duration); ShowAura(true);
		}
		_remaining -= dt;
		if (_remaining > 0) return;
		Owner.GrantArmor(Mathf.Max(1, Def.Damage)); Completed = true; _pending = false; EndChannel();
	}
	private void EndChannel() { Channeling = false; Owner.RemoveActionLock(this); ShowAura(false); }
	public override void Dispose() { EndChannel(); _abilities.Clear(); base.Dispose(); }
}
