using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **多段连招（近战武技1 的"统一驱动"）** —— 角色 B［武器大师］三把近战武器的武技1 **共用这一个类**。
///
/// <para>设计文档第 98 行的架构注（**权威**）：</para>
/// <code>
/// 角色B[武器大师](注:武器武技1基本是动画驱动碰撞和间隔的,这里的cd只是参考值,
///                几个武器的武技1应当是使用同一个技能或状态驱动,因为都是由动画控制);
/// </code>
///
/// <para><b>落地方式 = 一个能力 + 一段连招表</b>（不是三个技能类）：</para>
/// <list type="bullet">
/// <item>灵剑「破剑式」：5 段（<c>ComboAnimations</c> 5 个名字）；</item>
/// <item>镰刀「憎恶」：3 段；</item>
/// <item>长枪「横扫千军」：5 段（挥砍→上挑→下劈→突刺→挥砍，靠**动画链**循环；表的 <c>ComboLoop</c>
///   只是"打满之后回到第 0 段"这一条）。</item>
/// </list>
/// <para>差别**全在表里**（`Config/Tables/Skills/skl_b*_w*_1.tres` 的列）：每段的动画名、每段的
/// 命中盒尺寸/前伸/伤害倍率、输入窗口长度、事件帧名。代码里没有"灵剑/镰刀/长枪"任何一个分支，
/// 也没有任何 0.3/0.5/0.6 这类文档里的 cd 数字 —— 文档那句话本身就说「这里的 cd 只是参考值」。</para>
///
/// <para><b>为什么不是三个类</b>：① 文档明确要求"同一个技能或状态驱动"；② 三把武器的连招**只差
/// 段数与每段的数值**，而这两样都是表列；③ 三个类会让"给某把武器加一段"变成改 C#（违反
/// `docs/character-redesign.md` §1.5「加武器/技能不改代码」）。</para>
///
/// <para><b>一次激活 = 打一段 + 开一个输入窗口</b>：
/// <code>
/// 按下 → 播 ComboAnimations[0] → 事件帧 ComboNextEvent(默认 ATK) → 命中盒生效（斩断名单里的子弹）
///      → 开输入窗口 ComboWindowSeconds
///          ├─ 窗口内再按 → 段+1 → 播 ComboAnimations[1] → …（连到表尾或输入断了为止）
///          └─ 窗口内没按 → Finish()（进入冷却）
/// </code>
/// 事件帧 <c>AnimEventEnd</c>（表列）是"窗口关"的动画标记；真到点时由 <see cref="Tick"/> 的
/// <c>ComboWindowSeconds</c> 兜底 —— 两条路都指向同一个状态，不依赖引擎回调时序。</para>
///
/// <para>⚠️ <b>默认行为不变</b>：没配连招行（<see cref="Series"/> 为空）时，本类**退化成
/// "一段"**：<see cref="AnimName"/> 一个名字、<see cref="AnimEvent"/> 一个事件帧、
/// <see cref="StrikeActiveTime"/> 一个判定窗口 —— 与既有 <c>MeleeAttackAbility</c> 的时间轴同形。</para>
/// </summary>
public partial class MeleeComboAbility : Ability
{
	[ExportGroup("连招表（**每一段一段动画**：武技1 的三把武器共用这一组列）")]
	/// <summary>
	/// **这个技能的连招参数行**（表列 <c>AbilityConfig.ComboSeries</c>，类型 <see cref="AbilitySeries"/>）：
	/// 每段动画名 / 每段命中盒 / 每段伤害倍率 / 斩断子弹的名单 + 窗口时长 / 循环 / 事件帧名。
	///
	/// <para><b>空 = 单段模式</b>：用技能行的 <see cref="Ability.AnimName"/>、
	/// <see cref="StrikeActiveTime"/> 与场景里那个命中盒 —— 行为与既有近战同形（默认路径逐位不变）。</para>
	///
	/// <para>⚠️ <b>P8 改造</b>：这里原来是 7 个各指一行 <see cref="AbilitySeries"/> 的 <c>[Export]</c> 字段
	/// （<c>ComboAnimations</c> / <c>StepDamageScales</c> / … —— 两两组合成"半配"状态，实测**一个都没被填过**），
	/// 外加一个 <c>ComboSeriesPath</c> 字符串路径。现在只剩**一个**引用：一行连招行就是全部数据，
	/// 由技能行以资源引用指过来（`Config/Tables/SkillSeries/ser_*.tres`）。</para>
	/// </summary>
	[Export] public AbilitySeries? Series { get; set; }

	/// <summary>
	/// **自测用**：不等真实动画事件帧，直接**模拟"事件帧到了"**（= 开窗口 + 本段判定 + 斩断子弹）。
	///
	/// <para>为什么需要它：旧自测是在**同一帧里**连续驱动能力的（手动推进物理时间），
	/// 而 <c>OnAnimEvent</c> 要等 <c>UnitAnimator</c> 真的把播放位置推过事件帧 —— 同一帧里推不出来。
	/// 所以自测走这个入口做"连招推进"的取证；真人那一路仍然完全由动画事件帧驱动
	/// （<see cref="OnAnimEvent"/> 里那一段），本方法不参与任何正式流程。</para>
	/// </summary>
	public void DebugFireAnimEvent()
	{
		if (IsBusy)
		{
			OnAnimEvent(ComboNextEvent);
		}
	}

	/// <summary>
	/// **开输入窗口的那个事件帧名**（默认 <c>ATK</c>）。文档：武技1「**连续按键可连续释放**」
	/// —— 这一帧就是"可以接着按"的那一瞬间。连招行（<see cref="Series"/>）里填了就用行里的。
	/// 留空 = 不开窗口（只能一段，退化成单段连招）。
	/// </summary>
	[Export] public string ComboNextEvent { get; set; } = string.Empty;

	/// <summary>
	/// **输入窗口时长**（秒）。窗口没人按就收招进冷却。
	/// 连招行（<see cref="Series"/>）里给了 &gt;0 的值就用行里的，否则用这里的场景值。
	/// </summary>
	[Export] public float ComboWindowSeconds { get; set; } = 0.35f;

	/// <summary>
	/// 打满最后一段之后**是否回到第 0 段**（长枪「横扫千军：挥砍&gt;上挑&gt;下劈&gt;突刺&gt;挥砍，这么循环」）。
	/// 灵剑/镰刀是 false（连招打完就结束）。连招行里有这一列（bool：填了就是权威）。
	/// </summary>
	[Export] public bool ComboLoop { get; set; }

	[ExportGroup("判定与斩断子弹（每段的数值都在连招行里；这里只留单段模式与半径）")]
	/// <summary>一次判定的持续时间（秒；单段模式用它；连招模式用行里的 <c>StepActiveTimes</c>）。</summary>
	[Export] public float StrikeActiveTime { get; set; } = 0.12f;

	/// <summary>斩断的搜索半径（米）—— 以命中盒中心为球心。0 = 关掉斩断。</summary>
	[Export] public float CutRadius { get; set; } = 2.4f;

	[ExportGroup("运行期可读状态（自测 / UI / 剑气取尺寸）")]
	/// <summary>当前/最后一次打到的段号（0 起）。</summary>
	public int ComboStep { get; private set; }

	/// <summary>已经连着打出几段（自测取证：证明"连续按键推进了段数"）。</summary>
	public int ComboChained { get; private set; }

	/// <summary>
	/// **连招一共几段**（= 连招行里 <c>ComboAnimations</c> 的项数；0 = 单段模式 / 还没读表）。
	/// 自测与 UI 读它 —— 数组在行里（<see cref="AbilitySeries"/>），所以这里暴露一个数。
	/// </summary>
	public int ComboStepCount => _seriesRead ? _anims.Count : 0;

	/// <summary>当前段的命中盒尺寸（剑气"大小等比攻击盒大小"读它）。</summary>
	public Vector3 CurrentStepBoxSize { get; private set; }
	/// <summary>真实判定次数；表现层据此播放特效，不驱动战斗。</summary>
	public int StrikeCount { get; private set; }

	/// <summary>本段斩断了几个子弹（自测取证）。</summary>
	public int LastCutCount { get; private set; }

	/// <summary>累计斩断的子弹数（自测取证）。</summary>
	public int TotalCutCount { get; private set; }

	/// <summary>输入窗口现在开着吗（UI：连招提示）。</summary>
	public bool ComboWindowOpen { get; private set; }

	/// <summary>
	/// **外部封锁**（P4：镰刀/长枪的**武器抛出**期间"武技1 无法使用"，文档第 135 行）。
	///
	/// <para>由 <c>WeaponThrowAbility</c> 的状态变化驱动（那条连线放在场景里的
	/// <c>WeaponGate</c> 节点上，见 `docs/abilities.md` §8）—— 本类只知道"有人把我锁了"，
	/// 不知道是谁锁的、为什么锁（所以它仍是一个**通用**连招能力）。</para>
	///
	/// <para>默认 false → 行为与没有这条线时逐位一致。</para>
	/// </summary>
	public bool ExternallyBlocked { get; private set; }

	/// <summary>外部封锁的开关（武器飞出去 → true；收回 → false）。</summary>
	public void SetExternallyBlocked(bool blocked) => ExternallyBlocked = blocked;

	/// <summary>
	/// **近战攻击力乘区**（P5）：`StatSheet` 的 <c>DamageMultiplier</c>（强化「力量」、异化「大刀阔斧」都挂它）。
	/// 没配置时是 1（`Unit.SeedStats` 给了基础值），所以这一乘**逐位不变**；读到 0 也按 1 算
	/// （`architecture.md` 第 8 条：少一个基础值不能变成"打不出伤害"）。
	/// </summary>
	public float MeleeDamageMultiplier
	{
		get
		{
			float value = OwnerUnit?.Stats.Get("DamageMultiplier") ?? 1f;
			return value > 0f ? value : 1f;
		}
	}

	/// <summary>
	/// **近战攻击范围乘区**（P5）：`StatSheet` 的 <c>AttackRangeMultiplier</c>
	/// （异化「大刀阔斧」：范围 +30% / +70% / +150%）。乘在**表里那串命中盒几何量**上（见 <see cref="ApplyStepShape"/>），
	/// 默认 1 → 逐位不变。
	/// </summary>
	public float MeleeRangeMultiplier
	{
		get
		{
			float value = OwnerUnit?.Stats.Get("AttackRangeMultiplier") ?? 1f;
			return value > 0f ? value : 1f;
		}
	}

	private AttackHitbox? _hitbox;
	private CollisionShape3D? _hitboxShape;
	private BoxShape3D? _baseBox;
	private float _sceneHitboxZ = float.NaN;

	/// <summary>场景里那个盒子的基线（每一步套完尺寸后由 <see cref="ApplyStepShape"/> 覆盖，收招时还原）。</summary>
	private Vector3 _sceneBoxSize = Vector3.One;

	private float _stepTimer;
	private float _activeTimer;
	private bool _striking;
	private bool _stepHasStruck;
	private bool _advanceQueued;
	private bool _lunging;
	private bool _windowTimerRunning;
	private bool _pressBuffered;

	// ── 连招行里的数组（读一次缓存起来，每帧读它会白白分配）──
	private readonly List<string> _anims = new();
	private readonly List<float> _damageScales = new();
	private readonly List<Vector3> _boxSizes = new();
	private readonly List<float> _boxOffsets = new();
	private readonly List<float> _activeTimes = new();
	private readonly List<string> _cutNames = new();
	private readonly List<string> _cutExcludes = new();
	private bool _seriesRead;

	/// <summary>
	/// 把连招行（<see cref="Series"/>）里的数组读成 List —— 每次开新施法时读一次，段与段之间的推进零开销。
	///
	/// <para>⚠️ <b>P8 改造</b>：这里原来按"技能行 Id → `res://Config/Tables/SkillSeries/&lt;AbilityId&gt;.tres`"
	/// **约定路径**去 <c>GD.Load</c>，再把这七串**文本**解析成数组。现在数据是技能行以资源引用指过来的
	/// <see cref="Series"/>（强类型、编辑器里看得见），**没有路径、没有解析**。</para>
	///
	/// <para><see cref="Series"/> 为空 = 单段模式（用技能行的 <c>AnimName</c>/<c>StrikeActiveTime</c>）。
	/// 那时写一条明确日志（"这个技能行没填连招行"是表数据的问题，不该只表现为"连招只有一段"）。</para>
	/// </summary>
	private void ReadSeries()
	{
		_anims.Clear();
		_damageScales.Clear();
		_boxSizes.Clear();
		_boxOffsets.Clear();
		_activeTimes.Clear();
		_cutNames.Clear();
		_cutExcludes.Clear();

		AbilitySeries? series = Series;
		if (series == null)
		{
			if (!string.IsNullOrEmpty(AbilityId))
			{
				GameLog.Write("Combat", $"⚠ 连招：{AbilityId} 的技能行没填 ComboSeries（连招行）→ 退化成单段模式（用 AnimName 那一段）。");
			}

			return;
		}

		_anims.AddRange(series.ComboAnimations);
		_damageScales.AddRange(series.StepDamageScales);
		_boxSizes.AddRange(series.StepBoxSizes);
		_boxOffsets.AddRange(series.StepBoxOffsets);
		_activeTimes.AddRange(series.StepActiveTimes);
		_cutNames.AddRange(series.CutSceneNames);
		_cutExcludes.AddRange(series.CutExcludeSceneNames);

		// 窗口/循环/事件帧也住在连招行里（它们本来就是"这一个连招"的参数）。
		// 窗口给 0 = 不覆盖（保留能力节点上的值）；事件帧空 = 不覆盖。
		// ⚠️ `ComboLoop` 是 bool 列：**填了就是权威**（false 就是"不循环"，没有"不覆盖"这一说）。
		if (series.ComboWindowSeconds > 0f)
		{
			ComboWindowSeconds = series.ComboWindowSeconds;
		}

		if (!string.IsNullOrEmpty(series.ComboNextEvent))
		{
			ComboNextEvent = series.ComboNextEvent;
		}

		ComboLoop = series.ComboLoop;

		_seriesRead = true;
		GameLog.Write(
			"Combat",
			$"连招表读取：{AbilityId} 行=[{series.Id}] 段动画 {_anims.Count} 个 [{string.Join(",", _anims)}]｜伤害倍率 {_damageScales.Count}｜盒子尺寸 {_boxSizes.Count}｜前伸 {_boxOffsets.Count}｜判定时长 {_activeTimes.Count}｜斩断名单 [{string.Join(",", _cutNames)}] 排除 [{string.Join(",", _cutExcludes)}]｜窗口 {ComboWindowSeconds:0.##}s 循环 {ComboLoop} 事件帧 {ComboNextEvent}");
	}

	protected override void OnReady()
	{
		_hitbox = GetNodeOrNull<AttackHitbox>("Attack");
		_hitboxShape = _hitbox?.GetNodeOrNull<CollisionShape3D>("Shape");
		_baseBox = _hitboxShape?.Shape as BoxShape3D;
		if (_baseBox != null)
		{
			_sceneBoxSize = _baseBox.Size;
		}

		if (_hitbox != null)
		{
			_sceneHitboxZ = _hitbox.Position.Z;
		}
	}

	protected override bool CanActivate(Unit? target)
	{
		// 近战连招**不需要预先锁定目标**（朝准星/朝向挥砍，命中盒自己判定）。
		// 单段模式的旧习性是"够不着不出招"，但那要求一个 target；玩家的 target 来自
		// `PlayerAbilityInput` 的最近敌人，敌人不在附近时按钮会完全没反应 —— 那对"打空气"的连招是错的。
		// 另：**武器被抛出去时**（ExternallyBlocked）武技1 不许用（文档第 135 行）。
		return OwnerUnit != null && !ExternallyBlocked;
	}

	protected override void OnActivate(Unit? target)
	{
		ComboStep = 0;
		ComboChained = 0;
		_windowTimerRunning = false;
		ComboWindowOpen = false;
		_pressBuffered = false;
		_striking = false;
		_stepTimer = 0f;
		_stepHasStruck = false;
		_advanceQueued = false;
		_activeTimer = 0f;

		// 每次**开新的一次施法**时重读一次表（很小；换武器/换行后必须重读）。
		// 段与段之间的推进不会走这里，所以连招中间零开销。
		ReadSeries();

		// 连招的每一段都是一段独立动画；单段模式退回行里的 AnimName。
		string first = ResolveStepAnimation(0);
		PlaySkillAnimationNamed(first);
		ApplyStepShape(0);
		GameLog.Write("Combat", $"连招：{AbilityId} 起手 段 1/{Mathf.Max(1, _anims.Count)} 动画「{first}」（窗口 {ComboWindowSeconds:0.##}s）");
	}

	/// <summary>第 i 段的动画名（越界时退回最后一段；空数组退回 <see cref="Ability.AnimName"/>）。</summary>
	private string ResolveStepAnimation(int step)
	{
		if (_anims.Count == 0)
		{
			return AnimName;
		}

		int index = Mathf.Clamp(step, 0, _anims.Count - 1);
		return string.IsNullOrEmpty(_anims[index]) ? AnimName : _anims[index];
	}

	/// <summary>
	/// **玩家按下"武技1"时由输入层转进来**（<c>PlayerAbilityInput</c> 调的 <see cref="Ability.TryPress"/>）。
	///
	/// <para>两条路：正在连招（窗口开着或还没收招）→ 推进一段 / 缓冲这一次按键；
	/// 空闲 → 走 <see cref="Ability.TryActivate"/> 开新的一次施法。</para>
	///
	/// <para>⚠️ 它**不重置冷却**、不重复查动画：段与段之间是同一次施法（文档那句"连续按键可连续释放"）。</para>
	/// </summary>
	public override bool TryPress(Unit? target = null)
	{
		if (ExternallyBlocked)
		{
			return false;
		}

		if (!IsBusy)
		{
			return base.TryPress(target);
		}

		if (!Enabled || OwnerUnit == null)
		{
			return false;
		}

		// 提前按键只缓冲；真实ATK开窗且本段判定结束后，才能消费下一段。
		_pressBuffered = true;
		if (ComboWindowOpen)
		{
			_advanceQueued = true;
			if (!_striking) return Advance();
		}

		// 窗口既没开也没到（事件帧还没来）→ 这次按键**记在缓冲里**，等 ATK 帧到来时自动推进
		// （见 OnAnimEvent）。返回 false = "这一次没有立刻推进"，但按键不会丢。
		return false;
	}

	private bool EnableInputWindow()
	{
		if (!IsBusy || ComboWindowOpen || _anims.Count == 0)
		{
			return false;
		}

		ComboWindowOpen = true;
		_windowTimerRunning = true;
		_stepTimer = 0f;
		return true;
	}

	private bool Advance()
	{
		int next = ComboStep + 1;
		if (next >= Mathf.Max(1, _anims.Count))
		{
			if (!ComboLoop)
			{
				Finish();
				return false;
			}

			next = 0;
		}

		ComboStep = next;
		ComboChained++;
		_stepHasStruck = false;
		_advanceQueued = false;
		ComboWindowOpen = false;
		_windowTimerRunning = false;
		_pressBuffered = false;
		_stepTimer = 0f;

		string animation = ResolveStepAnimation(ComboStep);
		PlaySkillAnimationNamed(animation);
		ApplyStepShape(ComboStep);
		GameLog.Write("Combat", $"连招：{AbilityId} 推进到第 {ComboStep + 1} 段（动画「{animation}」，累计连击 {ComboChained}）");
		return true;
	}

	/// <summary>
	/// 事件帧：
	/// <list type="bullet">
	/// <item><see cref="ComboNextEvent"/>（文档：`ATK`）—— **开输入窗口 + 本段命中判定 + 斩断子弹**；</item>
	/// <item>动画收招标记不覆盖表内窗口时长，避免紧邻ATK时截断输入与判定。</item>
	/// </list>
	/// </summary>
	protected override void OnAnimEvent(string name)
	{
		if (!string.IsNullOrEmpty(ComboNextEvent) && name == ComboNextEvent)
		{
			if (_stepHasStruck) return;
			_stepHasStruck = true;
			Strike();
			EnableInputWindow();
			if (_pressBuffered)
			{
				_advanceQueued = true;
			}

			return;
		}

		// Combo是动画收招标记；连招输入窗口仍按表内ComboWindowSeconds计时。
		// 不能用紧邻ATK的收招标记把输入窗口压成数毫秒，也不能截断真实判定。
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (_striking)
		{
			_activeTimer -= delta;
			if (_activeTimer <= 0f)
			{
				_hitbox?.Deactivate();
				_striking = false;
				EndLunge();
			}
		}

		if (_advanceQueued && !_striking)
		{
			Advance();
			return;
		}

		if (!_windowTimerRunning)
		{
			_stepTimer += delta;
			// 事件帧一直没来（生成表配错）→ 超时收招并写明确日志（不做时间驱动兜底）。
			if (!HoldToCharge && _stepTimer > Windup + Recover + 2.5f)
			{
				GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 等不到动画事件帧「{ComboNextEvent}」，本次施法放弃（检查 SkillAnimations 表的事件帧名）。");
				Cancel();
			}

			return;
		}

		_stepTimer += delta;
		if (_stepTimer < ComboWindowSeconds)
		{
			return;
		}

		// 窗口超时：连招到此为止（"连续按键"断了）。
		ComboWindowOpen = false;
		_windowTimerRunning = false;
		GameLog.Write("Combat", $"连招：{AbilityId} 输入窗口超时（{ComboWindowSeconds:0.##}s 没人按），收招。累计连击 {ComboChained}");
		Finish();
	}

	/// <summary>本段判定：开命中盒（伤害按段倍率）+ 斩断名单里的子弹。</summary>
	private void Strike()
	{
		if (_hitbox == null || OwnerUnit == null)
		{
			return;
		}

		float scale = StepValue(_damageScales, ComboStep, 1f);
		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			// **攻击力乘区**（P5）：`StatSheet` 的 `DamageMultiplier` —— 强化「力量」与小刀阔斧的"+20% 伤害"
			// 都挂这个键。没配置时它是 1（`Unit.SeedStats` 有基础值），所以这一乘**逐位不变**。
			// ⚠️ 只加在连招这一路（近战武技 = 玩家专属）；`AttackHitbox` 的默认路径一个字没动 ——
			// 首领/敌人的伤害走那条，那里乘一次会改动"白板首领 315"这类硬指标。
			BaseDamage = _hitbox.Damage * scale * MeleeDamageMultiplier,
			CritChance = _hitbox.CritChance,
			CritMultiplier = _hitbox.CritMultiplier,
			Knockback = _hitbox.Knockback,
			Policy = _hitbox.Policy,
			HitInterval = _hitbox.HitInterval,
			Tags = _hitbox.AttackTags,
			AbilityKind = AbilityId,
		};

		_hitbox.Activate(OwnerUnit, attack);
		StrikeCount++;
		_striking = true;
		_activeTimer = Mathf.Max(0.02f, StepValue(_activeTimes, ComboStep, StrikeActiveTime));
		BeginLunge(_activeTimer);

		LastCutCount = CutBullets();
		TotalCutCount += LastCutCount;

		GameLog.Write(
			"Combat",
			$"连招·第 {ComboStep + 1} 段判定：{AbilityId} 伤害 {attack.BaseDamage:0.##}（×{scale:0.##}）盒子 {CurrentStepBoxSize}"
			+ $"｜斩断子弹 {LastCutCount} 个（名单 [{string.Join(",", _cutNames)}]，排除 [{string.Join(",", _cutExcludes)}] → 能量波斩不断）");
	}

	/// <summary>
	/// 斩断命中盒范围内的子弹（**包括友军的** —— 判据是来源场景名，不是敌我）。
	/// <see cref="AbilitySeries.CutSceneNames"/> 为空 → 一次都不查（性能与行为都不变）。
	/// </summary>
	private int CutBullets()
	{
		if (_cutNames.Count == 0 || CutRadius <= 0f || OwnerUnit == null)
		{
			return 0;
		}

		Vector3 center = _hitbox != null ? _hitbox.GlobalPosition : OwnerUnit.GlobalPosition;
		int total = 0;
		foreach (string sceneName in _cutNames)
		{
			total += Projectile.CutBySceneName(sceneName, center, CutRadius, _cutExcludes.ToArray());
		}

		if (total > 0)
		{
			GameLog.Write("Combat", $"斩断：{AbilityId} 命中盒中心 {center} 半径 {CutRadius:0.##}m 内清掉了 {total} 个（名单 [{string.Join(",", _cutNames)}]，排除 [{string.Join(",", _cutExcludes)}]）");
		}

		return total;
	}

	/// <summary>第 i 段的参数取值（数组不够长时用"最后一项"或兜底值）。</summary>
	private static float StepValue(List<float> values, int step, float fallback)
	{
		if (values.Count == 0)
		{
			return fallback;
		}

		int index = Mathf.Clamp(step, 0, values.Count - 1);
		return values[index];
	}

	private static Vector3 StepValue(List<Vector3> values, int step, Vector3 fallback)
	{
		if (values.Count == 0)
		{
			return fallback;
		}

		int index = Mathf.Clamp(step, 0, values.Count - 1);
		return values[index];
	}

	/// <summary>
	/// 把第 i 段的命中盒尺寸/前伸写到碰撞形状上（**是表里的几何量，不是代码里的常数**）。
	/// <see cref="AbilitySeries.StepBoxSizes"/> 的空项 = 保留场景基线；形状类型也随表切换
	/// （<c>z</c> 为 0 且 <c>x == y</c> 时用圆柱 —— 镰刀最后那一刀"大回旋"就是 360°）。
	///
	/// <para>⚠️ P5：几何量上再乘一层单位级的 <c>AttackRangeMultiplier</c> —— 异化「大刀阔斧」
	/// 就是给单位挂这个键（文档第 210-213 行：「近战攻击的攻击范围增加 30%…具体增幅哪个位置的范围
	/// 由其武器本身决定，一般是**伤害碰撞**和显示物」）。**只有这里乘**：近战武技是玩家专属，
	/// 敌人/首领的命中盒一个字节没动；没配置时是 1 → 逐位不变。</para>
	/// </summary>
	private void ApplyStepShape(int step)
	{
		if (_hitbox == null || _hitboxShape == null)
		{
			return;
		}

		Vector3 size = StepValue(_boxSizes, step, _sceneBoxSize) * MeleeRangeMultiplier;
		CurrentStepBoxSize = size;

		float offset = StepValue(_boxOffsets, step, float.NaN);
		if (!float.IsNaN(offset))
		{
			Vector3 position = _hitbox.Position;
			position.Z = -offset;
			_hitbox.Position = position;
		}

		if (size == Vector3.Zero)
		{
			return;
		}

		if (size.Z <= 0.0001f)
		{
			// 圆柱（360° 回旋）：半径取 x，高度取 y。
			if (_hitboxShape.Shape is not CylinderShape3D cylinder)
			{
				cylinder = new CylinderShape3D();
				_hitboxShape.Shape = cylinder;
			}

			cylinder.Radius = Mathf.Max(0.1f, size.X * 0.5f);
			cylinder.Height = Mathf.Max(0.1f, size.Y);
			return;
		}

		if (_hitboxShape.Shape is not BoxShape3D box)
		{
			box = new BoxShape3D();
			_hitboxShape.Shape = box;
		}

		box.Size = size;
	}

	/// <summary>收招时把命中盒还原到场景基线（不留下"上一段的尺寸"）。</summary>
	private void RestoreSceneShape()
	{
		if (_hitboxShape == null)
		{
			return;
		}

		if (_baseBox != null)
		{
			_hitboxShape.Shape = _baseBox;
		}

		if (_hitbox != null && !float.IsNaN(_sceneHitboxZ))
		{
			Vector3 position = _hitbox.Position;
			position.Z = _sceneHitboxZ;
			_hitbox.Position = position;
		}

		_hitbox?.Deactivate();
	}

	/// <summary>
	/// **近战突进**（P5 异化「剑突」：文档第 215-217 行「你的近战攻击在攻击时附带突进效果
	/// （**开启碰撞的同时移动**）」）。
	///
	/// <para>突进距离来自 <c>StatSheet.MeleeLungeAdd</c>（表里填），按本段的判定持续时长换成速度，
	/// 走 <see cref="CharacterMovement.BeginDash"/>（绕开常规加速度，与闪避/冲刺同一条路），
	/// 判定结束就 <see cref="EndLunge"/> 收回来。方向交给 <c>BeginDash</c> 自己取"当前朝向前方"
	/// （传零向量就是它文档里那条回退）。</para>
	///
	/// <para>⚠️ 没配置（<c>MeleeLungeAdd</c> ≤ 0）时**一次都不调用** —— 逐位不变。</para>
	/// </summary>
	private void BeginLunge(float activeSeconds)
	{
		float distance = OwnerUnit?.Stats.Get("MeleeLungeAdd") ?? 0f;
		if (distance <= 0f || Movement == null)
		{
			return;
		}

		float speed = distance / Mathf.Max(0.02f, activeSeconds);
		Movement.BeginDash(Vector3.Zero, speed);
		_lunging = true;
		GameLog.Write("Combat", $"剑突：{AbilityId} 第 {ComboStep + 1} 段附带突进 {distance:0.##}m（本段判定 {activeSeconds:0.##}s → 速度 {speed:0.##} m/s）");
	}

	/// <summary>突进结束（判定结束 / 技能退出时都走这里，幂等）。</summary>
	private void EndLunge()
	{
		if (!_lunging)
		{
			return;
		}

		_lunging = false;
		Movement?.EndDash();
	}

	/// <summary>把"上一段的尺寸/位置"清掉（技能结束时调用）。</summary>
	public override void _ExitTree()
	{
		EndLunge();
		_hitbox?.Deactivate();
		base._ExitTree();
	}

	/// <summary>技能结束/取消：命中盒关掉 + 形状还原（避免下一段/下一次还留着上一段的尺寸）。</summary>
	protected override void OnFinishOrCancel()
	{
		_striking = false;
		_stepHasStruck = false;
		_advanceQueued = false;
		_pressBuffered = false;
		ComboWindowOpen = false;
		_windowTimerRunning = false;
		RestoreSceneShape();
	}
}
