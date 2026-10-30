using System;
using System.Linq;
using Godot;

namespace ActRoguelike.Combat.Elites;

public sealed class BruiserTraitBehavior : EliteDefenseTraitBehavior
{
	public BruiserTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach()
	{
		base.Attach(); float before = Owner.MaxHealth;
		Modifier("MaxHealthMultiplier", Mathf.Clamp(Def.Magnitude, 1, 1.5f)); Owner.RefreshStats();
		Owner.Heal(Mathf.Max(0, Owner.MaxHealth - before)); Aura(new Color(.9f, .65f, .18f));
	}
}

public sealed class SluggerTraitBehavior : EliteDefenseTraitBehavior
{
	public SluggerTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach() { base.Attach(); Modifier("DamageMultiplier", Mathf.Clamp(Def.Magnitude, 1, 1.5f)); Owner.RefreshStats(); Aura(new Color(1, .32f, .15f)); }
}

public sealed class JuggernautTraitBehavior : EliteDefenseTraitBehavior
{
	public JuggernautTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach()
	{
		base.Attach(); Owner.AddTraitControlImmunity(this); Modifier("MoveSpeedMultiplier", Mathf.Clamp(Def.Magnitude, .5f, 1));
		Owner.RefreshStats(); Aura(new Color(.75f, .78f, .85f));
	}
	public override void Dispose() { if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveTraitControlImmunity(this); base.Dispose(); }
}

public sealed class AvengerTraitBehavior : EliteDefenseTraitBehavior
{
	public int Empowerments { get; private set; }
	public AvengerTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override void Attach() { base.Attach(); Wave.MemberDied += OnMemberDied; Aura(new Color(1, .2f, .35f)); }
	private void OnMemberDied(Unit unit, HitInstance hit)
	{
		if (ReferenceEquals(unit, Owner) || Owner.IsDead || Wave.IsStopped || Empowerments >= Def.Count || unit.HasMeta("EliteSummoned")) return;
		Empowerments++;
		Owner.Stats.RemoveBySource(this);
		float scale = Mathf.Pow(Mathf.Clamp(Def.Magnitude, 1, 1.25f), Empowerments);
		Modifier("DamageMultiplier", Mathf.Min(1.8f, scale));
		Modifier("MoveSpeedMultiplier", Mathf.Min(1.3f, 1 + .07f * Empowerments)); Owner.RefreshStats();
	}
	public override void Dispose() { Wave.MemberDied -= OnMemberDied; base.Dispose(); }
}

public sealed class VampiricTraitBehavior : EliteDefenseTraitBehavior
{
	private float _armedAt;
	public float TotalHealed { get; private set; }
	public VampiricTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public bool Armed => Wave.Time >= _armedAt;
	public override void Attach() { base.Attach(); Aura(new Color(.85f, .08f, .25f)); }
	public override void Tick(float dt) => ShowAura(Armed);
	public override void OnFact(GameplayEvent fact)
	{
		if (!Armed || fact.Kind != GameplayEventKind.Damaged || !ReferenceEquals(fact.Source, Owner) || fact.Target == null
			|| !Owner.IsHostileTo(fact.Target) || fact.Value <= 0 || fact.IsSecondary || fact.IsDamageOverTime || fact.Attack?.IsSecondary == true) return;
		_armedAt = Wave.Time + Mathf.Max(.5f, Def.Cooldown);
		TotalHealed += Owner.Heal(fact.Value * Mathf.Clamp(Def.Magnitude, 0, .5f));
	}
}
