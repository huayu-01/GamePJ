using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>周期行为只管理自身节奏；产生的危险由世界统一限额、暂停和清理。</summary>
public abstract class PeriodicEliteHazardBehavior : EliteTraitBehavior
{
	private float _cooldown;
	protected PeriodicEliteHazardBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public int TriggerCount { get; private set; }
	public override void Tick(float dt)
	{
		UpdateExisting(dt); _cooldown -= dt;
		if (_cooldown > 0 || Wave.IsStopped || !Wave.IsSealed || Wave.Player is not { IsDead: false }) return;
		_cooldown = Mathf.Max(1, Def.Cooldown); TriggerCount++; Trigger();
	}
	protected virtual void UpdateExisting(float dt) { }
	protected abstract void Trigger();
	protected bool PlayerNear(float scale = 3) => Wave.Player is { IsDead: false } player && player.GlobalPosition.DistanceSquaredTo(Owner.GlobalPosition) <= Def.Radius * Def.Radius * scale * scale;
	protected Vector3 Ground(Vector3 point)
		=> Wave.World.GroundPoint(point);
	protected static StatusEffect RootStatus(float seconds) => new()
	{
		Id = "EliteVines", Duration = seconds, IsControl = true, PreventsActions = false,
		Modifiers = new[] { new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 0 } },
	};
	protected static StatusEffect FreezeStatus(float seconds) => new()
	{
		Id = "EliteFrozen", Duration = seconds, IsControl = true, PreventsActions = true,
		Modifiers = new[] { new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 0 } },
	};
}

public sealed class LinkerTraitBehavior : PeriodicEliteHazardBehavior
{
	private readonly List<(Unit Target, EliteHazardHandle Hazard)> _links = new();
	public LinkerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		foreach (var member in Wave.LiveMembers())
		{
			if (member == Owner || _links.Count >= Mathf.Clamp(Def.Count, 1, 3) || member.GlobalPosition.DistanceTo(Owner.GlobalPosition) > Def.Radius * 2 || Mathf.Abs(member.GlobalPosition.Y - Owner.GlobalPosition.Y) > 1.2f) continue;
			bool duplicate = false; foreach (var link in _links) if (link.Target == member && !link.Hazard.IsDisposed) duplicate = true;
			if (duplicate || !Wave.World.HasSight(Owner.GlobalPosition + Vector3.Up, member.GlobalPosition + Vector3.Up)) continue;
			var h = Wave.World.Line(Owner, Owner.GlobalPosition, member.GlobalPosition, .55f, Def.Damage, Def.WarningSeconds, Def.Duration);
			if (h != null) { h.Tint = new Color(1, .2f, .65f, .8f); _links.Add((member, h)); }
		}
	}
	protected override void UpdateExisting(float dt)
	{
		for (int i = _links.Count - 1; i >= 0; i--)
		{
			var link = _links[i];
			if (link.Hazard.IsDisposed || !GodotObject.IsInstanceValid(link.Target) || link.Target.IsDead || link.Target.IsQueuedForDeletion()) { link.Hazard.Dispose(); _links.RemoveAt(i); continue; }
			link.Hazard.Center = Owner.GlobalPosition; link.Hazard.End = link.Target.GlobalPosition;
			bool visible = Owner.GlobalPosition.DistanceTo(link.Target.GlobalPosition) <= Def.Radius * 2 && Mathf.Abs(Owner.GlobalPosition.Y - link.Target.GlobalPosition.Y) <= 1.2f && Wave.World.HasSight(Owner.GlobalPosition + Vector3.Up, link.Target.GlobalPosition + Vector3.Up);
			// 断线即结束，恢复连接必须重新生成并经过完整预警。
			if (!visible) { link.Hazard.Dispose(); _links.RemoveAt(i); }
		}
	}
	public override void Dispose() { foreach (var link in _links) link.Hazard.Dispose(); _links.Clear(); }
}

public sealed class BurnerTraitBehavior : PeriodicEliteHazardBehavior
{
	public BurnerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (!PlayerNear()) return;
		Vector3 center = Ground(Wave.Player!.GlobalPosition);
		var h = Wave.World.Circle(Owner, center, Def.Radius, Def.Damage, Def.WarningSeconds, Def.Duration, true);
		if (h != null) h.Tint = new Color(1, .27f, .05f, .8f);
	}
}

public sealed class BurrowerTraitBehavior : PeriodicEliteHazardBehavior
{
	public BurrowerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (!PlayerNear()) return;
		var h = Wave.World.Circle(Owner, Ground(Wave.Player!.GlobalPosition), Def.Radius, Def.Damage, Def.WarningSeconds, Def.Duration, true, RootStatus(1.1f));
		if (h != null) h.Tint = new Color(.32f, .8f, .25f, .8f);
	}
}

public sealed class SlicerTraitBehavior : PeriodicEliteHazardBehavior
{
	private EliteHazardHandle? _beam;
	private float _angle;
	public SlicerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (_beam is { IsDisposed: false }) return;
		_angle = 0;
		_beam = Wave.World.Line(Owner, Owner.GlobalPosition, Owner.GlobalPosition + Vector3.Right * Def.Radius, .6f, Def.Damage, Def.WarningSeconds, Def.Duration);
		if (_beam != null) _beam.Tint = new Color(1, .2f, .42f, .85f);
	}
	protected override void UpdateExisting(float dt)
	{
		if (_beam is not { IsDisposed: false }) return;
		if (_beam.IsActive) _angle += dt * Mathf.DegToRad(Mathf.Clamp(Def.Magnitude, 15, 70));
		_beam.Center = Owner.GlobalPosition;
		_beam.End = Owner.GlobalPosition + new Vector3(Mathf.Cos(_angle), 0, Mathf.Sin(_angle)) * Def.Radius;
	}
	public override void Dispose() => _beam?.Dispose();
}

public sealed class SpinnerTraitBehavior : PeriodicEliteHazardBehavior
{
	private readonly List<EliteHazardHandle> _orbs = new();
	private float _angle;
	public SpinnerTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		_orbs.RemoveAll(h => h.IsDisposed); if (_orbs.Count > 0) return;
		for (int i = 0; i < 3; i++)
		{
			float angle = _angle + i * Mathf.Tau / 3;
			var h = Wave.World.Circle(Owner, Owner.GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Def.Radius, .48f, Def.Damage, Def.WarningSeconds, Def.Duration);
			if (h != null) { h.Orb = true; h.Tint = new Color(1, .8f, .12f, .9f); _orbs.Add(h); }
		}
	}
	protected override void UpdateExisting(float dt)
	{
		_angle += dt * .75f;
		for (int i = 0; i < _orbs.Count; i++) if (!_orbs[i].IsDisposed) _orbs[i].Center = Owner.GlobalPosition + new Vector3(Mathf.Cos(_angle + i * Mathf.Tau / 3), 0, Mathf.Sin(_angle + i * Mathf.Tau / 3)) * Def.Radius;
	}
	public override void Dispose() { foreach (var orb in _orbs) orb.Dispose(); _orbs.Clear(); }
}

public sealed class ArcaneTraitBehavior : PeriodicEliteHazardBehavior
{
	private readonly List<(EliteHazardHandle Beam, float Angle)> _nodes = new();
	public ArcaneTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (!PlayerNear(4)) return;
		for (int i = 0; i < Mathf.Clamp(Def.Count, 1, 2); i++)
		{
			float a = i * Mathf.Pi + .7f;
			Vector3 center = Ground(Owner.GlobalPosition.Lerp(Wave.Player!.GlobalPosition, .55f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.7f);
			var h = Wave.World.Line(Owner, center, center + Vector3.Right * Def.Radius, .55f, Def.Damage, Def.WarningSeconds, Def.Duration);
			if (h != null) { h.Tint = new Color(.72f, .25f, 1, .85f); h.Angle = a; h.End = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Def.Radius; h.BeamLength = Def.Radius; h.AngularSpeed = .55f; _nodes.Add((h, a)); }
		}
	}
	protected override void UpdateExisting(float dt)
	{
		for (int i = _nodes.Count - 1; i >= 0; i--)
		{
			var node = _nodes[i]; if (node.Beam.IsDisposed) { _nodes.RemoveAt(i); continue; }
			// 角速度由危险世界驱动，施法者死亡后已启动节点仍能完成旋转。
		}
	}
	// 已启动的固定节点允许自然到期；导演清场统一撤销，仍占世界危险额度。
}

public sealed class FrozenTraitBehavior : PeriodicEliteHazardBehavior
{
	public FrozenTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (!PlayerNear(4)) return;
		int count = Mathf.Clamp(Def.Count, 1, 4); Vector3 point = Ground(Wave.Player!.GlobalPosition);
		for (int i = 0; i < count; i++)
		{
			float a = i * Mathf.Tau / count;
			var h = Wave.World.Circle(Owner, point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .9f, Def.Radius, Def.Damage, Def.WarningSeconds, .14f, false, FreezeStatus(Mathf.Clamp(Def.Magnitude, .35f, 1)));
			if (h != null) { h.Tint = new Color(.3f, .8f, 1, .85f); h.Orb = true; }
		}
	}
}

public sealed class MoltenTraitBehavior : EliteTraitBehavior
{
	private Vector3 _last;
	private float _timer;
	public MoltenTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Attach() => _last = Owner.GlobalPosition;
	public override void Tick(float dt)
	{
		_timer -= dt;
		if (_timer > 0 || !Wave.IsSealed || Owner.GlobalPosition.DistanceSquaredTo(_last) < .7f * .7f) return;
		_last = Owner.GlobalPosition; _timer = Mathf.Max(.5f, Def.Cooldown / 5);
		var h = Wave.World.Circle(Owner, _last, Mathf.Min(1, Def.Radius * .4f), Def.Damage * .45f, .35f, Def.Duration, true);
		if (h != null) h.Tint = new Color(1, .3f, .035f, .85f);
	}
	public override void OnDeath(HitInstance hit)
	{
		if (Wave.IsStopped) return;
		var h = Wave.World.Circle(Owner, Owner.GlobalPosition, Def.Radius, Def.Damage, Mathf.Max(1, Def.WarningSeconds), .14f);
		if (h != null) h.Tint = new Color(1, .35f, .03f, .9f);
	}
}

public sealed class TidalTraitBehavior : PeriodicEliteHazardBehavior
{
	public TidalTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		Vector3 center = Owner.GlobalPosition; float inner = Def.Radius * .43f;
		var near = Wave.World.Circle(Owner, center, inner, Def.Damage, Def.WarningSeconds, .2f);
		var far = Wave.World.Ring(Owner, center, inner + .65f, Def.Radius, Def.Damage, Def.WarningSeconds + 1.4f, .2f);
		if (near != null) near.Tint = new Color(.3f, .8f, 1, .85f);
		if (far != null) far.Tint = new Color(.3f, .8f, 1, .85f);
	}
}

public sealed class AftershockTraitBehavior : EliteTraitBehavior
{
	private readonly List<Ability> _abilities = new();
	private readonly List<Vector3> _path = new();
	private bool _recording;
	private int _executedBefore;
	private float _cooldown;
	public int TrailCount { get; private set; }
	public AftershockTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Attach() => NodeSearch.CollectInSubtree(Owner, _abilities);
	public override void Tick(float dt)
	{
		_cooldown = Mathf.Max(0, _cooldown - dt);
		bool busy = false; foreach (var ability in _abilities) if (GodotObject.IsInstanceValid(ability) && ability.IsBusy) { busy = true; break; }
		if (busy)
		{
			if (!_recording) { _path.Clear(); _path.Add(Owner.GlobalPosition); _recording = true; _executedBefore = ExecutedAttacks(); }
			else if (_path.Count < 40 && _path[^1].DistanceSquaredTo(Owner.GlobalPosition) > .35f * .35f) _path.Add(Owner.GlobalPosition);
		}
		else if (_recording)
		{
			_recording = false; if (_cooldown > 0 || !Wave.IsSealed || _executedBefore >= 0 && ExecutedAttacks() <= _executedBefore) return;
			_cooldown = Mathf.Max(1, Def.Cooldown); TrailCount++;
			if (_path.Count > 1) Wave.World.Polyline(Owner, _path, Mathf.Min(2, Def.Radius), Def.Damage, Def.WarningSeconds, .2f);
			else Wave.World.Circle(Owner, Owner.GlobalPosition - Owner.GlobalBasis.Z.Normalized() * .8f, Def.Radius, Def.Damage, Def.WarningSeconds, .2f);
		}
	}
	private int ExecutedAttacks()
	{
		int count = 0; bool tracked = false;
		foreach (var ability in _abilities)
		{
			if (ability is EnemyPlanAbility plan) { tracked = true; count += plan.ExecutedAttackCount; }
			else if (ability is MeleeAttackAbility melee) { tracked = true; count += melee.ExecutedAttackCount; }
		}
		return tracked ? count : -1;
	}
}

public sealed class TrailBrandTraitBehavior : EliteTraitBehavior
{
	private float _cooldown, _markRemaining, _sample;
	private Vector3 _last;
	public int MarkCount { get; private set; }
	public TrailBrandTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	public override void Tick(float dt)
	{
		if (!Wave.IsSealed || Wave.Player is not { IsDead: false } player) return;
		_cooldown -= dt;
		if (_cooldown <= 0) { _cooldown = Mathf.Max(Def.Duration + 1, Def.Cooldown); _markRemaining = Def.Duration; _sample = 0; _last = player.GlobalPosition + Vector3.Up * 20; }
		if (_markRemaining <= 0) return; _markRemaining -= dt; _sample -= dt;
		if (_sample > 0 || player.GlobalPosition.DistanceSquaredTo(_last) < .8f * .8f) return;
		_sample = .55f; _last = player.GlobalPosition;
		var h = Wave.World.Circle(Owner, _last, Mathf.Clamp(Def.Radius, .6f, 1.4f), Def.Damage, Def.WarningSeconds, .14f, true);
		if (h != null) { h.Tint = new Color(1, .38f, .16f, .85f); MarkCount++; }
	}
}

public sealed class ThornGridTraitBehavior : PeriodicEliteHazardBehavior
{
	public ThornGridTraitBehavior(EliteTraitDefinition def, Unit owner, EliteTraitWave wave) : base(def, owner, wave) { }
	protected override void Trigger()
	{
		if (!PlayerNear(4)) return;
		Vector3 center = Ground(Wave.Player!.GlobalPosition); int created = 0;
		var slow = new StatusEffect { Id = "EliteThorns", Duration = .8f, IsControl = true, Modifiers = new[] { new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = .6f } } };
		for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
		{
			if (((x + z) & 1) != 0 || created >= Mathf.Clamp(Def.Count, 1, 5)) continue;
			Vector3 point = Ground(center + new Vector3(x, 0, z) * 2.2f);
			var h = Wave.World.Circle(Owner, point, Mathf.Clamp(Def.Radius * .3f, .65f, 1), Def.Damage, Def.WarningSeconds, Def.Duration, true, slow);
			if (h != null) { h.Tint = new Color(.35f, .9f, .2f, .85f); created++; }
		}
	}
}
