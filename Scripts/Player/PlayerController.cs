using ActRoguelike.Camera;
using ActRoguelike.Combat;
using ActRoguelike.UI;
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
	[Export] public float MovementAnchorRadius { get; set; } = 1.5f;
	[Export] public float MovementAnchorTurnSharpness { get; set; } = 14f;
	public bool HasMovementAnchor { get; private set; }
	public Vector3 MovementAnchorPosition => GlobalPosition + _anchorDirection * Mathf.Max(0.1f, MovementAnchorRadius);
	private Vector3 _anchorDirection;

	public override void _Ready()
	{
		base._Ready();
		if (!HasNode("AimAssist")) AddChild(new AimAssistController { Name = "AimAssist" });
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
			ClearJumpInput();
			HasMovementAnchor = false;
			_movement.Stop();
			return;
		}

		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 forward = _camera?.FlatForward ?? -GlobalTransform.Basis.Z;
		Vector3 right = _camera?.FlatRight ?? GlobalTransform.Basis.X;
		Vector3 direction = right * input.X + forward * -input.Y;
		direction = ResolveMovementDirection(direction, SettingsStore.PlayerMovementMode, (float)delta);
		if (direction.LengthSquared() > .0001f || _jumpInputLeft > 0) CancelLocomotionActions();

		// P6d：按住奔跑键且真的在移动时，意图速度 × 表里的倍率（没有 Sprint 组件 / 没在奔跑 → 原速）。
		_movement.SetIntent(direction, _sprint?.EffectiveSpeed(MoveSpeed) ?? MoveSpeed);

		ProcessJumpInput((float)delta);
	}
	private void CancelLocomotionActions()
	{
		foreach (var entry in AbilitySlots.Equipped)
			if (entry.Ability.IsActive && entry.Ability is Abilities.ILocomotionCancelable action) action.CancelForLocomotion();
	}

	/// <summary>锚点沿水平单位圆转动；使用角度插值，避免反向输入经过零向量而卡住。</summary>
	public Vector3 ResolveMovementDirection(Vector3 desired, SettingsStore.MovementMode mode, float delta)
	{
		desired.Y = 0f;
		if (mode != SettingsStore.MovementMode.Anchor || desired.LengthSquared() < 0.0001f)
		{
			HasMovementAnchor = false;
			_anchorDirection = Vector3.Zero;
			return desired;
		}
		float targetAngle = Mathf.Atan2(desired.X, desired.Z);
		float angle = HasMovementAnchor
			? Mathf.LerpAngle(Mathf.Atan2(_anchorDirection.X, _anchorDirection.Z), targetAngle,
				1f - Mathf.Exp(-Mathf.Max(0.01f, MovementAnchorTurnSharpness) * Mathf.Max(0f, delta)))
			: targetAngle;
		_anchorDirection = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
		HasMovementAnchor = true;
		return (MovementAnchorPosition - GlobalPosition).Normalized();
	}
}
