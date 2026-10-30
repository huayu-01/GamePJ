using System;
using System.Linq;
using Godot;

namespace ActRoguelike.Combat.Elites;

public sealed class ClangerTraitBehavior : EliteDefenseTraitBehavior
{
	public ClangerTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public bool Protected => !Wave.IsStopped && Wave.LiveMembers().Any(u => !ReferenceEquals(u, Owner));
	public override bool Blocks(HitInstance hit) => Protected;
	public override void Attach() { base.Attach(); Aura(new Color(.95f, .8f, .25f), true); }
	public override void Tick(float dt) => ShowAura(Protected);
}

public sealed class SpillerTraitBehavior : EliteDefenseTraitBehavior
{
	public SpillerTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override bool Blocks(HitInstance hit) => SourceOutside(hit);
	public override void Attach() { base.Attach(); Aura(new Color(.27f, .15f, .48f), true); }
}

public sealed class ShieldingTraitBehavior : EliteDefenseTraitBehavior
{
	private float _elapsed;
	public bool Protected => _elapsed >= Def.WarningSeconds && _elapsed < Def.WarningSeconds + Def.Duration;
	public ShieldingTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override bool Blocks(HitInstance hit) => Protected;
	public override void Attach() { base.Attach(); Aura(new Color(.75f, .8f, 1), true); ShowAura(false); }
	public override void Tick(float dt)
	{
		_elapsed += dt;
		if (_elapsed >= Def.WarningSeconds + Def.Duration + Mathf.Max(2, Def.Cooldown)) _elapsed = 0;
		ShowAura(Protected);
	}
}

public sealed class SuppressorTraitBehavior : EliteDefenseTraitBehavior
{
	private float _elapsed;
	public bool Protected => _elapsed >= Def.WarningSeconds && _elapsed < Def.WarningSeconds + Def.Duration;
	public SuppressorTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override bool Blocks(HitInstance hit) => Protected && hit.Attack is { } a && a.HasTag("Projectile") && !a.HasTag("Explosion") && SourceOutside(hit);
	public override void Attach() { base.Attach(); Aura(new Color(.2f, .65f, 1), true); ShowAura(false); }
	public override void Tick(float dt)
	{
		_elapsed += dt;
		if (_elapsed >= Def.WarningSeconds + Def.Duration + Mathf.Max(2, Def.Cooldown)) _elapsed = 0;
		ShowAura(Protected);
	}
}

public sealed class HealthLinkTraitBehavior : EliteDefenseTraitBehavior
{
	private readonly System.Collections.Generic.List<MeshInstance3D> _links = new();
	private float _visualTimer;
	public int Transfers { get; private set; }
	public HealthLinkTraitBehavior(EliteTraitDefinition d, Unit o, EliteTraitWave w) : base(d, o, w) { }
	public override int Priority => 20;
	public override void Attach()
	{
		base.Attach(); Aura(new Color(1, .3f, .65f));
		for (int i = 0; i < Math.Min(8, Math.Max(1, Def.Count)); i++)
		{
			var line = new MeshInstance3D { Name = "HealthLink", TopLevel = true, Visible = false,
				Mesh = new CylinderMesh { Height = 1, TopRadius = .035f, BottomRadius = .035f, RadialSegments = 8 },
				MaterialOverride = EliteTraitVisual.Material(new Color(1, .25f, .6f, .75f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			Owner.AddChild(line); _links.Add(line);
		}
	}
	public override void Tick(float dt)
	{
		_visualTimer -= dt; if (_visualTimer > 0) return; _visualTimer = .1f;
		var targets = Wave.LiveMembers().Where(u => !ReferenceEquals(u, Owner) && u.Faction == Owner.Faction
			&& u.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= Def.Radius * Def.Radius
			&& Wave.World.HasSight(Owner.GlobalPosition + Vector3.Up, u.GlobalPosition + Vector3.Up)).Take(_links.Count).ToArray();
		for (int i = 0; i < _links.Count; i++)
		{
			var line = _links[i]; if (!GodotObject.IsInstanceValid(line)) continue;
			line.Visible = i < targets.Length; if (!line.Visible) continue;
			Vector3 a = Owner.GlobalPosition + Vector3.Up, b = targets[i].GlobalPosition + Vector3.Up, delta = b - a;
			if (delta.LengthSquared() < .001f) { line.Visible = false; continue; }
			Vector3 y = delta.Normalized(), x = y.Cross(Mathf.Abs(y.Dot(Vector3.Up)) > .9f ? Vector3.Right : Vector3.Up).Normalized();
			line.GlobalTransform = new Transform3D(new Basis(x, y * delta.Length(), x.Cross(y)), (a + b) * .5f);
		}
	}
	public override float AdjustDamage(HitInstance hit, float damage)
	{
		if (hit.Transferred || damage <= 0 || hit.Context.Depth >= EventContext.MaxDepth) return damage;
		var targets = Wave.LiveMembers().Where(u => !ReferenceEquals(u, Owner) && u.Faction == Owner.Faction
			&& u.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= Def.Radius * Def.Radius
			&& Wave.World.HasSight(Owner.GlobalPosition + Vector3.Up, u.GlobalPosition + Vector3.Up)
			&& u.CanReceiveDamageFrom(Transferred(hit, u, damage))).Take(Math.Max(1, Def.Count)).ToArray();
		if (targets.Length == 0) return damage;
		float share = damage / (targets.Length + 1);
		foreach (var target in targets)
		{
			if (!GodotObject.IsInstanceValid(target) || target.IsDead) continue;
			target.ReceiveHit(Transferred(hit, target, share)); Transfers++;
		}
		return share;
	}
	public override void Dispose()
	{
		foreach (var line in _links) if (GodotObject.IsInstanceValid(line)) line.QueueFree(); _links.Clear(); base.Dispose();
	}
	private static HitInstance Transferred(HitInstance original, Unit target, float damage) => new()
	{
		Target = target, Attacker = original.Attacker, Attack = original.Attack,
		Context = original.Context.Child(original.Attacker, target)!, Damage = damage,
		Direction = original.Direction, Position = target.GlobalPosition, BypassBlock = original.BypassBlock,
		Transferred = true,
	};
}
