using System;
using ActRoguelike.Combat;
using ActRoguelike.Core;
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

	/// <summary>
	/// **这个技能要播的动画名**（配置表 `Config/Tables/Skills/*.tres` 的 <c>AnimName</c> 列）。
	///
	/// ⚠️ 用户裁定（`docs/character-redesign.md` §1.4）：**没有对应动画 → 这个技能不执行**，
	/// 不做"时间驱动兜底"。所以填了本列的技能在 <see cref="TryActivate"/> 里会先查动画：
	/// 查不到就返回 false 并写一条明确的日志（**不静默**）。
	///
	/// 查名走**全路径**（`skills/Skill_A1_ATK1`）：项目里 <c>AnimationPlayer.HasAnimation</c>
	/// **不搜嵌套库**，裸名永远 false（`refactor-survey.md` §1.4 坑 1）。
	/// </summary>
	[Export] public string AnimName { get; set; } = string.Empty;

	/// <summary>
	/// **事件帧名**：动画播到这一帧（方法轨道上的 <c>anim_event</c>）时，才真正释放技能效果。
	/// 留空 = 不用事件帧驱动（技能在 <see cref="OnActivate"/> 里立刻生效）。
	///
	/// 与 <see cref="AnimName"/> 是一对：有动画但没有事件帧名时，动画纯表现、效果照旧走时间轴。
	/// <b>两个都填</b>才是文档说的"随着动画里的事件帧去执行响应事件的逻辑"。
	/// </summary>
	[Export] public string AnimEvent { get; set; } = string.Empty;

	/// <summary>动画速度倍率（&gt;1 加速释放；蓄力类技能用 &lt;1 拉长）。写在**播放器**上（每实例独立）。</summary>
	[Export] public float AnimSpeedScale { get; set; } = 1f;

	/// <summary>
	/// **这个技能绑哪个按键动作**（`project.godot` 的 `[input]` 名，例如 <c>weapon_skill_1</c>）。
	///
	/// 留空 = 这个技能不由玩家按键直接触发（敌人技能、被动、由别的技能驱动）。
	/// 玩家输入（<see cref="Player.PlayerAbilityInput"/>）**只读这一列**，
	/// 不再按"能力的 C# 类名"硬编码 —— 这是"加角色/武器/技能不改代码"的前提
	/// （`AbilityConfig.cs` 里那条"刻意没有 InputAction 列"的注释就是本轮推翻的设计决定）。
	/// </summary>
	[Export] public string InputAction { get; set; } = string.Empty;

	/// <summary>
	/// 按住式输入：为 true 时技能的 <see cref="Tick"/> 会在"按住"期间被调用，
	/// 松开时额外调用一次 <see cref="OnInputReleased"/>（蓄力类技能用）。
	/// </summary>
	[Export] public bool HoldToCharge { get; set; }

	public Unit? OwnerUnit { get; private set; }

	/// <summary>惰性读取：Unit.Movement 在父节点 _Ready 里才赋值，而子节点 _Ready 更早。</summary>
	public CharacterMovement? Movement => OwnerUnit?.Movement;
	public Unit? Target { get; protected set; }
	public bool IsBusy { get; private set; }
	public float CooldownRemaining { get; private set; }
	public bool IsReady => Enabled && !IsBusy && CooldownRemaining <= 0f;

	/// <summary>阶段计时，OnActivate 后从 0 开始累计；子类切换阶段时自行归零。</summary>
	protected float StateTime { get; set; }

	/// <summary>
	/// 动画事件帧**已经到过**（技能正在等的那一帧）。
	/// 与 <see cref="ProjectileAttackAbility"/> 这类"一次释放只打一发"的子类配合：
	/// <see cref="OnAnimEvent"/> 默认实现会置位它，子类读它即可。
	/// </summary>
	protected bool AnimEventReached { get; private set; }

	public event Action<Ability>? Activated;

	/// <summary>动画事件帧的分发（`UnitAnimator.AnimationEvent`）。在拥有者解析出来时挂、退树时摘。</summary>
	private bool _initialized;
	private bool _animEventHookedByMe;

	public override void _PhysicsProcess(double delta)
	{
		EnsureInitialized();
		if (OwnerUnit == null)
		{
			return;
		}

		// 每帧补一次订阅：配置表是在第一个物理帧之后才套上来的（见 EnsureAnimEventHooked 的说明）。
		EnsureAnimEventHooked();

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
		EnsureAnimEventHooked();
		if (OwnerUnit is null or { IsDead: true } || !IsReady || !CanActivate(target))
		{
			return false;
		}

		// ⚠️ 用户裁定：**没有对应动画 → 不执行**（不做时间驱动兜底）。这里就是那条闸门。
		if (!string.IsNullOrEmpty(AnimName) && !EnsureAnimationAvailable())
		{
			return false;
		}

		Target = target;
		IsBusy = true;
		StateTime = 0f;
		AnimEventReached = false;
		OnActivate(target);
		Activated?.Invoke(this);
		return true;
	}

	/// <summary>
	/// 为什么这个技能现在按不出来（自测 / 排障用）。逐条列出 <see cref="TryActivate"/> 的那几个闸门。
	/// 加技能后"按了没反应"最常见的三种原因：没绑按键、冷却还没好、**动画不存在**（本轮新增的那道闸门）。
	/// </summary>
	public string Diagnose(Unit? target = null)
	{
		EnsureInitialized();
		string owner = OwnerUnit == null ? "（没有拥有者）" : OwnerUnit.Name;
		string anim = "（未绑定）";
		if (!string.IsNullOrEmpty(AnimName))
		{
			UnitAnimator? animator = OwnerUnit == null ? null : FindInOwner<UnitAnimator>();
			animator?.PrepareSkillLibrary();
			string resolved = animator?.ResolveSkillAnimation(AnimName) ?? string.Empty;
			anim = resolved.Length > 0 ? $"「{AnimName}」OK" : $"「{AnimName}」**查不到**（库目录没加载？）";
		}

		bool canActivate = OwnerUnit is { IsDead: false } && CanActivate(target);
		return $"{AbilityId}：启用={Enabled} 忙={IsBusy} 冷却剩余={CooldownRemaining:0.##} 拥有者={owner} "
			+ $"动画={anim} 按键={InputAction} 可激活={canActivate}";
	}

	/// <summary>
	/// 拥有者是父节点，其 _Ready 晚于本节点，所以延迟到第一次使用再取。
	/// </summary>
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
		EnsureAnimEventHooked();
		OnReady();
	}

	/// <summary>
	/// 订阅"动画事件帧"（<see cref="UnitAnimator.AnimationEvent"/>）——**只在"本能力真的关心事件帧"时挂**
	/// （否则每个技能都会收到别人的帧）。
	///
	/// ⚠️ <b>必须能被反复调用，不能只在 <see cref="EnsureInitialized"/> 里挂一次</b>（本轮修的真 bug，实测）：
	/// <see cref="AnimEvent"/> 来自配置表，而表的套用（<c>RunController.ApplyBalance</c> →
	/// <c>UnitConfig.ApplySkillsTo</c> → <c>AbilityConfig.ApplyTo</c>）发生在 <c>_Process</c>（idle 帧）里，
	/// **晚于第一个物理帧**；而 <see cref="EnsureInitialized"/> 在第一个物理帧就被
	/// <see cref="_PhysicsProcess"/> 调用了 —— 那时 <see cref="AnimEvent"/> 还是空的，
	/// 于是这次"唯一的一次"订阅机会被判为"不需要"，`_animEventHookedByMe` 永远为 false，
	/// 事件帧**永远不会送到这个能力**。
	///
	/// 表现（实测）：技能动画正常播、<c>UnitAnimator</c> 也真的发出了 <c>ATK</c> 帧，
	/// 但技能收不到 → 既不发射、也没有任何报错，最终以
	/// 「等不到动画事件帧「ATK」，本次施法放弃」收场（`EnergyWaveAbility.Tick` 的超时分支）。
	/// </summary>
	private void EnsureAnimEventHooked()
	{
		if (_animEventHookedByMe || string.IsNullOrEmpty(AnimEvent))
		{
			return;
		}

		UnitAnimator.AnimationEvent += OnAnimationEvent;
		_animEventHookedByMe = true;
	}

	public override void _ExitTree()
	{
		if (_animEventHookedByMe)
		{
			UnitAnimator.AnimationEvent -= OnAnimationEvent;
			_animEventHookedByMe = false;
		}
	}

	/// <summary>
	/// 动画事件帧的过滤与分发：**只认自己等的那一帧**（<see cref="AnimEvent"/>），
	/// 且只在自己正忙的时候认（否则别的单位的帧会打到没在放技能的能力上）。
	/// </summary>
	private void OnAnimationEvent(string name)
	{
		if (!IsBusy || string.IsNullOrEmpty(AnimEvent) || name != AnimEvent)
		{
			return;
		}

		AnimEventReached = true;
		OnAnimEvent(name);
	}

	/// <summary>
	/// **动画帧回调**：默认什么都不做（效果在 <see cref="OnActivate"/> / <see cref="Tick"/> 里做）。
	/// 需要"事件帧才发射"的技能覆写它 —— 那才是文档说的动画驱动。
	/// </summary>
	protected virtual void OnAnimEvent(string name)
	{
	}

	/// <summary>
	/// **按住期间每帧**（仅在 <see cref="HoldToCharge"/> 为 true 且按键仍被按住时由输入层调用）。
	/// 蓄力类技能在这里累加蓄力量。
	/// </summary>
	public virtual void OnInputHeld(float delta)
	{
	}

	/// <summary>**按键抬起**（仅在 <see cref="HoldToCharge"/> 为 true 时由输入层调用）。</summary>
	public virtual void OnInputReleased()
	{
	}

	/// <summary>
	/// 校验"这个技能要的动画真的存在"：单位的 <c>UnitAnimator</c> 把技能动画库加载好之后，
	/// 按**全路径**查一次。查不到 → 写明确日志并返回 false（<see cref="TryActivate"/> 据此拒绝执行）。
	/// </summary>
	private bool EnsureAnimationAvailable()
	{
		UnitAnimator? animator = OwnerUnit == null ? null : FindInOwner<UnitAnimator>();
		if (animator == null)
		{
			GD.PushWarning($"[Ability] {AbilityId} 需要动画「{AnimName}」，但 {OwnerUnit?.Name} 上没有 UnitAnimator，技能不执行。");
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 未执行：单位 {OwnerUnit?.Name} 没有 UnitAnimator（拿不到技能动画）。");
			return false;
		}

		animator.PrepareSkillLibrary();
		string resolved = animator.ResolveSkillAnimation(AnimName);
		if (resolved.Length > 0)
		{
			return true;
		}

		GD.PushWarning($"[Ability] {AbilityId} 需要动画「{AnimName}」，但 {OwnerUnit?.Name} 的动画库里没有它，技能不执行。");
		GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 未执行：动画「{AnimName}」不存在（查名用全路径，单位表 AnimLibraryDir 是否指向了生成目录？）。");
		return false;
	}

	/// <summary>
	/// 播技能动画（技能激活时调用）。返回动画全名；空 = 没播成（调用方应视为失败）。
	/// 顺带把 <c>speed_scale</c> 写到播放器上 —— 加速释放时事件帧仍然精确命中（实测）。
	/// </summary>
	protected string PlaySkillAnimation()
	{
		if (string.IsNullOrEmpty(AnimName))
		{
			return string.Empty;
		}

		return FindInOwner<UnitAnimator>()?.PlaySkillAnimation(AnimName, AnimSpeedScale) ?? string.Empty;
	}

	/// <summary>停掉技能动画（技能结束/取消时调用，把控制权交还"按移速切"）。</summary>
	protected void StopSkillAnimation() => FindInOwner<UnitAnimator>()?.StopSkillAnimation();

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
		StopSkillAnimation();
	}

	protected void Cancel()
	{
		IsBusy = false;
		StateTime = 0f;
		StopSkillAnimation();
	}

	/// <summary>在拥有者子树里找组件（武器、命中体等），避免依赖节点名。</summary>
	protected T? FindInOwner<T>() where T : class
	{
		EnsureInitialized();
		return OwnerUnit == null ? null : NodeSearch.FindInSubtree<T>(OwnerUnit);
	}
}

