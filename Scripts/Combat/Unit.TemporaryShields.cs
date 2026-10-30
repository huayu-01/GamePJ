using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ActRoguelike.Combat;

public partial class Unit
{
	private sealed class ShieldLease { public float Amount, Remaining; }
	private readonly Dictionary<object, ShieldLease> _temporaryShields = new();
	private readonly List<object> _expiredShieldSources = new();
	public float TemporaryShield { get { float total=0; foreach(var shield in _temporaryShields.Values) total+=shield.Amount; return total; } }
	public float TemporaryShieldFrom(object source) => _temporaryShields.TryGetValue(source, out var shield) ? shield.Amount : 0;
	public float TemporaryShieldSecondsFrom(object source) => _temporaryShields.TryGetValue(source, out var shield) ? shield.Remaining : 0;
	/// <summary>来源独立的临时护盾，不修改永久护盾上限，也不触发招架或格挡硬直。</summary>
	public void GrantTemporaryShield(object source, float amount, float cap, float seconds)
	{
		if (IsDead || !float.IsFinite(amount) || !float.IsFinite(cap) || !float.IsFinite(seconds) || amount <= 0 || cap <= 0 || seconds <= 0) return;
		if (!_temporaryShields.TryGetValue(source, out var shield)) _temporaryShields[source] = shield = new ShieldLease();
		shield.Amount = Mathf.Min(cap, shield.Amount + amount); shield.Remaining = seconds;
	}
	public void RemoveTemporaryShield(object source) => _temporaryShields.Remove(source);
	private float ConsumeTemporaryShield(float amount)
	{
		float consumed = 0;
		foreach (var shield in _temporaryShields.Values)
		{
			float loss = Mathf.Min(amount - consumed, shield.Amount); shield.Amount -= loss; consumed += loss;
			if (consumed >= amount) break;
		}
		return consumed;
	}
	private void TickTemporaryShields(float delta)
	{
		if (_temporaryShields.Count == 0) return;
		_expiredShieldSources.Clear();
		foreach (var pair in _temporaryShields)
		{
			var shield = pair.Value; shield.Remaining -= delta;
			if (shield.Remaining <= 0 || shield.Amount <= 0 || IsDead) _expiredShieldSources.Add(pair.Key);
		}
		foreach (var source in _expiredShieldSources) _temporaryShields.Remove(source);
		_expiredShieldSources.Clear();
	}
}
