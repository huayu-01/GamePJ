using System;
using ActRoguelike.Combat.Elites;

namespace ActRoguelike.Combat;

public partial class Unit
{
	private EliteHitWard? _eliteWard;
	public bool HasEliteHitWard => _eliteWard?.Active == true;
	public void GrantEliteHitWard(object source, float duration, Func<float> clock)
	{
		_eliteWard ??= new EliteHitWard(this); _eliteWard.Grant(source, duration, clock);
	}
	public void RemoveEliteHitWard(object source) => _eliteWard?.Remove(source);
	internal void RefreshEliteHitWard() { if (_eliteWard != null) _ = _eliteWard.Active; }
}
