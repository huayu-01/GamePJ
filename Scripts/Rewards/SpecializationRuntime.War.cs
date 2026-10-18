using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

public partial class SpecializationRuntime
{
	private float _duelRemaining, _duelCooldown, _bloodthirstRemaining;
	private int _duelKills;
	private readonly object _bloodthirstSource = new();
	private readonly List<(float Rate, float Remaining)> _heals = new();
	public bool DuelActive => _duelRemaining > 0f;
	public int DuelKills => _duelKills;
	public float DuelCooldown => _duelCooldown;
	public bool TryAvoidDeath(Unit unit)
	{
		if (!Has(K.DeathDuel)) return false;
		if (DuelActive) return true;
		if (_duelCooldown > 0f) return false;
		_duelRemaining = Value(K.DeathDuel, "Duration");
		_duelCooldown = Value(K.DeathDuel, "Cooldown");
		_duelKills = 0;
		return DuelActive;
	}
	private void OnKill()
	{
		if (DuelActive && ++_duelKills >= (int)Value(K.DeathDuel, "RequiredKills"))
		{
			_duelRemaining = 0f;
			_owner.RestoreHealthForDeathAvoid(_owner.MaxHealth * Value(K.DeathDuel, "HealFraction"));
		}
		if (Has(K.Bloodthirst))
		{
			float duration = Value(K.Bloodthirst, "Duration");
			_bloodthirstRemaining = duration;
			_owner.Stats.RemoveBySource(_bloodthirstSource);
			_owner.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Value = Value(K.Bloodthirst, "CritChance"), Source = _bloodthirstSource });
			if (duration > 0f) _heals.Add((_owner.MaxHealth * Value(K.Bloodthirst, "HealFraction") / duration, duration));
		}
	}
	private void TickWar(float dt)
	{
		_duelCooldown = Mathf.Max(0f, _duelCooldown - dt);
		if (DuelActive)
		{
			_duelRemaining -= dt;
			if (_duelRemaining <= 0f) { _owner.ForceKill(); return; }
		}
		if (_bloodthirstRemaining > 0f)
		{
			_bloodthirstRemaining -= dt;
			if (_bloodthirstRemaining <= 0f) _owner.Stats.RemoveBySource(_bloodthirstSource);
		}
		for (int i = _heals.Count - 1; i >= 0; i--)
		{
			var heal = _heals[i];
			_owner.Heal(heal.Rate * Mathf.Min(dt, heal.Remaining));
			if (heal.Remaining <= dt) _heals.RemoveAt(i); else _heals[i] = (heal.Rate, heal.Remaining - dt);
		}
	}
}
