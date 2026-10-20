using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
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
	protected virtual bool WarnOnActivate => true;
	/// <summary>地面主动移动：默认锁定，明确放行，或由被动授予攻击移动。</summary>
	public enum MovementPolicy { Locked, Allowed, Attack }
	[Export] public MovementPolicy ActionMovement { get; set; }
	private CharacterMovement? _registeredMovement;
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
	[ExportGroup("动作打断")]
	/// <summary>默认允许后来的动作打断；明确不可打断的技能行关闭此项。</summary>
	[Export] public bool CanBeInterrupted { get; set; } = true;
	/// <summary>起手锁定的动画时间（秒）；0表示立即可打断，随动画加速同步。</summary>
	[Export] public float InterruptLockSeconds { get; set; }
	/// <summary>动画事件缺失时只取消；此值不用于计时兜底执行效果。</summary>
	[Export] public float AnimationEventTimeout { get; set; } = 2f;
	[ExportGroup("行为提示")]

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
	/// **第二个事件帧名**（"一段动画里夹出一个区间"的技能用；空 = 不用，行为与改动前逐位一致）。
	///
	/// <para>设计文档（角色 A 技能 1「后撤」）：「帧 <c>Dodge</c> 到 <c>Dodger</c> 期间无敌
	/// （相当于 Dodge 开无敌、Dodger 关无敌）」—— 这就是**两个帧夹一个区间**。
	/// 因为 <see cref="OnAnimationEvent"/> 的过滤只认 <see cref="AnimEvent"/> 一个名字，
	/// 所以这里补一列：填了它之后，<b>这两个名字的事件帧都会送到</b> <see cref="OnAnimEvent"/>，
	/// 子类按名字分支即可（`Dodge` → 开无敌，`Dodger` → 关无敌）。</para>
	///
	/// <para>它同样是"没有对应动画 → 不执行"那套的一部分：两个帧都必须真的在动画里
	/// （`AnimEvent` / `AnimEventEnd` 两张生成表列）。</para>
	/// </summary>
	[Export] public string AnimEventEnd { get; set; } = string.Empty;

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

	/// <summary>纯效果在后台运行：不占动作、不播放或停止角色技能动画。默认 false，原技能语义不变。</summary>
	[Export] public bool IsBackground { get; set; }

	/// <summary>按住维持效果，松开结束；与蓄力释放分开配置。</summary>
	[Export] public bool HoldToMaintain { get; set; }

	/// <summary>维持效果的无动画软启动时长，不参与蓄力档位或完美窗口。</summary>
	[Export] public float MaintainStartupSeconds { get; set; }
	public float HeldTime { get; private set; }
	public bool IsMaintaining => IsActive && HoldToMaintain && _maintainHeld;
	public bool MaintainStartupComplete => HeldTime >= Mathf.Max(0f, MaintainStartupSeconds);
	private bool _maintainHeld;

	[ExportGroup("冷却缩减（CDR）—— 数值全部来自表，代码里不写任何百分比")]
	/// <summary>
	/// **本技能的冷却倍率**（行级：`1` = 不变、`0.8` = 冷却 -20%、`0.5` = 减半）。
	///
	/// 与**单位级常驻 CDR**（<see cref="UnitCooldownMultiplier"/>，洞悉 / 速攻这类增幅挂
	/// `StatSheet` 的 <c>CooldownMultiplier</c>）**相乘**，见 <see cref="EffectiveCooldown"/>。
	///
	/// ⚠️ 技能行里**留空 / 0 = 不覆盖**（保留场景里的 1），与 <see cref="Cooldown"/> 同一条纪律。
	/// </summary>
	[Export] public float CooldownMultiplier { get; set; } = 1f;

	[ExportGroup("技能层数（charges）")]
	/// <summary>
	/// **技能层数**（设计文档第 13 行：「每次使用技能消耗一层」）。
	/// **1 = 现在的行为**（单层，等价于纯冷却）；**&gt;1** 时每层按 <see cref="RechargeSeconds"/> 独立回复。
	/// 技能行里**留空 / 0 = 不覆盖**（保留场景里的 1）。
	/// </summary>
	[Export] public int MaxCharges { get; set; } = 1;

	/// <summary>
	/// **每层回复间隔**（秒；≤0 = 退化成 <see cref="Cooldown"/>）。
	/// 文档给的参考值是 0.5s（「间隔 0.5s」）—— 它是**表值**，别写死在代码里。
	/// 只在 <see cref="MaxCharges"/> &gt; 1 时有意义。
	/// </summary>
	[Export] public float RechargeSeconds { get; set; }

	[ExportGroup("蓄力（按住激活 → 抬起释放；四个武技共用这一套）")]
	/// <summary>
	/// **基础蓄力时长**（秒）。<see cref="HoldToCharge"/> 为 true 时：
	/// **没蓄满就松手 = 不释放效果**（设计文档第 14 行）。0 = 按住即算蓄满（松手立刻释放）。
	/// </summary>
	[Export] public float ChargeBaseSeconds { get; set; }

	/// <summary>
	/// **后置蓄力**：基础蓄满之后**每多少秒升一档**（≤0 = 没有后置蓄力，只有基础蓄力这一段）。
	/// 文档里的弓（0.1s / +30%）、短弩（0.2s / +2 支）、长枪（0.25s / +20%）都是这一段。
	/// </summary>
	[Export] public float ChargeStepSeconds { get; set; }

	/// <summary>每档的威力倍率（1.3 = 每档 +30%，见 <see cref="ChargePowerScale"/>；≤0 视为 1 = 不增强）。</summary>
	[Export] public float ChargeStepScale { get; set; } = 1f;

	/// <summary>后置蓄力最多升几档（≤0 = 不限，由 <see cref="ChargeMaxSeconds"/> 封顶）。</summary>
	[Export] public int ChargeMaxSteps { get; set; }

	/// <summary>后置蓄力的**总时长上限**（秒；≤0 = 不限）。与 <see cref="ChargeMaxSteps"/> 谁先到听谁。</summary>
	[Export] public float ChargeMaxSeconds { get; set; }

	/// <summary>
	/// **完美时机窗口**（秒；≤0 = 没有完美时机）：**刚蓄满**之后的这一段里松手 = 完美释放。
	/// 文档（能量掌武技2）：0.2s。
	/// </summary>
	[Export] public float PerfectWindowSeconds { get; set; }

	public Unit? OwnerUnit { get; private set; }

	/// <summary>惰性读取：Unit.Movement 在父节点 _Ready 里才赋值，而子节点 _Ready 更早。</summary>
	public CharacterMovement? Movement => OwnerUnit?.Movement;
	public Unit? Target { get; protected set; }
	public bool IsActive { get; private set; }
	public bool IsBusy => IsActive && !IsBackground && OccupiesAction;
	/// <summary>持续实体在出手后可释放动作占用；默认技能保持原语义。</summary>
	protected virtual bool OccupiesAction => true;
	public double ActionTimelineTime => _playedAnimator != null && IsInstanceValid(_playedAnimator)
		&& _animationPlaybackId != 0 && _playedAnimator.SkillPlaybackId == _animationPlaybackId
		? _playedAnimator.SkillPlaybackPosition : StateTime;
	public bool IsInterruptibleNow => CanBeInterrupted && ActionTimelineTime + 0.000001 >= InterruptLockSeconds;
	public float InterruptLockRemaining => IsBusy ? Mathf.Max(0, InterruptLockSeconds - (float)ActionTimelineTime) : 0;
	public float CooldownRemaining { get; private set; }

	/// <summary>
	/// 能不能用。⚠️ <see cref="MaxCharges"/> ≤ 1 时**逐位等价于改动前**（只看 <see cref="CooldownRemaining"/>）。
	/// </summary>
	public virtual bool IsReady => Enabled && !IsActive && (MaxCharges > 1 ? ChargesRemaining > 0 : CooldownRemaining <= 0f);
	/// <summary>施法结束后仍持续的效果秒数，供HUD显示；不占用前台动作状态。</summary>
	public virtual float EffectRemainingSeconds => 0f;

	/// <summary>
	/// 剩余层数。单层技能（<see cref="MaxCharges"/> ≤ 1）退化成"冷却好了就是 1 层"，
	/// 于是 UI 读同一个属性就能同时显示层数与冷却。
	/// </summary>
	public int ChargesRemaining => MaxCharges > 1
		? (_charges < 0 ? MaxCharges : _charges)
		: (CooldownRemaining <= 0f ? 1 : 0);

	/// <summary>
	/// **单位级常驻冷却缩减**（洞悉 / 速攻这类增幅挂 `StatSheet` 的 <c>CooldownMultiplier</c>，默认 1）。
	/// ⚠️ 读到 0（键没有基础值）时按 1 算 —— 不能因为少一个基础值变成"零冷却"（architecture.md 第 8 条）。
	/// </summary>
	public float UnitCooldownMultiplier
	{
		get
		{
			float value = OwnerUnit?.Stats.Get("CooldownMultiplier") ?? 1f;
			return value > 0f ? value : 1f;
		}
	}

	/// <summary>行级 × 单位级的合成冷却倍率（1 = 一点没减）。</summary>
	public float CooldownMultiplierEffective => Mathf.Max(0f, CooldownMultiplier) * UnitCooldownMultiplier;

	/// <summary>这个技能**实际用到的冷却秒数**（<see cref="Finish"/> 写进 <see cref="CooldownRemaining"/> 的值）。</summary>
	public float EffectiveCooldown => Cooldown * CooldownMultiplierEffective;

	/// <summary>每层回复间隔（秒）：表里没给 <see cref="RechargeSeconds"/> 时退化成 <see cref="EffectiveCooldown"/>。</summary>
	public float EffectiveRechargeSeconds => RechargeSeconds > 0f
		? RechargeSeconds * CooldownMultiplierEffective
		: EffectiveCooldown;

	/// <summary>是否正在蓄力（按住期间为 true）。松手后立刻变 false，但 <see cref="ChargeTime"/> 保留最后一次的快照。</summary>
	public bool IsCharging { get; private set; }

	/// <summary>本次蓄力累计的秒数（松手之后保留最后一次的值，供 UI / 自测取证）。</summary>
	public float ChargeTime { get; private set; }
	/// <summary>本次已经松手；等待释放动画期间不能再次蓄力或重复提交释放。</summary>
	public bool ChargeReleased { get; private set; }
	public bool AlwaysPerfectRelease { get; private set; }
	public bool AutoReleaseOnFullCharge { get; private set; }
	private float _chargeBaseOverride = -1f, _chargeStepOverride = -1f, _chargeStepTimeScale = 1f;
	private float? _chargeCooldownBaseline;
	/// <summary>进化只配置指定能力；独立字段允许花心与心流按任意获取顺序组合。</summary>
	public virtual void ConfigureChargeBehavior(ChargeBehaviorTuning tuning)
	{
		if (tuning.CooldownScale >= 0f && tuning.TargetAbility is { } row)
		{
			// 技能表的0表示保留场景倍率，不能把它当作零冷却；重复施效不再次折半。
			_chargeCooldownBaseline ??= CooldownMultiplier;
			CooldownMultiplier = (row.CooldownMultiplier > 0f ? row.CooldownMultiplier : _chargeCooldownBaseline.Value) * tuning.CooldownScale;
		}
		if (tuning.BaseSecondsOverride > 0f) _chargeBaseOverride = tuning.BaseSecondsOverride;
		if (tuning.StepSecondsOverride > 0f) _chargeStepOverride = tuning.StepSecondsOverride;
		if (tuning.StepTimeScale > 0f && tuning.StepTimeScale != 1f) _chargeStepTimeScale = tuning.StepTimeScale;
		AlwaysPerfectRelease |= tuning.AlwaysPerfectRelease;
		AutoReleaseOnFullCharge |= tuning.AutoReleaseOnFullCharge;
	}
	/// <summary>释放动画的播放速度，独立于蓄力期间的拉伸速度。</summary>
	[Export] public float ChargeReleaseAnimSpeedScale { get; set; } = 1f;
	/// <summary>允许未完成基础蓄力时松手，使用独立低伤倍率。</summary>
	[Export] public bool AllowEarlyChargeRelease { get; set; }
	[Export(PropertyHint.Range, "0,1,0.01")] public float EarlyReleaseDamageScale { get; set; } = 0.5f;
	public bool LastReleaseEarly { get; private set; }
	/// <summary>松手时冻结的纯伤害倍率，不缩短弹速、射程，也不影响后置蓄力档位。</summary>
	public float LastReleaseDamageScale { get; private set; } = 1f;
	protected virtual bool CanReleaseBeforeBase => AllowEarlyChargeRelease;

	/// <summary>后置蓄力已经升到第几档（0 = 只有基础蓄力那一段）。</summary>
	public int ChargeSteps { get; private set; }

	/// <summary>
	/// **实际基础蓄力** —— 表的 <see cref="ChargeBaseSeconds"/> 可以被单位级修正器覆盖
	/// （<c>StatSheet.ChargeBaseSecondsOverride</c> &gt; 0 才覆盖）。异化/进化里的"蓄力时间变为 X"
	/// 全局来源仍走这一条；心流已迁入指定技能的局部覆盖，优先于全局值。
	/// 没有修正器时**逐位等于** <see cref="ChargeBaseSeconds"/>。
	/// </summary>
	public float ChargeBaseSecondsEffective
	{
		get
		{
			if (_chargeBaseOverride > 0f) return _chargeBaseOverride;
			float over = OwnerUnit?.Stats.Get("ChargeBaseSecondsOverride") ?? 0f;
			if (over > 0f)
			{
				return over;
			}

			return ChargeBaseSeconds;
		}
	}

	/// <summary>
	/// **实际后置蓄力间隔** = （单位级覆盖 &gt; 0 ? 覆盖 : 表的 <see cref="ChargeStepSeconds"/>）× 倍率。
	/// 保留全局来源兼容；花心使用局部缩时，心流的固定间隔覆盖优先，避免获取顺序改变结果。
	/// </summary>
	public float ChargeStepSecondsEffective
	{
		get
		{
			if (_chargeStepOverride > 0f) return _chargeStepOverride;
			float over = OwnerUnit?.Stats.Get("ChargeStepSecondsOverride") ?? 0f;
			float mult = OwnerUnit?.Stats.Get("ChargeStepSecondsMultiplier") ?? 1f;
			return (over > 0f ? over : ChargeStepSeconds) * (mult > 0f ? mult : 1f) * _chargeStepTimeScale;
		}
	}

	/// <summary>基础蓄力是否完成（**没完成就松手 = 不释放**）。</summary>
	public bool ChargeBaseComplete => ChargeTime >= ChargeBaseSecondsEffective;

	/// <summary>蓄力进度（0~1，按**基础蓄力**算；UI 的"指示器往准星缩"读它）。</summary>
	public float ChargeProgress => ChargeBaseSecondsEffective <= 0f ? 1f : Mathf.Clamp(ChargeTime / ChargeBaseSecondsEffective, 0f, 1f);

	/// <summary>每档按基础值加算；例如1.2的四档为1.8倍。</summary>
	public float ChargePowerScale => ChargeStepScale > 0f ? Mathf.Max(0f, 1f + (ChargeStepScale - 1f) * ChargeSteps) : 1f;

	/// <summary>是否处在**完美时机窗口**里（刚蓄满之后的 <see cref="PerfectWindowSeconds"/> 那一段）。</summary>
	public bool IsPerfectWindow => ChargeBaseComplete && (AlwaysPerfectRelease || (PerfectWindowSeconds > 0f
		&& ChargeTime <= ChargeBaseSecondsEffective + PerfectWindowSeconds));

	/// <summary>后置蓄力的档数上限（把 <see cref="ChargeMaxSteps"/> 与 <see cref="ChargeMaxSeconds"/> 合成一个数）。</summary>
	public int ChargeMaxStepsEffective
	{
		get
		{
			float baseSeconds = ChargeBaseSecondsEffective;
			float stepSeconds = ChargeStepSecondsEffective;
			if (stepSeconds <= 0f)
			{
				return 0;
			}

			int steps = ChargeMaxSteps > 0 ? ChargeMaxSteps : int.MaxValue;
			if (ChargeMaxSeconds > baseSeconds)
			{
				// ⚠️ 浮点容差（J10）：表里的 (1.4-0.5)/0.3 在 float 下是 2.9999998，Floor 会少算一档
				// ——「基础 1 档 + 后置 3 档 = 4 个威力档」就变成 2 档。这里加一个远小于任何真实档位的
				// epsilon（1e-4 档），只影响"表值正好落在整数档边界"的行；既有行（长枪 (2.0-1.0)/0.25 = 4
				// 精确、短弩用 ChargeMaxSteps 封顶、其余 step ≤ 0 直接返回 0）的结果一个都不变。
				steps = Mathf.Min(steps, (int)Mathf.Floor(((ChargeMaxSeconds - baseSeconds) / stepSeconds) + 0.0001f));
			}

			return Mathf.Max(0, steps);
		}
	}

	/// <summary>上一次**松手释放**是不是完美时机（表现层 / 自测读它）。</summary>
	public bool LastReleasePerfect { get; private set; }

	/// <summary>上一次**松手**是不是"基础蓄力没完成"而放弃（没有释放任何效果）。</summary>
	public bool LastReleaseAborted { get; private set; }

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
	private UnitAnimator? _eventAnimator;
	private UnitAnimator? _playedAnimator;
	private ulong _animationPlaybackId;
	private readonly List<Ability> _actionCandidates = new();
	private float _finishCooldownScale = 1;
	private float _finishCooldownSubtract;

	/// <summary>剩余层数（<see cref="MaxCharges"/> &gt; 1 才用；&lt; 0 = 还没同步过）。</summary>
	private int _charges = -1;

	/// <summary>
	/// **玩家按了一次本技能的键**（P4 新增，**只加不删**）。
	///
	/// <para>默认实现 = 原来的行为（<see cref="TryActivate"/>）。多段连招的子类覆写它：
	/// 第一次按走 <see cref="TryActivate"/>（开新的一次施法），之后的按走"推进一段"。</para>
	///
	/// <para>为什么要有这个入口：<c>PlayerAbilityInput</c> 只认表里的 <c>InputAction</c> 列，
	/// 它**不该知道**哪个技能是连招（那会把"加技能不改代码"这条纪律破掉）。</para>
	/// </summary>
	public virtual bool TryPress(Unit? target = null) => TryActivate(target);

	/// <summary>
	/// **由"事件驱动"而不是"按键驱动"的技能进入施法态**（P4 新增，**只加不删**）。
	///
	/// <para>被动技能（<c>LandingBurstAbility</c> 那种）没有按键，它的效果由上游技能的事件
	/// （如「下落攻击落地」）触发 —— 那不是 <see cref="TryActivate"/> 那条路（那条要求
	/// <see cref="IsReady"/>，而"触发间隔"是它自己判的），但也需要 <see cref="IsBusy"/> 与
	/// <see cref="StateTime"/> 进入"施法中"，<see cref="Tick"/> 才会跑。</para>
	///
	/// <para>⚠️ 它**不消耗冷却**（冷却由调用方在结束时走 <see cref="Finish"/> 计），
	/// 也不改任何既有状态机 —— 没人调它就等于不存在。</para>
	/// </summary>
	protected void BeginEventDrivenCast()
	{
		_finishCooldownScale = 1;
		_finishCooldownSubtract = 0;
		IsActive = true;
		StateTime = 0f;
	}

	/// <summary>上一次同步时看到的 <see cref="MaxCharges"/>（行换了就重置成满层）。</summary>
	private int _chargesForMaxCharges = -1;

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
		if (IsActive && (!Enabled || OwnerUnit.IsDead))
		{
			Cancel();
		}
		SyncCharges();
		TickCooldown(dt);
		// 命中停顿只延长动作，不吞输入、不延长独立技能的冷却。
		if (OwnerUnit.ImpactPause?.IsHolding == true) return;

		if (IsActive)
		{
			StateTime += dt;
		}

		Tick(dt);
	}

	/// <summary>
	/// 层数状态跟着 <see cref="MaxCharges"/> 走：行被换掉（换角色 / 换武器 / 增幅改层数）就重置成满层。
	/// <see cref="MaxCharges"/> ≤ 1 时它只是把 <c>_charges</c> 摆到 1，**不参与任何判定**。
	/// </summary>
	private void SyncCharges()
	{
		if (_chargesForMaxCharges == MaxCharges)
		{
			return;
		}

		_chargesForMaxCharges = MaxCharges;
		_charges = Mathf.Max(1, MaxCharges);
	}

	/// <summary>
	/// 冷却 / 层数回复。⚠️ **<see cref="MaxCharges"/> ≤ 1 时逐字等价于改动前的"冷却倒计时"那一小段**；
	/// &gt;1 时改成"每 <see cref="EffectiveRechargeSeconds"/> 回一层"（每层独立回复，设计文档第 13 行）。
	/// </summary>
	private void TickCooldown(float dt)
	{
		if (MaxCharges > 1)
		{
			if (_charges >= MaxCharges)
			{
				return;
			}

			CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
			if (CooldownRemaining <= 0f)
			{
				_charges++;
				CooldownRemaining = _charges < MaxCharges ? EffectiveRechargeSeconds : 0f;
			}

			return;
		}

		if (CooldownRemaining > 0f)
		{
			CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
		}
	}

	/// <summary>层数已经用掉时把"回层计时"跑起来（用掉第一层的那一刻调用）。</summary>
	private void EnsureRechargeRunning()
	{
		if (MaxCharges > 1 && _charges < MaxCharges && CooldownRemaining <= 0f)
		{
			CooldownRemaining = EffectiveRechargeSeconds;
		}
	}

	/// <summary>消耗一层（<see cref="MaxCharges"/> ≤ 1 时**什么都不做** —— 默认行为逐位不变）。</summary>
	private void ConsumeCharge()
	{
		if (MaxCharges <= 1)
		{
			return;
		}

		SyncCharges();
		_charges = Mathf.Max(0, _charges - 1);
		if (CooldownRemaining <= 0f)
		{
			CooldownRemaining = EffectiveRechargeSeconds;
		}
	}

	public bool TryActivate(Unit? target = null)
	{
		EnsureInitialized();
		EnsureAnimEventHooked();
		if (OwnerUnit is null or { IsDead: true } || OwnerUnit.Movement?.IsControlDisabled == true || !IsReady || !CanActivate(target))
		{
			return false;
		}

		// ⚠️ 用户裁定：**没有对应动画 → 不执行**（不做时间驱动兜底）。这里就是那条闸门。
		if (!IsBackground && !string.IsNullOrEmpty(AnimName) && !EnsureAnimationAvailable())
		{
			return false;
		}
		if (!TakeAction()) return false;

		Target = target;
		SyncCharges();
		ResetChargeState();
		ConsumeCharge();
		_finishCooldownScale = 1;
		_finishCooldownSubtract = 0;
		IsActive = true;
		_maintainHeld = HoldToMaintain;
		HeldTime = 0f;
		StateTime = 0f;
		AnimEventReached = false;
		OnActivate(target);
		if (WarnOnActivate && IsActive && !IsBackground && OwnerUnit.Faction == Faction.Enemy)
			OwnerUnit.GetNodeOrNull<UnitPresentation>("Presentation")?.FlashAttack();
		Activated?.Invoke(this);
		return true;
	}

	/// <summary>先确认所有旧动作可交接，再统一收尾；无效请求不影响当前动作。</summary>
	private bool TakeAction()
	{
		if (IsBackground || !OccupiesAction || OwnerUnit == null) return true;
		_actionCandidates.Clear();
		NodeSearch.CollectInSubtree(OwnerUnit, _actionCandidates);
		foreach (Ability other in _actionCandidates)
			if (other != this && other.IsBusy && !other.IsInterruptibleNow) return false;
		foreach (Ability other in _actionCandidates)
		{
			if (other == this || !other.IsBusy) continue;
			GameLog.Write("Combat", $"动作交接：{other.AbilityId} 被 {AbilityId} 打断（动画时间{other.ActionTimelineTime:0.###}s）。");
			// 已起手的技能进入既有冷却／回层；未触发的事件不补发。
			other.Finish();
		}
		return true;
	}

	/// <summary>
	/// **一次性冷却缩减（按比例）** —— 供增幅在"事件触发"时调用
	/// （闪身步：技能1 击杀 → 冷却 -80%）。**比例是表值**，代码里不写任何百分比。
	/// </summary>
	/// <param name="fraction">要减掉的比例（0.8 = 剩余冷却 ×0.2；夹在 0~1）。</param>
	public void ReduceCooldownFraction(float fraction)
	{
		float clamped = Mathf.Clamp(fraction, 0f, 1f);
		CooldownRemaining = Mathf.Max(0f, CooldownRemaining * (1f - clamped));
		if (IsActive && MaxCharges <= 1)
		{
			_finishCooldownScale *= 1 - clamped;
			_finishCooldownSubtract *= 1 - clamped;
		}
	}

	/// <summary>**一次性冷却缩减（按秒）**：把剩余冷却减去若干秒（增幅 / 词条用）。</summary>
	public void ReduceCooldown(float seconds)
	{
		float amount = Mathf.Max(0, seconds);
		CooldownRemaining = Mathf.Max(0, CooldownRemaining - amount);
		if (IsActive && MaxCharges <= 1) _finishCooldownSubtract += amount;
	}

	/// <summary>
	/// **刷新冷却**（招架反击：技能2 格挡成功 → 刷新冷却）：剩余冷却清零，且**层数补满**
	/// （有层数的技能，"刷新"= 立刻又能用）。
	/// </summary>
	public void RefreshCooldown()
	{
		CooldownRemaining = 0f;
		_finishCooldownScale = IsActive && MaxCharges <= 1 ? 0 : 1;
		_finishCooldownSubtract = 0;
		SyncCharges();
		_charges = Mathf.Max(1, MaxCharges);
	}

	/// <summary>
	/// 在单位子树里按**技能行 Id** 找能力（增幅要"改某个技能"时用它 ——
	/// 与"按键 → 技能"那条路一样，都只认表里的标识，不认节点名 / C# 类名）。
	/// </summary>
	public static Ability? FindByAbilityId(Unit unit, string abilityId)
	{
		if (string.IsNullOrEmpty(abilityId))
		{
			return null;
		}

		var abilities = new List<Ability>();
		NodeSearch.CollectInSubtree(unit, abilities);
		foreach (Ability ability in abilities)
		{
			if (ability.AbilityId == abilityId)
			{
				return ability;
			}
		}

		return null;
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
		string charges = MaxCharges > 1 ? $"层数={ChargesRemaining}/{MaxCharges}（回层 {EffectiveRechargeSeconds:0.##}s） " : string.Empty;
		string charge = HoldToCharge
			? $"蓄力={ChargeTime:0.##}/{ChargeBaseSeconds:0.##}s 档={ChargeSteps}/{ChargeMaxStepsEffective} 威力×{ChargePowerScale:0.###} 完美窗口={IsPerfectWindow} "
			: string.Empty;
		return $"{AbilityId}：启用={Enabled} 忙={IsBusy} 冷却剩余={CooldownRemaining:0.##}/{EffectiveCooldown:0.##}"
			+ $"（CDR 行×{CooldownMultiplier:0.##} 单位×{UnitCooldownMultiplier:0.##}） {charges}{charge}"
			+ $"拥有者={owner} 动画={anim} 按键={InputAction} 可激活={canActivate}";
	}

	/// <summary>
	/// 拥有者是父节点，其 _Ready 晚于本节点，所以延迟到第一次使用再取。
	/// </summary>
	private void EnsureInitialized()
	{
		if (_registeredMovement == null && Movement is { } movement)
		{
			movement.RegisterAbility(this);
			_registeredMovement = movement;
		}
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
		if (Movement is { } initialMovement)
		{
			initialMovement.RegisterAbility(this);
			_registeredMovement = initialMovement;
		}
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
		if (IsBackground)
		{
			return;
		}
		if (_animEventHookedByMe || (string.IsNullOrEmpty(AnimEvent) && string.IsNullOrEmpty(AnimEventEnd)))
		{
			return;
		}

		_eventAnimator = FindInOwner<UnitAnimator>();
		if (_eventAnimator == null) return;
		_eventAnimator.SkillEvent += OnAnimationEvent;
		_animEventHookedByMe = true;
	}

	public override void _ExitTree()
	{
		if (_registeredMovement != null && IsInstanceValid(_registeredMovement)) _registeredMovement.UnregisterAbility(this);
		_registeredMovement = null;
		if (_animEventHookedByMe)
		{
			if (_eventAnimator != null) _eventAnimator.SkillEvent -= OnAnimationEvent;
			_animEventHookedByMe = false;
			_eventAnimator = null;
		}
	}

	/// <summary>
	/// 只订阅拥有者的实例事件，并校验本能力的播放序号；再过滤起止事件名。
	/// 同单位重播同名动画和其他单位的同名ATK都不能串触发。
	/// </summary>
	private void OnAnimationEvent(string name, ulong playbackId)
	{
		if (!IsActive || IsBackground || _animationPlaybackId == 0 || playbackId != _animationPlaybackId)
		{
			return;
		}

		bool first = !string.IsNullOrEmpty(AnimEvent) && name == AnimEvent;
		bool second = !string.IsNullOrEmpty(AnimEventEnd) && name == AnimEventEnd;
		if (!first && !second)
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

	/// <summary>由当前槽位的输入消费者驱动面向玩家的预览；不依赖技能的飞行或收招生命周期。</summary>
	public virtual void UpdateInputPreview(bool held) { }

	/// <summary>
	/// **按住期间每帧**（仅在 <see cref="HoldToCharge"/> 为 true 且按键仍被按住时由输入层调用）。
	///
	/// 基类在这里**只做通用的蓄力记账**（基础蓄力 + 后置蓄力档位），子类要读就通过
	/// <see cref="ChargeTime"/> / <see cref="ChargeSteps"/> / <see cref="ChargePowerScale"/> /
	/// <see cref="IsPerfectWindow"/> 读，**不要在子类里重新实现一遍计时**
	/// （那正是"6 把武器各写一套、早晚对不齐"的来源，见 docs/abilities.md）。
	/// </summary>
	public virtual void OnInputHeld(float delta)
	{
		if (IsMaintaining)
		{
			HeldTime += Mathf.Max(0f, delta);
			return;
		}
		if (!HoldToCharge || !IsBusy || ChargeReleased)
		{
			return;
		}

		if (!IsCharging)
		{
			IsCharging = true;
			ChargeTime = 0f;
			ChargeSteps = 0;
		}

		// ⚠️ 这里**不封顶**：封了顶就永远"在完美时机窗口里"（松开也分不出"刚闭合"与"按住很久"）。
		// 上限只作用在后置蓄力的**档数**上（ChargeMaxStepsEffective）。
		ChargeTime += delta;
		RecountChargeSteps();
		// 自动提交与松手共用一次性快照，仍由子类等待释放动画的真实ATK。
		if (AutoReleaseOnFullCharge && ChargeBaseComplete && ChargeSteps >= ChargeMaxStepsEffective) OnInputReleased();
	}

	/// <summary>
	/// **按键抬起**（仅在 <see cref="HoldToCharge"/> 为 true 时由输入层调用）。
	///
	/// 两条路（设计文档第 14 行）：
	/// <list type="bullet">
	/// <item>基础蓄力未完成：允许提前释放时冻结低伤倍率，否则走取消分支。</item>
	/// <item>**蓄满了** → 走 <see cref="OnChargeReleased"/>，并带上"是不是完美时机 / 第几档 / 威力倍率"。</item>
	/// </list>
	/// </summary>
	public virtual void OnInputReleased()
	{
		if (IsMaintaining)
		{
			_maintainHeld = false;
			OnMaintainReleased();
			return;
		}
		if (!HoldToCharge || !IsBusy || ChargeReleased)
		{
			return;
		}

		ChargeReleased = true;
		bool released = ChargeBaseComplete || CanReleaseBeforeBase;
		LastReleasePerfect = ChargeBaseComplete && IsPerfectWindow;
		LastReleaseAborted = !released;
		LastReleaseEarly = released && !ChargeBaseComplete;
		LastReleaseDamageScale = LastReleaseEarly && AllowEarlyChargeRelease ? Mathf.Clamp(EarlyReleaseDamageScale, 0f, 1f) : 1f;
		int steps = ChargeSteps;
		float power = ChargePowerScale;
		IsCharging = false;

		if (!released)
		{
			OnChargeAborted();
			return;
		}

		OnChargeReleased(LastReleasePerfect, steps, power);
	}

	/// <summary>
	/// 松手被接受后冻结释放结果；低伤倍率单独读取LastReleaseDamageScale，不混入档位或弹速。
	/// （都不是"必须用"的 —— 子类也可以直接读那几个只读属性）。
	/// </summary>
	protected virtual void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
	}

	/// <summary>**基础蓄力没完成就松手**（设计文档：不释放）。默认什么都不做，子类通常在这里收尾。</summary>
	protected virtual void OnChargeAborted()
	{
	}

	/// <summary>把已蓄时长换算成"后置蓄力第几档"。</summary>
	private void RecountChargeSteps()
	{
		float baseSeconds = ChargeBaseSecondsEffective;
		float stepSeconds = ChargeStepSecondsEffective;
		if (stepSeconds <= 0f || ChargeTime <= baseSeconds)
		{
			ChargeSteps = 0;
			return;
		}

		int steps = (int)Mathf.Floor((ChargeTime - baseSeconds) / stepSeconds);
		ChargeSteps = Mathf.Max(0, Mathf.Min(steps, ChargeMaxStepsEffective));
	}

	/// <summary>下一次施法前把蓄力状态清干净（<see cref="TryActivate"/> 调用）。</summary>
	private void ResetChargeState()
	{
		ChargeReleased = false;
		IsCharging = false;
		ChargeTime = 0f;
		ChargeSteps = 0;
		LastReleasePerfect = false;
		LastReleaseAborted = false;
		LastReleaseEarly = false;
		LastReleaseDamageScale = 1f;
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
		return PlaySkillAnimationNamed(AnimName);
	}
	/// <summary>松手冻结结果后重播释放段，事件仍由本单位的本轮动画派发。</summary>
	protected bool PlayChargeReleaseAnimation()
	{
		StateTime = 0;
		return !string.IsNullOrEmpty(AnimName) && !string.IsNullOrEmpty(AnimEvent)
			&& PlaySkillAnimationNamed(AnimName, Mathf.Max(0.01f, ChargeReleaseAnimSpeedScale)).Length > 0;
	}

	/// <summary>停掉技能动画（技能结束/取消时调用，把控制权交还"按移速切"）。</summary>
	protected void StopSkillAnimation()
	{
		if (!IsBackground && _playedAnimator != null && IsInstanceValid(_playedAnimator)
			&& _animationPlaybackId != 0 && _playedAnimator.SkillPlaybackId == _animationPlaybackId)
		{
			_playedAnimator.StopSkillAnimation();
		}
		_animationPlaybackId = 0;
	}

	/// <summary>
	/// **播一段"不是本行 <see cref="AnimName"/>"的技能动画**（P4 新增，**只加不删**）。
	///
	/// <para>存在的理由：角色 B 的三把近战武器，**武技1 的每一段连招都是一段独立的技能动画**
	/// （见 `AbilityConfig.ComboSeries`）—— "一段连招 = 一段动画"才是文档说的
	/// 「武技1 基本是动画驱动碰撞和间隔的」。基类原来的 <see cref="PlaySkillAnimation"/> 只认
	/// <see cref="AnimName"/> 这一个名字，所以这里补一个"按名播"的入口。</para>
	///
	/// <para>⚠️ 它只是把 <c>UnitAnimator.PlaySkillAnimation</c> 暴露出来（同一个函数、同一条
	/// `speed_scale` 语义），**没有改动任何既有路径**：不调它的技能行为与改动前逐位一致。</para>
	/// </summary>
	/// <param name="animationName">技能动画名（表里的 <c>AnimName</c> 形态，不带库名也行）。</param>
	/// <param name="speedScale">&gt;0 时覆盖本行的 <see cref="AnimSpeedScale"/>。</param>
	/// <returns>真正播出去的动画全名；空 = 没播成。</returns>
	protected string PlaySkillAnimationNamed(string animationName, float speedScale = 0f)
	{
		if (IsBackground || string.IsNullOrEmpty(animationName))
		{
			return string.Empty;
		}

		float scale = speedScale > 0f ? speedScale : AnimSpeedScale;
		_playedAnimator = FindInOwner<UnitAnimator>();
		string played = _playedAnimator?.PlaySkillAnimation(animationName, scale) ?? string.Empty;
		_animationPlaybackId = played.Length > 0 ? _playedAnimator!.SkillPlaybackId : 0;
		return played;
	}

	/// <summary>
	/// **这些动画名里有没有至少一个存在**（P4 新增，**只加不删**）。
	///
	/// <para>给"一段连招里每段是一段独立动画"的技能用：<see cref="TryActivate"/> 原来只校验
	/// <see cref="AnimName"/> 一个名字，而连招的每一段名字不同。它只读不写，
	/// **没有连招行（<c>AbilityConfig.ComboSeries</c>）的技能永远不会走到这里** → 默认行为逐位不变。</para>
	///
	/// <para>⚠️ 目前**没有调用点**（P8 照实记）：连招的每一段名字由连招行保证，
	/// 那一段的存在性由 <c>UnitAnimator.ResolveSkillAnimation</c> 在播放时查。留着它是"只加不删"的纪律，
	/// 不是"有一条激活路径在用它"。</para>
	/// </summary>
	protected bool AnyAnimationAvailable(string[] animationNames)
	{
		UnitAnimator? animator = FindInOwner<UnitAnimator>();
		if (animator == null)
		{
			return false;
		}

		animator.PrepareSkillLibrary();
		foreach (string name in animationNames)
		{
			if (!string.IsNullOrEmpty(name) && animator.ResolveSkillAnimation(name).Length > 0)
			{
				return true;
			}
		}

		return false;
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
		IsActive = false;
		IsCharging = false;
		_maintainHeld = false;
		StateTime = 0f;
		if (MaxCharges > 1)
		{
			// 有层数的技能：回层计时从"用掉那一层"就开始跑了（见 ConsumeCharge），这里只兜底。
			EnsureRechargeRunning();
		}
		else
		{
			// ⚠️ 没配 CDR 时 EffectiveCooldown == Cooldown（×1），与改动前逐位一致。
			CooldownRemaining = Mathf.Max(0, EffectiveCooldown * _finishCooldownScale - _finishCooldownSubtract);
		}
		_finishCooldownScale = 1;
		_finishCooldownSubtract = 0;

		StopSkillAnimation();
		OnFinishOrCancel();
	}

	/// <summary>换装前释放本能力持有的状态，保留正常技能的取消生命周期。</summary>
	public void ResetForLoadout()
	{
		UpdateInputPreview(false);
		if (IsActive) Cancel();
		if (_chargeCooldownBaseline.HasValue) CooldownMultiplier = _chargeCooldownBaseline.Value;
		_chargeCooldownBaseline = null;
		AlwaysPerfectRelease = AutoReleaseOnFullCharge = false;
		_chargeBaseOverride = _chargeStepOverride = -1f; _chargeStepTimeScale = 1f;
		OnLoadoutReset();
		ResetChargeState();
		RefreshCooldown();
	}
	protected virtual void OnLoadoutReset() { }
	/// <summary>明确的硬控制可终止动作；霸体在单位受控入口判断，收尾与普通技能交接一致。</summary>
	public void InterruptByControl() { if (IsBusy) Finish(); }

	protected void Cancel()
	{
		IsActive = false;
		IsCharging = false;
		_finishCooldownScale = 1;
		_finishCooldownSubtract = 0;
		_maintainHeld = false;
		StateTime = 0f;
		StopSkillAnimation();
		OnFinishOrCancel();
	}

	/// <summary>
	/// **收招钩子**（P4 新增，**只加不删**）：<see cref="Finish"/> / <see cref="Cancel"/> 都会调它。
	///
	/// <para>存在的理由：多段连招（<c>MeleeComboAbility</c>）要保证"收招时命中盒一定是关的、
	/// 命中盒形状还原回场景基线"。原来子类只能在 <see cref="Tick"/> 里自己判 <c>IsBusy</c>，
	/// 而 <see cref="Cancel"/> 那条路（动画事件帧没来、被别的技能打断）会**绕过**那段收尾 ——
	/// 于是命中盒会留在"上一段的尺寸"上（那正是"多段连招占位盒"最容易出的错）。</para>
	///
	/// <para>⚠️ 默认实现为空 → **所有既有子类的行为逐位不变**（它们一个字都没改）。</para>
	/// </summary>
	protected virtual void OnFinishOrCancel()
	{
	}

	/// <summary>维持类效果松手时的收尾；可覆写为独立的淡出，不占用角色动作。</summary>
	protected virtual void OnMaintainReleased() => Finish();

	/// <summary>在拥有者子树里找组件（武器、命中体等），避免依赖节点名。</summary>
	protected T? FindInOwner<T>() where T : class
	{
		EnsureInitialized();
		return OwnerUnit == null ? null : NodeSearch.FindInSubtree<T>(OwnerUnit);
	}
}

