using Godot;

namespace ActRoguelike.Abilities;

/// <summary>飞行和落地伤害各自计时；落地才创建命中体，取消可当帧撤销。</summary>
public sealed class EnemyDiveExecution : EnemyHitboxExecution
{
	private Vector3 _velocity;
	private float _elapsed;
	private bool _landed, _complete;
	public override bool ControlsDuration => true;
	public override bool IsComplete => _complete;
	public override bool SuppressWarning => _landed;
	public override Vector3? WarningVelocity => _velocity;
	public EnemyDiveExecution(EnemyActionContext context) : base(context) { }
	public override void Begin()
	{
		if (IsDisposed || Context.Target() is not { } target || !GodotObject.IsInstanceValid(target)) return;
		Vector3 direction = target.GlobalPosition - Context.Owner.GlobalPosition; direction.Y = Mathf.Min(-1, direction.Y);
		_velocity = direction.Normalized() * Step.Speed;
		Context.Owner.Movement?.BeginVelocityOverride(this, _velocity);
	}
	public override void Tick(float delta)
	{
		if (IsDisposed || _complete) return;
		_elapsed += delta;
		if (_landed) { Sweep(); _complete = _elapsed >= Step.LandingSeconds; return; }
		if (Context.Owner.IsOnFloor() && _elapsed > .03f)
		{
			Context.Owner.Movement?.EndVelocityOverride(this); _landed = true; _elapsed = 0;
			base.Begin(); return;
		}
		_complete = _elapsed >= Step.ActiveSeconds;
	}
}
