using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>已出手攻击的跟发队列；按游戏物理帧计时，暂停冻结，空闲不更新。</summary>
public partial class ProjectileDispatch : Node
{
	private sealed class Pending
	{
		public required Projectile Projectile;
		public required AttackInstance Attack;
		public required Node Source;
		public Ability? Ability;
		public string AbilityId = "";
		public Vector3 OriginOffset, Direction;
		public int Frames;
		public ulong CreatedFrame;
	}
	private readonly List<Pending> _pending = new();
	private Unit _owner = null!;
	public int PendingCount => _pending.Count;
	public override void _Ready() { _owner = GetParent<Unit>(); _owner.Died += OnDied; SetPhysicsProcess(false); }
	public static void Submit(Node source, Unit? owner, Projectile projectile, AttackInstance attack, Vector3 origin, Vector3 direction, int delayFrames = 0)
	{
		if (delayFrames <= 0 || owner == null) { Launch(source, projectile, attack, origin, direction); return; }
		var queue = owner.GetNodeOrNull<ProjectileDispatch>("ProjectileDispatch");
		if (queue == null) { queue = new ProjectileDispatch { Name = "ProjectileDispatch" }; owner.AddChild(queue); }
		queue._pending.Add(new Pending { Projectile = projectile, Attack = attack, Source = source, Ability = source as Ability,
			AbilityId = (source as Ability)?.AbilityId ?? "", OriginOffset = origin - owner.GlobalPosition, Direction = direction,
			Frames = delayFrames, CreatedFrame = Engine.GetPhysicsFrames() });
		queue.SetPhysicsProcess(true);
	}
	private static void Launch(Node source, Projectile projectile, AttackInstance attack, Vector3 origin, Vector3 direction)
	{
		(source.GetTree().CurrentScene ?? source.GetTree().Root).AddChild(projectile);
		projectile.GlobalPosition = origin; projectile.Launch(attack, direction);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner.IsDead) { Clear(); return; }
		if (_owner.ImpactPause?.IsHolding == true) return;
		for (int i = _pending.Count - 1; i >= 0; i--)
		{
			var shot = _pending[i];
			bool stale = !IsInstanceValid(shot.Source) || !shot.Source.IsInsideTree();
			if (!stale && shot.Ability is { } ability)
				stale = !ability.Enabled || ability.AbilityId != shot.AbilityId || !_owner.AbilitySlots.Owns(ability);
			if (stale) { shot.Projectile.Free(); _pending.RemoveAt(i); continue; }
			if (shot.CreatedFrame == Engine.GetPhysicsFrames() || --shot.Frames > 0) continue;
			_pending.RemoveAt(i); Launch(shot.Source, shot.Projectile, shot.Attack, _owner.GlobalPosition + shot.OriginOffset, shot.Direction);
		}
		if (_pending.Count == 0) SetPhysicsProcess(false);
	}
	public static void CancelFor(Unit owner) => owner.GetNodeOrNull<ProjectileDispatch>("ProjectileDispatch")?.Clear();
	private void OnDied(HitInstance _) => Clear();
	private void Clear()
	{
		foreach (var shot in _pending) if (IsInstanceValid(shot.Projectile)) shot.Projectile.Free();
		_pending.Clear(); SetPhysicsProcess(false);
	}
	public override void _ExitTree() { if (IsInstanceValid(_owner)) _owner.Died -= OnDied; Clear(); }
}
