using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Abilities;

public partial class EnemyPlanAbility
{
	private EnemyAttackWarning? _warning;
	private float _warningRefresh;
	private Vector3 _warningDiveVelocity;
	private Vector3 HitboxOffset() => Vector3.Up * .8f + (_step!.Kind == Kind.Spin || _step.Centered ? Vector3.Zero : Vector3.Forward * .7f);
	/// <summary>发射与前摇预览共读同一初速，不能各算一套弹道。</summary>
	private void ProjectileMotion(out Vector3 origin, out Vector3 velocity, out float gravityScale)
	{
		var motion = EnemyProjectileMotion.Resolve(_step!, OwnerUnit!, Target!);
		origin = motion.Origin; velocity = motion.Velocity; gravityScale = motion.GravityScale;
	}
	private void UpdateWarning(float delta)
	{
		if (_warning == null || _step == null || _phase == 2) return;
		if (_execution?.SuppressWarning == true) { _warning.HideAll(); return; }
		_warningRefresh -= delta;
		_warning.SetProgress(_phase==0 ? _elapsed/Mathf.Max(.001f,_step.WindupSeconds) : 1);
		Vector3 center = OwnerUnit!.GlobalTransform * HitboxOffset();
		Vector3 scale = OwnerUnit.GlobalBasis.Scale.Abs();
		float radius = Mathf.Max(.2f, _step.Radius) * Mathf.Max(scale.X, scale.Z);
		if (_step.Kind is Kind.Charge or Kind.HomingCharge or Kind.Spin)
		{
			float timeLeft = _phase == 0 ? _step.ActiveSeconds : Mathf.Max(0, _step.ActiveSeconds - _elapsed);
			float distance = _step.Speed * timeLeft;
			if (_step.Kind != Kind.Spin) distance = Mathf.Min(distance, Mathf.Max(0, _step.TravelDistance - OwnerUnit.GlobalPosition.DistanceTo(_startPosition)));
			_warning.SweepCircle(center, _direction, distance, radius);
		}
		else if (_step.Kind == Kind.Melee && _step.Centered && _phase == 0) _warning.Circle(center, radius);
		else if (_step.Kind == Kind.Meteor && _phase == 0) _warning.Circle(Target!.GlobalPosition, _step.Radius);
		else if (_step.Kind is Kind.Bomb or Kind.ArcBomb && _phase == 0 && _warningRefresh <= 0)
		{
			ProjectileMotion(out Vector3 origin, out Vector3 velocity, out float gravityScale);
			// 预览用实际限速；发射仍保留原始Speed，由Projectile统一约束实际速度。
			float limit = ConfigIndex.Settings.ProjectileMaxSpeed;
			if (limit > 0 && velocity.Length() > limit) velocity = velocity.Normalized() * limit;
			float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * gravityScale;
			Vector3 impact = EnemyAttackWarning.PredictImpact(OwnerUnit, origin, velocity, gravity, _step.TravelDistance, 12, CombatLayers.World | CombatLayers.Player);
			_warning.Circle(impact, _step.Radius); _warningRefresh = .1f;
		}
		else if (_step.Kind is Kind.Jump or Kind.Hover)
		{
			foreach (var next in Plan.Far)
				if (next.Kind == Kind.Dive)
				{
					var landing = EnemyActionState.From(next, OwnerUnit.GlobalPosition);
					Vector3 offset = landing.Centered ? Vector3.Zero : -OwnerUnit.GlobalBasis.Z * .7f;
					_warning.Circle(Target!.GlobalPosition + offset, landing.Radius * Mathf.Max(scale.X, scale.Z)); break;
				}
		}
		else if (_step.Kind == Kind.Dive)
		{
			Vector3 landing = Target!.GlobalPosition;
			if (_phase == 1 && _warningRefresh <= 0)
			{
				landing = EnemyAttackWarning.PredictImpact(OwnerUnit, OwnerUnit.GlobalPosition + Vector3.Up * .02f, _execution?.WarningVelocity ?? _warningDiveVelocity, 0, _step.Speed * _step.ActiveSeconds, _step.ActiveSeconds, CombatLayers.World);
				_warningRefresh = .1f;
			}
			else if (_phase == 1) return;
			_warning.Circle(landing + OwnerUnit.GlobalBasis * HitboxOffset(), radius);
		}
		else if (_phase == 1) _warning.HideAll();
	}
}
