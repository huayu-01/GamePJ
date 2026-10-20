using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.World;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Abilities;

/// <summary>AI自动推进通用动作段；伤害动作等待真实ATK动画事件，准备／远近分支／收尾独立组合。</summary>
public partial class EnemyPlanAbility : Ability
{
	protected override bool WarnOnActivate => false;
	[Export] public EnemyActionPlan Plan { get; set; } = null!;
	public int ExecutedAttackCount { get; private set; }
	public int StepIndex { get; private set; }
	public Kind CurrentKind => _step?.Kind ?? Kind.Wait;
	private readonly List<EnemyActionStep> _steps = new();
	private EnemyActionStep? _step;
	private int _phase;
	private float _elapsed;
	private Vector3 _direction, _startPosition;
	private AttackHitbox? _hitbox;
	private bool _contact, _branched, _blockingBefore, _stepEvent;
	protected override void OnAnimEvent(string name) { if (name == "ATK") _stepEvent = true; }
	private Node3D? _model;
	private Vector3 _modelPosition, _modelRotation;
	protected override void OnReady()
	{
		Cooldown = Plan.CooldownSeconds; ApproachMaxRange = Plan.TriggerDistance;
		AnimName = "EnemyAction"; AnimEvent = "ATK";
		_model = OwnerUnit!.GetNodeOrNull<Node3D>("Model");
		_warning = EnemyAttackWarning.Create(this, OwnerUnit);
		if (_model != null) { _modelPosition = _model.Position; _modelRotation = _model.Rotation; }
	}
	protected override bool CanActivate(Unit? target) => IsInstanceValid(target) && target is { IsDead: false } && OwnerUnit!.GlobalPosition.DistanceTo(target.GlobalPosition) <= Plan.TriggerDistance;
	protected override void OnActivate(Unit? target)
	{
		_steps.Clear(); _steps.AddRange(Plan.Opening); _branched = false; StepIndex = -1;
		_blockingBefore = OwnerUnit!.Block?.Blocking == true;
		Advance();
	}
	private bool IsAttack(Kind kind) => kind is Kind.Shot or Kind.ArcShot or Kind.Bomb or Kind.ArcBomb or Kind.Meteor or Kind.Melee or Kind.Spin or Kind.Charge or Kind.HomingCharge or Kind.Dive;
	private void Advance()
	{
		ClearStep(); StepIndex++;
		if (StepIndex >= _steps.Count && !_branched)
		{
			_branched = true;
			float distance = Target != null ? OwnerUnit!.GlobalPosition.DistanceTo(Target.GlobalPosition) : float.MaxValue;
			_steps.AddRange(distance <= Plan.NearDistance && Plan.Near.Length > 0 ? Plan.Near : Plan.Far);
			_steps.AddRange(Plan.Closing);
		}
		if (StepIndex >= _steps.Count || !IsInstanceValid(Target) || Target is not { IsDead: false }) { Finish(); return; }
		_step = _steps[StepIndex]; _elapsed = 0; _phase = 0; _contact = false; _stepEvent = false;
		_startPosition = OwnerUnit!.GlobalPosition;
		if (OwnerUnit.Block is { } block) block.Blocking = _step.Kind == Kind.Guard || _step.Blocking;
		FaceTarget(1);
		if (IsAttack(_step.Kind))
		{
			OwnerUnit.GetNodeOrNull<UnitPresentation>("Presentation")?.FlashAttack();
			if (PlaySkillAnimationNamed("EnemyAction", 0.55f / Mathf.Max(0.06f, _step.WindupSeconds)).Length == 0) { Cancel(); return; }
		}
		else { _phase = 1; BeginExecution(); }
		UpdateWarning(1);
	}
	private void FaceTarget(float dt)
	{
		if (!IsInstanceValid(Target) || Target == null) return;
		Vector3 offset = Target.GlobalPosition - OwnerUnit!.GlobalPosition; offset.Y = 0;
		_direction = offset.LengthSquared() > 0.001f ? offset.Normalized() : -OwnerUnit.GlobalBasis.Z;
		Movement?.FaceDirection(_direction, dt);
	}
	protected override void Tick(float delta)
	{
		if (!IsBusy || _step == null) return;
		if (!IsInstanceValid(Target) || Target is not { IsDead: false }) { Cancel(); return; }
		_elapsed += delta;
		UpdateWarning(delta);
		if (_phase == 0)
		{
			// 蓄势一直看向玩家，真正执行时才锁定直线动作方向。
			FaceTarget(delta);
			if (_stepEvent) { _phase = 1; _elapsed = 0; ExecutedAttackCount++; BeginExecution(); }
			else if (_elapsed > Mathf.Max(2, _step.WindupSeconds * 3)) Cancel();
			return;
		}
		if (_phase == 2) { if (_elapsed >= _step.RecoverSeconds) Advance(); return; }
		bool done = _elapsed >= _step.ActiveSeconds;
		switch (_step.Kind)
		{
			case Kind.Guard: FaceTarget(delta); break;
			case Kind.Charge:
			case Kind.HomingCharge:
				if (_step.Kind == Kind.HomingCharge)
				{
					Vector3 desired = Target.GlobalPosition - OwnerUnit!.GlobalPosition; desired.Y = 0;
					if (desired.LengthSquared() > 0.001f) _direction = _direction.Slerp(desired.Normalized(), Mathf.Min(1, Mathf.DegToRad(_step.TurnDegreesPerSecond) * delta / Mathf.Max(0.001f, _direction.AngleTo(desired.Normalized())))).Normalized();
				}
				Movement!.FaceDirection(_direction, delta); Movement.BeginVelocityOverride(this, _direction * _step.Speed, -1, true);
				done |= _contact || OwnerUnit!.GlobalPosition.DistanceTo(_startPosition) >= _step.TravelDistance
					|| (_step.Kind == Kind.HomingCharge && OwnerUnit.GlobalPosition.DistanceTo(Target.GlobalPosition) <= _step.Radius);
				break;
			case Kind.Melee:
			case Kind.Spin:
				if (_step.Speed > 0) Movement!.BeginVelocityOverride(this, _direction * _step.Speed, -1, true);
				if (_step.Kind == Kind.Spin && _model != null) _model.Rotation = _modelRotation + Vector3.Up * Mathf.Tau * Mathf.Clamp(_elapsed / Mathf.Max(0.01f, _step.ActiveSeconds), 0, 1);
				break;
			case Kind.Hover: Movement!.BeginVelocityOverride(this, Vector3.Zero); break;
			case Kind.Dive:
				if (OwnerUnit!.IsOnFloor() && _elapsed > 0.03f)
				{
					Movement!.EndVelocityOverride(this); OpenHitbox(_step.Radius); _step = _step.Duplicate() as EnemyActionStep ?? _step;
					_step.Kind = Kind.Melee; _step.Speed = 0; _step.ActiveSeconds = 0.12f; _elapsed = 0; done = false;
				}
				break;
			case Kind.Burrow:
				if (_model != null) _model.Position = _modelPosition + Vector3.Down * Mathf.Sin(Mathf.Pi * Mathf.Clamp(_elapsed / Mathf.Max(0.01f, _step.ActiveSeconds), 0, 1)) * 1.5f;
				if (done && MapManager.Instance?.TryRingPoint(Target.GlobalPosition, 4, 8, out var exit, OwnerUnit) == true)
				{ OwnerUnit!.GlobalPosition = exit; OwnerUnit.ResetPhysicsInterpolation(); OwnerUnit.Velocity = Vector3.Zero; }
				break;
		}
		if (_hitbox != null && IsInstanceValid(_hitbox)) _hitbox.SweepSphere(_hitbox.GlobalPosition, _hitbox.GlobalPosition);
		if (done) { ClearStep(); _phase = 2; _elapsed = 0; }
	}
	private AttackInstance Attack(float damage, bool bomb = false)
	{
		float scale = OwnerUnit!.Stats.Get("DamageMultiplier");
		return new AttackInstance { Attacker = OwnerUnit, Ability = AbilityId, AbilityKind = AbilityId,
			BaseDamage = bomb ? 0 : damage * scale, ExplosionDamage = damage * scale, ExplosionRadius = bomb ? _step!.Radius : 0,
			Knockback = 3, Lifetime = 12, Tags = bomb ? new[] { "Projectile", "Explosion" } : new[] { "EnemyAttack" } };
	}
	private void OpenHitbox(float radius)
	{
		if (_hitbox != null) return;
		_hitbox = new AttackHitbox { Position = HitboxOffset() };
		_hitbox.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Mathf.Max(0.2f, radius) } });
		OwnerUnit!.AddChild(_hitbox); _hitbox.HitLanded += _ => _contact = true;
		_hitbox.Activate(OwnerUnit, Attack(_step!.Damage)); _hitbox.SweepSphere(_hitbox.GlobalPosition, _hitbox.GlobalPosition);
	}
	private void BeginExecution()
	{
		if (_step == null) return;
		if (_step.Kind is Kind.Melee or Kind.Meteor or Kind.Bomb or Kind.ArcBomb) _warning?.HideAll();
		switch (_step.Kind)
		{
			case Kind.Shot: case Kind.ArcShot: case Kind.Bomb: case Kind.ArcBomb: case Kind.Meteor: Fire(); break;
			case Kind.Melee: case Kind.Spin: case Kind.Charge: case Kind.HomingCharge: OpenHitbox(_step.Radius); break;
			case Kind.Jump: Movement!.BeginJump(_step.Speed); break;
			case Kind.Hover: Movement!.BeginVelocityOverride(this, Vector3.Zero); break;
			case Kind.BackHop:
				Vector3 desired = OwnerUnit!.GlobalPosition - _direction * _step.Speed * _step.ActiveSeconds;
				if (MapManager.Instance?.TryValidPoint(desired, OwnerUnit.GlobalPosition, out _, OwnerUnit) != true) { _phase = 2; _elapsed = 0; break; }
				Movement!.BeginJump(5); Movement.BeginVelocityOverride(this, -_direction * _step.Speed + Vector3.Up * 3); break;
			case Kind.Dive:
				Vector3 dive = Target!.GlobalPosition - OwnerUnit!.GlobalPosition; dive.Y = Mathf.Min(-1, dive.Y);
				_warningDiveVelocity = dive.Normalized() * _step.Speed;
				Movement!.BeginVelocityOverride(this, _warningDiveVelocity); break;
		}
	}
	private void Fire()
	{
		if (_step == null || Target == null) return;
		bool bomb = _step.Kind is Kind.Bomb or Kind.ArcBomb or Kind.Meteor;
		var projectile = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/Bullet.tscn").Instantiate<Projectile>();
		projectile.Pierce = 0; projectile.GravityScale = 0; projectile.MaxTravelDistance = _step.TravelDistance;
		projectile.DistanceEnd = bomb ? Projectile.DistanceEndMode.Explode : Projectile.DistanceEndMode.Disappear;
		projectile.ExplosionOnly = bomb;
		projectile.QueryGeometry = 1;
		projectile.SweepRadius = _step.Kind == Kind.Meteor ? 0.7f : bomb ? 0.2f : 0.1f;
		projectile.AddChild(new MeshInstance3D { Mesh = new SphereMesh
		{
			Radius = projectile.SweepRadius, Height = projectile.SweepRadius * 2,
			Material = new StandardMaterial3D { AlbedoColor = bomb ? new Color(1, 0.35f, 0.1f) : new Color(0.8f, 0.3f, 1), EmissionEnabled = true, Emission = new Color(0.7f, 0.2f, 0.1f) },
		} });
		ProjectileMotion(out Vector3 origin, out Vector3 velocity, out float gravityScale);
		projectile.GravityScale = gravityScale;
		projectile.ShowImpactWarning = bomb;
		if (_step.Kind == Kind.Shot)
		{ projectile.SeekAnyHostile = true; projectile.AssignedHomingTarget = Target; projectile.HomingTurnRateDegrees = _step.TurnDegreesPerSecond; }
		projectile.Speed = velocity.Length();
		(GetTree().CurrentScene ?? GetTree().Root).AddChild(projectile); projectile.GlobalPosition = origin;
		projectile.Launch(Attack(_step.Damage, bomb), velocity.Normalized());
	}
	private void ClearStep()
	{
		_warning?.HideAll();
		Movement?.EndVelocityOverride(this); Movement?.EndDash(); Movement?.Stop();
		if (IsInstanceValid(_hitbox)) { _hitbox!.Deactivate(); _hitbox.QueueFree(); } _hitbox = null;
		if (IsInstanceValid(_model)) { _model!.Position = _modelPosition; _model.Rotation = _modelRotation; }
	}
	protected override void OnFinishOrCancel()
	{
		ClearStep(); if (OwnerUnit?.Block is { } block) block.Blocking = _blockingBefore;
		_step = null;
	}
}
