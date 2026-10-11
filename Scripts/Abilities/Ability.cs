using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 能力基类（执行文档 §4.3）。挂在 Unit 下，只回答“能不能做、怎么做”，
/// 什么时候做由 Controller / Behavior 决定（§4.4）。子类用 Windup / Recover 组织阶段。
/// 必须是 Node3D：能力下面会挂命中体(Area3D)、武器(Node3D) 等 3D 子节点，
/// 而 Godot 只沿直接父节点解析 3D 变换，普通 Node 会让这些子节点掉在世界原点。
/// 注意：Godot 先执行子节点 _Ready，所以拥有者信息在第一次使用时才解析（见 EnsureInitialized）。
/// </summary>
public abstract partial class Ability : Node3D
{
	/// <summary>技能释放方向的来源。不是所有技能都朝准星，所以做成可选配置。</summary>
	public enum AimSource
	{
		/// <summary>朝角色自身前方。</summary>
		Facing,

		/// <summary>朝当前移动意图方向（没有输入时回落到前方）。</summary>
		MovementIntent,

		/// <summary>朝玩家准星（屏幕中心射线）。敌人没有相机时自动回落到前方。</summary>
		Crosshair,
	}

	/// <summary>本技能朝哪个方向释放（策划文档：位移/突进类默认朝准星）。</summary>
	[Export] public AimSource Aim { get; set; } = AimSource.Facing;

	[Export] public float Cooldown { get; set; } = 1f;
	[Export] public float Windup { get; set; } = 0.3f;
	[Export] public float Recover { get; set; } = 0.3f;
	[Export] public string AbilityId { get; set; } = string.Empty;

	/// <summary>未解锁的能力不参与行为决策（Boss 分阶段解锁技能用）。</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>行为提示：目标进入这个距离区间时适合触发本能力（UseAbilityBehavior 读取）。</summary>
	[Export] public float ApproachMinRange { get; set; }

	[Export] public float ApproachMaxRange { get; set; }

	public Unit? OwnerUnit { get; private set; }

	/// <summary>惰性读取：Unit.Movement 在父节点 _Ready 里才赋值，而子节点 _Ready 更早。</summary>
	public CharacterMovement? Movement => OwnerUnit?.Movement;
	public Unit? Target { get; protected set; }
	public bool IsBusy { get; private set; }
	public float CooldownRemaining { get; private set; }
	public bool IsReady => Enabled && !IsBusy && CooldownRemaining <= 0f;

	/// <summary>阶段计时，OnActivate 后从 0 开始累计；子类切换阶段时自行归零。</summary>
	protected float StateTime { get; set; }

	public event Action<Ability>? Activated;

	private bool _initialized;

	public override void _PhysicsProcess(double delta)
	{
		EnsureInitialized();
		if (OwnerUnit == null)
		{
			return;
		}

		float dt = (float)delta;
		if (CooldownRemaining > 0f)
		{
			CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
		}

		if (IsBusy)
		{
			StateTime += dt;
		}

		Tick(dt);
	}

	public bool TryActivate(Unit? target = null)
	{
		EnsureInitialized();
		if (OwnerUnit is null or { IsDead: true } || !IsReady || !CanActivate(target))
		{
			return false;
		}

		Target = target;
		IsBusy = true;
		StateTime = 0f;
		OnActivate(target);
		Activated?.Invoke(this);
		return true;
	}

	/// <summary>拥有者是父节点，其 _Ready 晚于本节点，所以延迟到第一次使用再取。</summary>
	private void EnsureInitialized()
	{
		if (_initialized)
		{
			return;
		}

		OwnerUnit = GetParent<Unit>();
		if (OwnerUnit == null)
		{
			return;
		}

		if (string.IsNullOrEmpty(AbilityId))
		{
			AbilityId = GetType().Name;
		}

		_initialized = true;
		OnReady();
	}

	protected virtual void OnReady()
	{
	}

	protected virtual bool CanActivate(Unit? target) => true;

	/// <summary>
	/// 按 <see cref="Aim"/> 解析释放方向。位移/突进/远程技能都可以直接用它，
	/// 这样「朝准星」只是一个配置项，而不是每个技能各写一遍。
	/// </summary>
	protected Vector3 ResolveAimDirection()
	{
		Vector3 facing = OwnerUnit != null ? -OwnerUnit.GlobalTransform.Basis.Z : Vector3.Forward;
		switch (Aim)
		{
			case AimSource.Crosshair:
				return AimUtil.GetCrosshairDirection(GetViewport(), facing);
			case AimSource.MovementIntent:
				Vector3 intent = Movement?.IntentDirection ?? Vector3.Zero;
				return intent.LengthSquared() > 0.04f ? intent : facing;
			default:
				return facing;
		}
	}

	protected abstract void OnActivate(Unit? target);

	protected virtual void Tick(float delta)
	{
	}

	protected void Finish()
	{
		IsBusy = false;
		StateTime = 0f;
		CooldownRemaining = Cooldown;
	}

	protected void Cancel()
	{
		IsBusy = false;
		StateTime = 0f;
	}

	/// <summary>在拥有者子树里找组件（武器、命中体等），避免依赖节点名。</summary>
	protected T? FindInOwner<T>() where T : class
	{
		EnsureInitialized();
		return OwnerUnit == null ? null : NodeSearch.FindInSubtree<T>(OwnerUnit);
	}
}
