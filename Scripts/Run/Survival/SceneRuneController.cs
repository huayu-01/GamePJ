using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Run;

/// <summary>场景单符文及其所有实例的生命周期；由导演推进，暂停/停止时不会自行继续结算。</summary>
public partial class SceneRuneController : Node3D
{
	public SceneRuneDefinition Definition { get; set; } = null!;
	public Unit Player { get; set; } = null!;
	public bool Running { get; private set; }
	public int SpawnedCount { get; private set; }
	public int ImpactCount { get; private set; }
	public float ElapsedSeconds { get; private set; }
	public int ActiveCount => _actors.Count;
	public IReadOnlyList<SceneRuneActor> Actors => _actors;
	private float _next;
	private Vector3 _anchor;
	private readonly List<SceneRuneActor> _actors = new();
	public override void _Ready() { _next = Definition.InitialDelay; _anchor = Player.GlobalPosition; Running = true; }
	public void Tick(float dt)
	{
		if (!Running || GetTree().Paused || !IsInstanceValid(Player) || Player.IsDead) return;
		ElapsedSeconds += dt;
		for (int index = _actors.Count - 1; index >= 0; index--)
		{
			var actor = _actors[index]; actor.Tick(dt);
			if (!Running) return;
			if (actor.Finished) { _actors.RemoveAt(index); actor.QueueFree(); }
		}
		_next -= dt;
		if (_next > 0) return;
		_next = Definition.Interval;
		TrySpawn();
	}
	public bool TrySpawn()
	{
		if (!Running || _actors.Count >= Definition.MaxActive || MapManager.Instance is not { IsReady: true } map) return false;
		Vector3 preferred = Player.GlobalPosition with { Y = _anchor.Y };
		if (!map.TryNavigationAnchor(preferred, out Vector3 anchor, Player)) return false;
		_anchor = anchor;
		if (!map.TryRingPoint(anchor, Definition.MinDistance, Definition.MaxDistance, out Vector3 point, Player, anchor)) return false;
		SceneRuneActor actor = Definition.Kind switch
		{
			SceneRuneDefinition.RuneKind.Missile => new SceneRuneMissile(),
			SceneRuneDefinition.RuneKind.StarRain => new SceneRuneMeteor(),
			_ => new SceneRuneField()
		};
		actor.Controller = this; AddChild(actor); actor.GlobalPosition = point;
		actor.Initialize(); _actors.Add(actor); SpawnedCount++; return true;
	}
	public void ApplyMissile(Vector3 direction)
	{
		if (!Running || !IsInstanceValid(Player) || Player.IsDead) return;
		ImpactCount++;
		switch (Definition.Impact)
		{
			case SceneRuneDefinition.MissileEffect.Heal: Player.Heal(Definition.Heal); break;
			case SceneRuneDefinition.MissileEffect.Shield:
				float amount = Player.MaxHealth * Definition.ShieldRatio;
				Player.GrantTemporaryShield(this, amount, amount, Definition.ShieldSeconds); break;
			case SceneRuneDefinition.MissileEffect.Knockback: Player.ReceiveKnockback(direction * Definition.Impulse); break;
			case SceneRuneDefinition.MissileEffect.Damage: Hit(Player, Definition.Damage); break;
		}
	}
	public void Hit(Unit target, float amount)
	{
		if (!Running || target.IsDead) return;
		var attack = new AttackInstance { Ability = "SceneRune", AbilityKind = Definition.Id, BaseDamage = amount, IsSecondary = true, BypassBlock = true };
		target.ReceiveHit(attack.RollHit(target, Vector3.Up, target.GlobalPosition));
	}
	public void Stop()
	{
		if (!Running) return;
		Running = false;
		foreach (var actor in _actors) { actor.Finish(); actor.QueueFree(); }
		_actors.Clear();
		if (IsInstanceValid(Player)) Player.RemoveTemporaryShield(this);
	}
	public override void _ExitTree() => Stop();
}
