using ActRoguelike.Camera;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Player;

/// <summary>
/// 玩家单位：只负责把输入翻译成移动意图，物理交给 CharacterMovement。
/// 武器技能（火力倾泻、战术滑铲）通过后续 Ability 接入，不写进这个脚本。
/// </summary>
public partial class PlayerController : Unit
{
	private CharacterMovement _movement = null!;
	private CameraRig? _camera;

	public override void _Ready()
	{
		base._Ready();
		_movement = GetNode<CharacterMovement>("Movement");
		_camera = GetNodeOrNull<CameraRig>("CameraRig");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (IsDead)
		{
			_movement.Stop();
			return;
		}

		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 forward = _camera?.FlatForward ?? -GlobalTransform.Basis.Z;
		Vector3 right = _camera?.FlatRight ?? GlobalTransform.Basis.X;
		Vector3 direction = right * input.X + forward * -input.Y;

		_movement.SetIntent(direction, MoveSpeed);

		if (Input.IsActionJustPressed("jump"))
		{
			_movement.TryJump();
		}
	}
}
