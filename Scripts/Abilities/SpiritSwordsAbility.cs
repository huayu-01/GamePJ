using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>本人主攻击命中后，三柄灵剑按中、左、右顺序高速突刺同一目标，完全归位后才能再次追击。</summary>
public partial class SpiritSwordsAbility : Ability
{
	[Export] public PackedScene SwordScene { get; set; } = null!;
	[Export] public float DurationSeconds { get; set; } = 10f;
	[Export] public float ReturnSeconds { get; set; } = 1.1f;
	[Export] public float SwordDamageScale { get; set; } = .3f;
	[Export] public float LaunchIntervalSeconds { get; set; } = .12f;
	[Export] public float WindupSeconds { get; set; } = .065f;
	[Export] public float ThrustSpeed { get; set; } = 85f;
	[Export] public float FlightTimeoutSeconds { get; set; } = .65f;
	[Export] public float MaxChaseDistance { get; set; } = 18f;
	public float RemainingSeconds { get; private set; }
	public override float EffectRemainingSeconds => RemainingSeconds;
	public override bool IsReady => base.IsReady && !Summoned;
	public float KillExtensionSeconds { get; private set; }
	public bool Summoned => RemainingSeconds > 0f;
	public bool SwordsReady => Summoned && !BarrageActive;
	public bool BarrageActive { get; private set; }
	public int SwordCount => _swords.Count;
	public int VolleyCount { get; private set; }
	public int LaunchCount { get; private set; }
	public int ImpactCount { get; private set; }
	public int ExtensionCount { get; private set; }
	public AttackInstance? LastSwordAttack { get; private set; }
	public Unit? LockedTarget => _target;
	public IReadOnlyList<AttackHitbox> Swords => _swords;
	public IReadOnlyList<int> LaunchOrder => _launchOrder;
	private enum Phase { Ready, Waiting, Windup, Flying, Impact, Returning }
	private sealed class SwordState
	{
		public AttackHitbox Body = null!;
		public SpiritSwordFx? Fx;
		public Phase Phase;
		public float Time, FlightTime;
		public Vector3 Direction, Aim;
		public bool AimLocked;
		public Transform3D ReturnFrom;
	}
	private readonly List<AttackHitbox> _swords = new();
	private readonly List<SwordState> _states = new();
	private readonly List<int> _launchOrder = new();
	private Unit? _target;
	private AttackInstance? _sourceAttack;
	private Vector3 _targetOffset;
	private bool _subscribed;

	public void ConfigureKillExtension(float seconds) => KillExtensionSeconds = Mathf.Max(0f, seconds);
	protected override bool CanActivate(Unit? target) => !Summoned && SwordScene != null && OwnerUnit != null;

	protected override void OnActivate(Unit? target)
	{
		RemainingSeconds = Mathf.Max(.01f, DurationSeconds);
		VolleyCount = LaunchCount = ImpactCount = ExtensionCount = 0;
		LastSwordAttack = null;
		_launchOrder.Clear();
		for (int i = 0; i < 3; i++)
		{
			var sword = SwordScene.Instantiate<AttackHitbox>();
			AddChild(sword); sword.TopLevel = true;
			sword.GlobalTransform = RestPose(i); sword.ResetPhysicsInterpolation();
			var state = new SwordState { Body = sword, Fx = sword.GetNodeOrNull<SpiritSwordFx>("MotionFx") };
			sword.HitLanded += hit => OnSwordHit(state, hit);
			_swords.Add(sword); _states.Add(state);
		}
		GameplayEventBus.Raised += OnGameplayEvent;
		_subscribed = true;
		// 召唤立刻开始冷却，持续效果独立于前台动作。
		Finish();
	}
	private Transform3D RestPose(int index) => new(OwnerUnit!.GlobalBasis.Orthonormalized() * new Basis(Vector3.Back, (1 - index) * .1f),
		OwnerUnit.GlobalPosition + OwnerUnit.GlobalBasis * new Vector3((index - 1) * .55f, 2.95f + (index == 1 ? .18f : 0f), .15f));
	private bool TargetValid => IsInstanceValid(_target) && _target is { IsDead: false } && OwnerUnit != null
		&& OwnerUnit.IsHostileTo(_target) && OwnerUnit.GlobalPosition.DistanceTo(_target.GlobalPosition) <= MaxChaseDistance;
	private Vector3 AimPoint => _target!.GlobalPosition + _targetOffset;

	private void BeginBarrage(Unit target, AttackInstance sourceAttack, Vector3 contact)
	{
		_target = target; _sourceAttack = sourceAttack;
		_targetOffset = contact - target.GlobalPosition;
		BarrageActive = true; VolleyCount++; _launchOrder.Clear();
		for (int i = 0; i < _states.Count; i++)
		{
			SwordState state = _states[i];
			state.Phase = Phase.Waiting;
			int order = i == 1 ? 0 : i == 0 ? 1 : 2;
			state.Time = -order * Mathf.Max(.06f, LaunchIntervalSeconds);
			state.FlightTime = 0; state.AimLocked = false;
			state.Body.RequiredTarget = target;
		}
		LastSwordAttack = CreateAttack(0);
	}
	private AttackInstance CreateAttack(int order)
	{
		AttackInstance source = _sourceAttack!;
		return new AttackInstance
		{
			Attacker = OwnerUnit, Ability = AbilityId, AbilityKind = AbilityId,
			BaseDamage = source.BaseDamage * SwordDamageScale,
			CanCrit = source.CanCrit, CritChance = source.CritChance, CritMultiplier = source.CritMultiplier,
			ComboStep = source.ComboStep, ComboStepCount = source.ComboStepCount,
			Knockback = source.Knockback, FeedbackWeight = order == 2 ? 1.8f : 1.45f,
			Policy = HitPolicy.OncePerTarget, Tags = new[] { "Projectile", "Physical", "SpiritSword" },
		};
	}
	private static Basis PointAlong(Vector3 direction) => new(new Quaternion(Vector3.Up, direction));
	private void Launch(SwordState state, int index)
	{
		state.Phase = Phase.Flying; state.Time = 0;
		state.Direction = (AimPoint - state.Body.GlobalPosition).Normalized();
		state.Body.GlobalBasis = PointAlong(state.Direction);
		LastSwordAttack = CreateAttack(_launchOrder.Count);
		state.Body.Activate(OwnerUnit, LastSwordAttack);
		state.Fx?.Launch();
		_launchOrder.Add(index); LaunchCount++;
	}
	private void BeginReturn(SwordState state)
	{
		state.Body.Deactivate(); state.Body.RequiredTarget = null;
		state.Fx?.Return();
		state.Phase = Phase.Returning; state.Time = 0;
		state.ReturnFrom = state.Body.GlobalTransform;
	}
	private void OnSwordHit(SwordState state, Unit target)
	{
		if (state.Phase != Phase.Flying || target != _target) return;
		state.Body.Deactivate();
		state.Phase = Phase.Impact; state.Time = 0;
		state.Fx?.Impact(); ImpactCount++;
	}
	private void Fly(SwordState state, float delta)
	{
		AttackHitbox sword = state.Body;
		state.FlightTime += delta;
		if (!state.AimLocked)
		{
			state.Aim = AimPoint;
			// 接近目标后锁定最后一段直刺，不减速盘旋。
			state.AimLocked = sword.GlobalPosition.DistanceTo(state.Aim) <= 1.6f;
		}
		Vector3 offset = state.Aim - sword.GlobalPosition;
		if (offset.LengthSquared() > .0001f) state.Direction = offset.Normalized();
		sword.GlobalBasis = PointAlong(state.Direction);
		Vector3 from = sword.GlobalPosition;
		float speedCap = ConfigIndex.Settings.ProjectileMaxSpeed;
		float speed = speedCap > 0 ? Mathf.Min(ThrustSpeed, speedCap) : ThrustSpeed;
		Vector3 to = from + state.Direction * Mathf.Min(Mathf.Max(1f, speed) * delta, offset.Length() + .35f);
		// 与地形做真实形状运动查询；敌人仍由同一剑体的扫掠处理，避免穿墙追击。
		var shape = sword.GetNode<CollisionShape3D>("Shape");
		using var query = new PhysicsShapeQueryParameters3D
		{
			Shape = shape.Shape, Transform = shape.GlobalTransform, Motion = to - from,
			CollisionMask = CombatLayers.World, CollideWithBodies = true, CollideWithAreas = false, Margin = 0f,
		};
		float[] motion = GetWorld3D().DirectSpaceState.CastMotion(query);
		bool blocked = motion.Length > 0 && motion[0] < 1f;
		if (blocked) to = from.Lerp(to, motion[0]);
		sword.GlobalPosition = to;
		sword.SweepBox(from, to);
		state.Fx?.Sample(to);
		if (state.Phase != Phase.Flying) return;
		if (blocked || state.FlightTime >= Mathf.Max(.05f, FlightTimeoutSeconds) || offset.LengthSquared() < .01f) BeginReturn(state);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (!Summoned) return;
		if (!Enabled || OwnerUnit is not { IsDead: false } || !OwnerUnit.AbilitySlots.Owns(this)) { ClearSwords(); return; }
		float dt = (float)delta;
		RemainingSeconds = Mathf.Max(0f, RemainingSeconds - dt);
		if (RemainingSeconds <= 0f) { ClearSwords(); return; }
		// 脱手后的剑使用独立时钟，不因玩家下一次近战命中停顿而悬在半空。
		for (int i = 0; i < _states.Count; i++)
		{
			SwordState state = _states[i]; state.Time += dt;
			if (!TargetValid && state.Phase is Phase.Waiting or Phase.Windup or Phase.Flying) BeginReturn(state);
			switch (state.Phase)
			{
				case Phase.Ready:
					state.Body.GlobalTransform = RestPose(i);
					break;
				case Phase.Waiting:
					state.Body.GlobalTransform = RestPose(i);
					if (state.Time >= 0f) { state.Phase = Phase.Windup; state.Time = 0; state.ReturnFrom = RestPose(i); }
					break;
				case Phase.Windup:
					Vector3 direction = (AimPoint - state.ReturnFrom.Origin).Normalized();
					float windup = Mathf.Clamp(state.Time / Mathf.Max(.01f, WindupSeconds), 0, 1);
					state.Body.GlobalTransform = new Transform3D(PointAlong(direction), state.ReturnFrom.Origin - direction * .3f * windup);
					state.Fx?.Windup(windup);
					if (windup >= 1) Launch(state, i);
					break;
				case Phase.Flying:
					Fly(state, dt);
					break;
				case Phase.Impact:
					state.Body.GlobalPosition += state.Direction * (12f * dt);
					state.Fx?.Sample(state.Body.GlobalPosition);
					if (state.Time >= .045f) BeginReturn(state);
					break;
				case Phase.Returning:
					float t = Mathf.Clamp(state.Time / Mathf.Max(.01f, ReturnSeconds), 0, 1);
					state.Body.GlobalTransform = state.ReturnFrom.InterpolateWith(RestPose(i), t * t * (3f - 2f * t));
					if (t >= 1) state.Phase = Phase.Ready;
					break;
			}
		}
		if (BarrageActive && _states.TrueForAll(state => state.Phase == Phase.Ready))
		{
			BarrageActive = false; _target = null; _sourceAttack = null;
		}
	}
	private void OnGameplayEvent(GameplayEvent evt)
	{
		if (!Summoned || !Enabled || OwnerUnit is not { IsDead: false } || evt.Source != OwnerUnit) return;
		if (evt.Kind == GameplayEventKind.Kill)
		{
			if (evt.Target is { IsDead: true } victim && OwnerUnit.IsHostileTo(victim) && KillExtensionSeconds > 0)
			{ RemainingSeconds += KillExtensionSeconds; ExtensionCount++; }
			return;
		}
		// 必须等本次防御／伤害结算完成；Health可能已归零而Die尚未发布，不能先锁定尸体。
		if (evt.Kind != GameplayEventKind.Damaged || !SwordsReady || !OwnerUnit.AbilitySlots.Owns(this)
			|| evt.Target is not { IsDead: false, Health: > 0 } target || !OwnerUnit.IsHostileTo(target)
			|| evt.Attack is not { IsDamageOverTime: false, IsSecondary: false } attack || attack.Ability == AbilityId
			|| OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition) > MaxChaseDistance) return;
		BeginBarrage(target, attack, evt.Position);
	}
	private void ClearSwords()
	{
		if (_subscribed) GameplayEventBus.Raised -= OnGameplayEvent;
		_subscribed = false;
		foreach (AttackHitbox sword in _swords) { sword.Deactivate(); sword.Visible = false; sword.QueueFree(); }
		_swords.Clear(); _states.Clear();
		_target = null; _sourceAttack = null;
		RemainingSeconds = 0; BarrageActive = false;
	}
	protected override void OnLoadoutReset() { ClearSwords(); KillExtensionSeconds = 0f; }
	public override void _ExitTree() { ClearSwords(); base._ExitTree(); }
}
