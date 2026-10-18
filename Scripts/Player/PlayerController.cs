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
	private Combat.SprintController? _sprint;

	public override void _Ready()
	{
		base._Ready();
		_movement = GetNode<CharacterMovement>("Movement");
		_camera = GetNodeOrNull<CameraRig>("CameraRig");
		// ⚠️ 惰性查（**不要在 `_Ready` 里当成一定有**）：子节点的 `_Ready` 早于父节点，
		// 而且"奔跑"是 P6d 才加的可选组件 —— 没有它时移动速度与改动前逐位相同。
		_sprint = GetNodeOrNull<Combat.SprintController>("Sprint");
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

		// P6d：按住奔跑键且真的在移动时，意图速度 × 表里的倍率（没有 Sprint 组件 / 没在奔跑 → 原速）。
		_movement.SetIntent(direction, _sprint?.EffectiveSpeed(MoveSpeed) ?? MoveSpeed);

		if (Input.IsActionJustPressed("jump") && !_movement.IsActionMovementLocked)
		{
			_movement.TryJump();
		}
	}
}
