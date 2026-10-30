using System;

namespace ActRoguelike.Combat.Elites;

/// <summary>每名承载者独立实例；生命周期由运行组件统一驱动。</summary>
public abstract class EliteTraitBehavior : IDisposable
{
	protected EliteTraitBehavior(EliteTraitDefinition definition, Unit owner, EliteTraitWave wave) { Def = definition; Owner = owner; Wave = wave; }
	public EliteTraitDefinition Def { get; }
	public Unit Owner { get; }
	public EliteTraitWave Wave { get; }
	public virtual void Attach() { }
	public virtual void Tick(float dt) { }
	public virtual void OnDamaged(DamageResult result, HitInstance hit) { }
	public virtual void OnFact(GameplayEvent fact) { }
	public virtual void OnDeath(HitInstance hit) { }
	public virtual void Dispose() { }
}
