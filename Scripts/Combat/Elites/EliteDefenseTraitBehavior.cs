using System;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>防御行为只拥有自己的修正器、规则和视觉，退出不改写其他来源。</summary>
public abstract class EliteDefenseTraitBehavior : EliteTraitBehavior, IIncomingHitRule
{
	private Node3D? _aura;
	protected EliteDefenseTraitBehavior(EliteTraitDefinition definition, Unit owner, EliteTraitWave wave) : base(definition, owner, wave) { }
	public virtual int Priority => 0;
	public virtual bool Blocks(HitInstance hit) => false;
	public virtual void OnBlocked(HitInstance hit) { }
	public virtual float AdjustDamage(HitInstance hit, float damage) => damage;
	public override void Attach() => Owner.AddIncomingHitRule(this);
	protected void Modifier(string stat, float value) => Owner.Stats.AddModifier(new StatModifier { Stat = stat, Operation = ModifierOperation.Multiply, Value = value, Source = this });
	protected void Aura(Color color, bool dome = false)
	{
		_aura = EliteTraitVisual.Aura(Owner, Mathf.Max(.7f, dome ? Def.Radius : .9f), color, dome);
	}
	protected void ShowAura(bool visible) { if (_aura != null && GodotObject.IsInstanceValid(_aura)) _aura.Visible = visible; }
	protected bool SourceOutside(HitInstance hit)
	{
		if (hit.Attacker == null || !GodotObject.IsInstanceValid(hit.Attacker)) return true;
		Vector3 offset = hit.Attacker.GlobalPosition - Owner.GlobalPosition;
		return new Vector2(offset.X, offset.Z).LengthSquared() > Def.Radius * Def.Radius || Mathf.Abs(offset.Y) > Def.Radius;
	}
	public override void Dispose()
	{
		if (GodotObject.IsInstanceValid(Owner)) { Owner.RemoveIncomingHitRule(this); Owner.Stats.RemoveBySource(this); Owner.RefreshStats(); }
		if (_aura != null && GodotObject.IsInstanceValid(_aura)) _aura.QueueFree(); _aura = null;
	}
}
