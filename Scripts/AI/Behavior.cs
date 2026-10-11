using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI;

/// <summary>
/// 行为基类（执行文档 §4.3）：只决定“怎么做”，不复制移动与攻击的物理/结算逻辑。
/// 挂在 EnemyBrain 下，按 Priority 从高到低取第一个可以运行的行为。
/// </summary>
public abstract partial class Behavior : Node
{
	[Export] public int Priority { get; set; }

	/// <summary>生效距离下限，0 = 不限。</summary>
	[Export] public float MinRange { get; set; }

	/// <summary>生效距离上限，0 = 不限。</summary>
	[Export] public float MaxRange { get; set; }

	protected EnemyBrain Brain { get; private set; } = null!;
	protected Unit OwnerUnit { get; private set; } = null!;

	/// <summary>惰性读取：Unit.Movement 在父节点 _Ready 里才赋值，而子节点 _Ready 更早。</summary>
	protected CharacterMovement? Movement => OwnerUnit.Movement;

	protected Unit? Target => Brain.Target;

	/// <summary>由 EnemyBrain 在 _Ready 时注入。</summary>
	public void Initialize(EnemyBrain brain, Unit owner)
	{
		Brain = brain;
		OwnerUnit = owner;
		OnReady();
	}

	protected virtual void OnReady()
	{
	}

	public virtual bool CanRun() => Target != null && InRange(Target);

	public virtual void OnEnter()
	{
	}

	public virtual void OnExit()
	{
	}

	public abstract void Tick(float delta);

	protected bool InRange(Unit target)
	{
		float distance = OwnerUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		if (MinRange > 0f && distance < MinRange)
		{
			return false;
		}

		return MaxRange <= 0f || distance <= MaxRange;
	}

	private float _avoidSide = 1f;
	private float _avoidFlipTimer;

	/// <summary>
	/// 前方有场景障碍时改走侧向，避免敌人顶在柱子/墙角上原地不动（执行文档的 AvoidObstacle）。
	/// 侧移方向每 1.2 秒翻转一次，防止凹角卡死。
	/// </summary>
	protected Vector3 AvoidObstacles(Vector3 direction, float probeDistance = 2.4f)
	{
		if (direction.LengthSquared() < 0.0001f)
		{
			return direction;
		}

		Vector3 forward = direction.Normalized();
		Vector3 origin = OwnerUnit.GlobalPosition + (Vector3.Up * 0.6f);
		var query = PhysicsRayQueryParameters3D.Create(origin, origin + (forward * probeDistance), CombatLayers.World);
		query.Exclude = new Godot.Collections.Array<Rid> { OwnerUnit.GetRid() };

		if (OwnerUnit.GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0)
		{
			_avoidFlipTimer = 0f;
			return direction;
		}

		_avoidFlipTimer += (float)OwnerUnit.GetPhysicsProcessDeltaTime();
		if (_avoidFlipTimer > 1.2f)
		{
			_avoidFlipTimer = 0f;
			_avoidSide = -_avoidSide;
		}

		Vector3 side = forward.Cross(Vector3.Up).Normalized() * _avoidSide;
		return (side + (forward * 0.35f)).Normalized();
	}

	protected float HorizontalDistanceTo(Unit target)
	{
		Vector3 offset = target.GlobalPosition - OwnerUnit.GlobalPosition;
		offset.Y = 0f;
		return offset.Length();
	}

	protected Vector3 HorizontalDirectionTo(Unit target)
	{
		Vector3 offset = target.GlobalPosition - OwnerUnit.GlobalPosition;
		offset.Y = 0f;
		return offset.LengthSquared() < 0.0001f ? -OwnerUnit.GlobalTransform.Basis.Z : offset.Normalized();
	}
}
