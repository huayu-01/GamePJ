using Godot;

namespace ActRoguelike.Camera;

/// <summary>
/// 第三人称镜头预制体（执行文档 6.2 的独立 CameraRig 方案）。
/// 结构：CameraRig(Yaw) -> SpringArm3D(Pitch/碰撞) -> Camera3D。
/// 设为 TopLevel，因此不继承目标节点的旋转，位置由本脚本跟随 <see cref="Target"/>。
/// 俯角约定：PitchDegrees 为正表示镜头在目标上方俯视。
/// </summary>
public partial class CameraRig : Node3D
{
	[Export] public Node3D? Target { get; set; }

	[Export(PropertyHint.Range, "0.02,2,0.01")] public float MouseSensitivity { get; set; } = 0.25f;

	[Export] public float MinPitchDegrees { get; set; } = -70f;
	[Export] public float MaxPitchDegrees { get; set; } = 70f;

	[Export(PropertyHint.Range, "-70,70,1")] public float PitchDegrees { get; set; } = 20f;

	/// <summary>镜头围绕目标的位置偏移，通常取角色胸口高度。</summary>
	[Export] public Vector3 TargetOffset { get; set; } = new(0f, 1.5f, 0f);

	/// <summary>
	/// 角色在屏幕上的高度比例（0.5 = 正中，越小角色越靠下）。
	/// 第三人称常用 0.25~0.35：角色偏下，视野更多留给前方。
	/// </summary>
	[Export(PropertyHint.Range, "0.05,0.5,0.01")] public float TargetScreenHeight { get; set; } = 0.3f;

	/// <summary>跟随平滑速度（1/秒）。越大越紧跟，0 表示硬跟随。</summary>
	[Export] public float FollowSharpness { get; set; } = 12f;

	[Export] public bool CaptureMouseOnStart { get; set; } = true;

	private SpringArm3D _springArm = null!;
	private float _yawDegrees;

	/// <summary>镜头水平前方向（已归一化），供单位移动与瞄准使用。</summary>
	public Vector3 FlatForward => Flatten(-GlobalTransform.Basis.Z);

	/// <summary>镜头水平右方向（已归一化）。</summary>
	public Vector3 FlatRight => Flatten(GlobalTransform.Basis.X);

	/// <summary>收到过的鼠标移动事件数（排查「UI 抢走视角输入」用）。</summary>
	public int MouseMotionEvents { get; private set; }

	/// <summary>鼠标当前是否被捕获（只有捕获状态下才会转动视角）。</summary>
	public bool IsMouseCaptured => Input.MouseMode == Input.MouseModeEnum.Captured;

	public override void _Ready()
	{
		TopLevel = true;
		_springArm = GetNode<SpringArm3D>("SpringArm");
		_springArm.CollisionMask = ActRoguelike.Combat.CombatLayers.World;
		Target ??= GetParent() as Node3D;
		if (Target is CollisionObject3D body)
		{
			_springArm.AddExcludedObject(body.GetRid());
		}

		_yawDegrees = Mathf.RadToDeg(Rotation.Y);
		ApplyRotation();

		if (CaptureMouseOnStart)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	/// <summary>
	/// 跟随目标。**必须放在 _PhysicsProcess 里**，和物理同频：
	/// 之前写在 _Process（120fps）、而角色位置只在 60Hz 物理帧上变，导致镜头与角色
	/// 「一帧动一帧不动」交替，画面上就是持续缓动 + 残影（实测：镜头单帧位移 0 与 0.06 交替，
	/// 角色/镜头相位差在 4.091~4.133 之间反复抖动）。
	/// 放到物理帧后由物理插值把它插回渲染帧，镜头与角色共享同一对物理状态，相对位置恒定。
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		if (Target == null)
		{
			return;
		}

		Vector3 desired = Target.GlobalPosition + TargetOffset + (Vector3.Up * ScreenHeightLift());
		if (FollowSharpness <= 0f)
		{
			GlobalPosition = desired;
		}
		else
		{
			float weight = 1f - Mathf.Exp(-FollowSharpness * (float)delta);
			GlobalPosition = GlobalPosition.Lerp(desired, weight);
		}
	}

	/// <summary>
	/// 让角色不居中所需的抬高量：镜头看向的是 rig 原点，把它抬高一点，
	/// 角色就落在画面下方（抬高的世界距离 = 屏幕比例差 × 该距离处的视锥高度）。
	/// </summary>
	private float ScreenHeightLift()
	{
		if (TargetScreenHeight >= 0.499f)
		{
			return 0f;
		}

		Camera3D? camera = GetViewport().GetCamera3D();
		float fov = camera?.Fov ?? 75f;
		float distance = Mathf.Max(0.5f, _springArm.SpringLength);
		float viewHeight = 2f * Mathf.Tan(Mathf.DegToRad(fov * 0.5f)) * distance;
		return (0.5f - TargetScreenHeight) * viewHeight;
	}

	/// <summary>
	/// 视角走 _Input 而不是 _UnhandledInput：HUD 上任何 mouse_filter = Stop 的显示元素
	/// （准星、色块…）都会抢先吃掉鼠标事件，导致镜头完全不能转（踩过一次）。
	/// 游戏视角属于「玩家输入」，不该被纯显示的 UI 拦截。
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			MouseMotionEvents++;
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
			{
				_yawDegrees -= motion.Relative.X * MouseSensitivity;
				PitchDegrees = Mathf.Clamp(PitchDegrees + motion.Relative.Y * MouseSensitivity, MinPitchDegrees, MaxPitchDegrees);
				ApplyRotation();
			}
		}
	}

	private void ApplyRotation()
	{
		Rotation = new Vector3(0f, Mathf.DegToRad(_yawDegrees), 0f);
		_springArm.RotationDegrees = new Vector3(-PitchDegrees, 0f, 0f);
	}

	private static Vector3 Flatten(Vector3 direction)
	{
		direction.Y = 0f;
		return direction.Normalized();
	}
}
