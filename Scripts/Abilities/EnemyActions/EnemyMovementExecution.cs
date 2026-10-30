using System;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Abilities;

public sealed class EnemyJumpExecution : EnemyActionExecution
{
	public EnemyJumpExecution(EnemyActionContext context) : base(context) { }
	public override void Begin() { if (!IsDisposed) Context.Owner.Movement?.BeginJump(Context.Step?.Speed ?? 8); }
}

public sealed class EnemyBackHopExecution : EnemyActionExecution
{
	private readonly EnemyActionState _step;
	private bool _complete;
	public override bool IsComplete => _complete;
	public EnemyBackHopExecution(EnemyActionContext context) : base(context) => _step = context.Step ?? throw new ArgumentException("后跳动作需要本次参数快照。");
	public override void Begin()
	{
		if (IsDisposed) return;
		Vector3 desired = Context.Owner.GlobalPosition - Context.Heading * _step.Speed * _step.ActiveSeconds;
		if (MapManager.Instance?.TryValidPoint(desired, Context.Owner.GlobalPosition, out _, Context.Owner) != true) { _complete = true; return; }
		if (Context.Owner.Movement?.BeginVelocityOverride(this, -Context.Heading * _step.Speed + Vector3.Up * 3) != true) { _complete = true; return; }
		Context.Owner.Movement.BeginJump(5);
	}
	protected override void End() { if (GodotObject.IsInstanceValid(Context.Owner.Movement)) Context.Owner.Movement!.EndVelocityOverride(this); }
}

/// <summary>仅移动外层表现根，真实出土位置继续由导航网格、连通性与身体净空决定。</summary>
public sealed class EnemyBurrowExecution : EnemyActionExecution
{
	private readonly EnemyActionState _step;
	private readonly Node3D? _visual;
	private readonly Vector3 _position;
	private float _elapsed;
	private bool _finished;
	private bool _relocated;
	public EnemyBurrowExecution(EnemyActionContext context) : base(context)
	{
		_step = context.Step ?? throw new ArgumentException("遁地动作需要本次参数快照。");
		_visual = context.Owner.GetNodeOrNull<Node3D>("Model"); _position = _visual?.Position ?? Vector3.Zero;
	}
	public override void Tick(float delta)
	{
		if (IsDisposed || _finished) return;
		_elapsed += delta;
		if (GodotObject.IsInstanceValid(_visual)) _visual!.Position = _position + Vector3.Down * Mathf.Sin(Mathf.Pi * Mathf.Clamp(_elapsed / Mathf.Max(.01f, _step.ActiveSeconds), 0, 1)) * _step.BurrowDepth;
		// 模型完全下沉时换位，再从新位置浮起；不能等回到地面才瞬移。
		if (!_relocated && _elapsed >= Mathf.Max(.01f, _step.ActiveSeconds) * .5f)
		{
			_relocated = true;
			if (GodotObject.IsInstanceValid(_visual)) _visual!.Position = _position + Vector3.Down * _step.BurrowDepth;
			if (Context.Target() is { } target && GodotObject.IsInstanceValid(target)
				&& MapManager.Instance?.TryRingPoint(target.GlobalPosition, _step.BurrowMinDistance, _step.BurrowMaxDistance, out var exit, Context.Owner) == true)
			{ Context.Owner.GlobalPosition = exit; Context.Owner.ResetPhysicsInterpolation(); Context.Owner.Velocity = Vector3.Zero; }
		}
		if (_elapsed >= _step.ActiveSeconds) _finished = true;
	}
	protected override void End() { if (GodotObject.IsInstanceValid(_visual)) _visual!.Position = _position; }
}
