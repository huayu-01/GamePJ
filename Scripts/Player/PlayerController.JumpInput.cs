using Godot;

namespace ActRoguelike.Player;

public partial class PlayerController
{
	[Export(PropertyHint.Range, "0,0.3,0.01")] public float JumpInputBufferSeconds { get; set; } = .12f;
	private float _jumpInputLeft;
	private bool _jumpInputLatched;
	private int _jumpInputRevision;
	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsEcho() || IsDead || GetTree().Paused) return;
		if (inputEvent.IsActionPressed("jump")) QueueJumpInput();
		else if (inputEvent.IsActionPressed("move_left") || inputEvent.IsActionPressed("move_right")
			|| inputEvent.IsActionPressed("move_forward") || inputEvent.IsActionPressed("move_back")) CancelLocomotionActions();
	}
	private void QueueJumpInput()
	{
		_jumpInputLatched = true;
		if (IsDead || GetTree().Paused || HasActionLock || _movement.IsControlDisabled) return;
		CancelLocomotionActions();
		_jumpInputRevision = AbilitySlots.Revision;
		_jumpInputLeft = Mathf.Max(.001f, JumpInputBufferSeconds);
	}
	private void ClearJumpInput() { _jumpInputLeft = 0; _jumpInputLatched = false; }
	private void ProcessJumpInput(float dt)
	{
		if (!_jumpInputLatched && Input.IsActionJustPressed("jump")) QueueJumpInput();
		if (!Input.IsActionPressed("jump") && !Input.IsActionJustPressed("jump")) _jumpInputLatched = false;
		if (HasActionLock || _movement.IsControlDisabled || _jumpInputRevision != AbilitySlots.Revision) _jumpInputLeft = 0;
		if (_jumpInputLeft <= 0) return;
		// 输入略早于落地或短收招时保留一次；成功立即消费，持续按住不会自动连跳。
		if (!_movement.IsActionMovementLocked && _movement.TryJump()) _jumpInputLeft = 0;
		else _jumpInputLeft = Mathf.Max(0, _jumpInputLeft - dt);
	}
}
