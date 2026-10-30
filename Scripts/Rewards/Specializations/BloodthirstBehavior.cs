using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>连续击杀刷新暴击时长，各次恢复独立叠加，保持原结算顺序。</summary>
public sealed class BloodthirstBehavior : SpecializationBehavior
{
	private readonly float _duration, _healFraction, _critChance;
	private readonly List<(float Rate, float Remaining)> _heals = new();
	private float _remaining;
	public override Color? StatusColor => !IsDisposed && _remaining > 0 ? new Color(1f, .35f, .4f, .8f) : null;
	public BloodthirstBehavior(Unit owner, float duration, float healFraction, float critChance) : base(owner)
	{ _duration = duration; _healFraction = healFraction; _critChance = critChance; }
	public override float Parameter(string key, float fallback) => key switch { "Duration" => _duration, "HealFraction" => _healFraction, "CritChance" => _critChance, _ => fallback };
	public override void OnKill()
	{
		if (IsDisposed || Owner.IsDead) return;
		_remaining = _duration; Owner.Stats.RemoveBySource(this);
		Owner.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Value = _critChance, Source = this });
		if (_duration > 0) _heals.Add((Owner.MaxHealth * _healFraction / _duration, _duration));
	}
	public override void Tick(float delta)
	{
		if (IsDisposed || Owner.IsDead) return;
		if (_remaining > 0) { _remaining -= delta; if (_remaining <= 0) Owner.Stats.RemoveBySource(this); }
		for (int i = _heals.Count - 1; i >= 0; i--)
		{
			var heal = _heals[i]; Owner.Heal(heal.Rate * Mathf.Min(delta, heal.Remaining));
			if (heal.Remaining <= delta) _heals.RemoveAt(i); else _heals[i] = (heal.Rate, heal.Remaining - delta);
		}
	}
	protected override void Remove() { _remaining = 0; _heals.Clear(); if (GodotObject.IsInstanceValid(Owner)) Owner.Stats.RemoveBySource(this); }
}
