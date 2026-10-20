using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **技能表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Skills/skl_*.tres`）。
/// 由单位表的 <see cref="UnitConfig.Skills"/> 以**资源引用**指过来（不是字符串 id）。
///
/// 两类行共用同一张表（用户："首领行为表本质也是一些技能"）：
/// <list type="bullet">
/// <item><b>单位技能</b>（<see cref="ApplyHandler"/> 留空）：描述"某个单位身上的某个能力节点怎么配"。
///   通用项（冷却/前摇/后摇/朝向/开关/接近距离）+ <see cref="ConfigRow.Params"/> 里的子类专属参数
///   （<c>Distance</c> / <c>Count</c> / <c>ChargeSpeed</c> / <c>TriggerRadius</c> …）。</item>
/// <item><b>首领行为技能</b>（<see cref="ApplyHandler"/> 非空）：<b>Boss Build</b>（首领实际获得的战斗行为）。
///   由 <c>ActRoguelike.Boss.BossPerkCatalog</c> 编译成运行期条目。⚠️ 关卡核心文档 §7.1：行为型（本类，
///   在界面上叫**技能**）与数值型（`Config/Tables/Pressure/` 的 <c>pmod_*.tres</c>，在界面上叫**词条**）
///   **必须能按来源分开**，不得混成一个无法区分来源的池。</item>
/// </list>
///
/// 数值权威：**这一行**。行读不到时调用方什么都不做，能力保留场景里的值。
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilityConfig : ConfigRow
{
	[ExportGroup("寻址")]
	/// <summary>
	/// 目标节点名（`AbilitySkill1` / `AbilityWeaponSkill1` / `AbilityScream` …）—— 单位表按它找能力节点。
	/// 首领行为技能行留空（它们不挂在某个节点上，而是改 Boss 的构筑/压力字段）。
	/// </summary>
	[Export] public string NodeName { get; set; } = string.Empty;

	/// <summary>期望的脚本类名（`BackdashAbility` 等）。填了就在套用时校验，不匹配只警告、不静默写坏。</summary>
	[Export] public string ClassHint { get; set; } = string.Empty;

	/// <summary>
	/// 默认安装槽（对应InputMap动作，例如weapon_skill_1）。PlayerLoadout只安装当前配装声明的技能。
	/// 实际输入只读取Unit.AbilitySlots的槽位索引，不扫描仓库中技能的此属性；可显式装入其它槽。
	/// 留空表示被动／由事件驱动。InputMap决定物理按键，技能行不保存Q或鼠标键本身。
	/// </summary>
	[Export] public string InputAction { get; set; } = string.Empty;

	[ExportGroup("技能栏（只供HUD读取，不参与战斗）")]
	[Export] public Texture2D? HudIcon { get; set; }
	[Export] public Color HudColor { get; set; } = new(0.45f, 0.88f, 0.85f);
	[Export] public int HudOrder { get; set; }

	/// <summary>按住式输入（蓄力类技能用）：按住期间每帧回调，松开时回调一次"释放"。</summary>
	[Export] public bool HoldToCharge { get; set; }
	[Export] public bool IsBackground { get; set; }
	[Export] public bool HoldToMaintain { get; set; }
	[Export] public float MaintainStartupSeconds { get; set; }
	[ExportGroup("动作打断（前摇锁定使用动画时间轴）")]
	[Export] public bool CanBeInterrupted { get; set; } = true;
	[Export] public float InterruptLockSeconds { get; set; }
	[Export] public float AnimationEventTimeout { get; set; } = 2f;
	[ExportGroup("动作移动（0锁定／1允许／2攻击由被动授权）")]
	[Export] public Ability.MovementPolicy ActionMovement { get; set; }
	/// <summary>灵动等被动是否授予移动攻击；只由AirJumpPassiveAbility消费。</summary>
	[Export] public bool GrantAttackMovement { get; set; }
	[ExportGroup("下落攻击（强类型参数）")]
	[Export] public FallAttackTuning? FallAttack { get; set; }

	[ExportGroup("连招（P4：近战武技1 的「每段一段动画」；只有 MeleeComboAbility 读它）")]
	/// <summary>
	/// **这个技能的连招参数行**（`Config/Tables/SkillSeries/*.tres` 里的一个 <see cref="AbilitySeries"/>）：
	/// 每段动画名 / 每段命中盒 / 每段伤害倍率 / 斩断子弹的名单。**留空 = 这个技能不是连招**。
	///
	/// <para>⚠️ <b>P8 改造</b>：这一列原来是 <c>[Export(PropertyHint.File,"*.tres")] string ComboSeriesPath</c>
	/// （一个**路径字符串**），套用时靠 <c>ability.Get("ComboSeriesPath")</c> / <c>Set(...)</c> **字符串反射**写进能力
	/// —— 属性名写错不报错、路径改了要满地找、`.tres` 里也看不出它指向谁。现在改成**资源引用**：
	/// 编辑器里点得到、改名/移动文件会自动跟着走、类型不对（指到一个非 <see cref="AbilitySeries"/> 的行）
	/// 在编辑器里就是红的。与单位表的 <c>Skills</c> / 武器的 <c>Pool</c> 是同一条纪律。</para>
	///
	/// <para>为什么"一串参数"要落成独立资源而不是这里的一堆数组列：见 <see cref="AbilitySeries"/> 的类注释。</para>
	/// </summary>
	[Export] public AbilitySeries? ComboSeries { get; set; }

	[ExportGroup("动画（「没有对应动画 → 技能不执行」，用户裁定）")]
	/// <summary>
	/// 这个技能要播的**技能动画名**（`Assets/Animations/skills/` 下的那一段，例如 <c>Skill_A1_ATK1</c>）。
	///
	/// 查名走全路径（<c>skills/Skill_A1_ATK1</c>），因为 <c>AnimationPlayer.HasAnimation</c> 不搜嵌套库。
	/// **留空 = 这个技能不需要动画**（纯数值被动之类）；填了却查不到 → 技能**拒绝执行**并写明确日志。
	/// </summary>
	[Export] public string AnimName { get; set; } = string.Empty;

	/// <summary>事件帧名（<c>ATK</c> / <c>Dodge</c> / <c>Release</c> …）：动画播到这一帧才释放效果。</summary>
	[Export] public string AnimEvent { get; set; } = string.Empty;

	/// <summary>
	/// **第二个事件帧名**（空 = 不用）。用在"一段动画里夹出一个区间"的技能上 ——
	/// 设计文档（角色 A 技能 1「后撤」）：「帧 <c>Dodge</c> 到 <c>Dodger</c> 期间无敌」，
	/// 所以这一列填 <c>Dodger</c>：<c>Dodge</c> 开无敌、<c>Dodger</c> 关无敌。
	/// 见 <see cref="Ability.AnimEventEnd"/>。
	/// </summary>
	[Export] public string AnimEventEnd { get; set; } = string.Empty;

	/// <summary>动画速度倍率（&gt;1 加速释放，蓄力类 &lt;1 拉长）。写在每个单位自己的播放器上。</summary>
	[Export] public float AnimSpeedScale { get; set; } = 1f;
	/// <summary>松手释放段的独立速度；不能沿用蓄力期的慢放倍率。</summary>
	[Export] public float ChargeReleaseAnimSpeedScale { get; set; } = 1f;

	[ExportGroup("冷却缩减 / 技能层数（**0 = 不覆盖**）")]
	/// <summary>
	/// **本技能的冷却倍率**（`1` = 不变、`0.8` = 冷却 -20%、`0.5` = 减半）。
	/// 与"单位级常驻 CDR"（`StatSheet` 的 <c>CooldownMultiplier</c>，洞悉 / 速攻这类增幅挂它）**相乘**。
	/// **留空 / 0 = 不覆盖**（保留场景里的 1），与 <see cref="Cooldown"/> 同一条纪律。
	/// </summary>
	[Export] public float CooldownMultiplier { get; set; }

	/// <summary>
	/// **技能层数**（设计文档第 13 行「每次使用技能消耗一层」）。**留空 / 0 = 不覆盖**（保留场景里的 1 = 单层）。
	/// 例：角色 A 技能1「后撤」的增幅「有备无患：后撤现在可叠 3 层」→ 这里填 3。
	/// </summary>
	[Export] public int MaxCharges { get; set; }

	/// <summary>每层回复间隔（秒；**≤0 = 不覆盖**，退化成 <see cref="Cooldown"/>）。文档给的参考值是 0.5s。</summary>
	[Export] public float RechargeSeconds { get; set; }

	[ExportGroup("蓄力（按住激活 → 抬起释放；数值全在这一组，代码里没有硬编码）")]
	/// <summary>
	/// 基础蓄力时长（秒）；未完成时按AllowEarlyChargeRelease决定低伤释放或取消。
	/// 只在 <see cref="HoldToCharge"/> 为 true 时有意义；0 = 按住即算蓄满。
	/// </summary>
	[Export] public float ChargeBaseSeconds { get; set; }
	/// <summary>仅指定技能启用未蓄满低伤释放；留空仍取消。</summary>
	[Export] public bool AllowEarlyChargeRelease { get; set; }
	[Export(PropertyHint.Range, "0,1,0.01")] public float EarlyReleaseDamageScale { get; set; } = 0.5f;

	/// <summary>后置蓄力：基础蓄满后每多少秒升一档（≤0 = 没有后置蓄力）。弓 0.1 / 短弩 0.2 / 长枪 0.25。</summary>
	[Export] public float ChargeStepSeconds { get; set; }

	/// <summary>每档的威力倍率（1.3 = 每档 +30%；**≤0 = 不覆盖**，保留 1 = 不增强）。</summary>
	[Export] public float ChargeStepScale { get; set; }

	/// <summary>后置蓄力最多几档（≤0 = 不限，由 <see cref="ChargeMaxSeconds"/> 封顶）。短弩 = 3（+6 支）。</summary>
	[Export] public int ChargeMaxSteps { get; set; }

	/// <summary>后置蓄力的总时长上限（秒；≤0 = 不限）。弓「最多蓄力 1 秒」→ 1。</summary>
	[Export] public float ChargeMaxSeconds { get; set; }

	/// <summary>完美时机窗口（秒；≤0 = 没有完美时机）。文档（能量掌武技2）：0.2s。</summary>
	[Export] public float PerfectWindowSeconds { get; set; }

	[ExportGroup("通用项（多数技能填这一组）")]
	/// <summary>释放方向来源（行里给了就用行里的；所有行都会显式给出，与场景里的值一致）。</summary>
	[Export] public Ability.AimSource Aim { get; set; } = Ability.AimSource.Facing;

	/// <summary>冷却（秒）。<b>&lt;= 0 = 不覆盖</b> —— 单位的"主攻击"由单位行的 <see cref="UnitConfig.AttackInterval"/> 说了算。</summary>
	[Export] public float Cooldown { get; set; }

	/// <summary>前摇（秒）：按下到判定生效之间的准备时间（这期间不产生命中）。</summary>
	[Export] public float Windup { get; set; } = 0.3f;

	/// <summary>后摇（秒）：判定结束到能再次行动之间的硬直。</summary>
	[Export] public float Recover { get; set; } = 0.3f;

	/// <summary>未解锁的能力不参与行为决策（Boss 分阶段解锁技能用）。</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>行为提示：目标进入这个距离区间时适合触发本能力。<b>&lt; 0 = 不覆盖</b>（保留场景里的值）。</summary>
	[Export] public float ApproachMinRange { get; set; } = -1f;

	/// <summary>接近区间的上界（与 <see cref="ApproachMinRange"/> 组成"舒适圈"；&lt; 0 = 不覆盖）。</summary>
	[Export] public float ApproachMaxRange { get; set; } = -1f;

	[ExportGroup("命中体参数（这个技能的命中体属性：Damage/Knockback/StatusId/AttackTags…）")]
	/// <summary>
	/// 套到**这个技能自己的命中体**上（<c>AttackHitbox</c>；没有命中体时退到武器组件，远程兵走这条）。
	/// 与 <see cref="ConfigRow.Params"/> 分开，是因为它们作用在两个不同的对象上：
	/// 能力节点（<c>Cooldown</c>/<c>TriggerRadius</c>…）与它下面的命中体（<c>Damage</c>/<c>StatusId</c>…）。
	/// 这里**逐条照写**（不像单位的攻击组那样用 0 当"不覆盖"）：列出来的就是权威值。
	/// 主攻击的伤害/击退由单位行的攻击组统一给（避免两个真源），这里填的是剩下的那些。
	/// </summary>
	[Export] public ParamEntry[] AttackParams { get; set; } = System.Array.Empty<ParamEntry>();

	[ExportGroup("首领行为技能（Boss Build；这两列只有技能行用）")]
	/// <summary>
	/// 处理器 id（<c>BossPerkCatalog</c> 里注册的字符串）。**留空 = 这是一条普通单位技能行**；
	/// 非空 = 这是一条首领行为技能（`bhv_*.tres`），会被编译成运行期条目参与 Boss Build 抽取。
	/// </summary>
	[Export] public string ApplyHandler { get; set; } = string.Empty;

	/// <summary>抽中概率（只在 <see cref="ApplyHandler"/> 非空时有意义）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	/// <summary>这条行是不是"首领行为技能"（Boss Build），而不是挂在单位身上的普通技能。</summary>
	public bool IsBossBuild => !string.IsNullOrEmpty(ApplyHandler);

	/// <summary>
	/// 把这一行套到能力节点上。走引擎自带的 <c>Set</c>，所以 <see cref="ConfigRow.Params"/> 的
	/// <c>Key</c> 直接写能力子类的属性名即可（不需要维护映射表）。
	/// </summary>
	public override void ApplyTo(Node target)
	{
		if (target is not Ability ability)
		{
			return;
		}

		if (!string.IsNullOrEmpty(ClassHint) && ability.GetType().Name != ClassHint)
		{
			GD.PushWarning($"[AbilityConfig] {Id} 期望 {ClassHint}，实际是 {ability.GetType().Name}，仍按属性名套用。");
		}

		ability.AbilityId = Id;
		ability.Aim = Aim;
		// 按键与动画：**逐条照写**（空 = 不绑按键 / 不需要动画），因为它们决定"这个技能能不能被按出来"。
		ability.InputAction = InputAction;
		ability.HoldToCharge = HoldToCharge;
		ability.IsBackground = IsBackground;
		ability.HoldToMaintain = HoldToMaintain;
		ability.MaintainStartupSeconds = MaintainStartupSeconds;
		ability.CanBeInterrupted = CanBeInterrupted;
		ability.InterruptLockSeconds = Mathf.Max(0, InterruptLockSeconds);
		ability.AnimationEventTimeout = Mathf.Max(0.01f, AnimationEventTimeout);
		ability.ActionMovement = ActionMovement;
		if (ability is AirJumpPassiveAbility agility) agility.GrantAttackMovement = GrantAttackMovement;
		ability.AnimName = AnimName;
		ability.AnimEvent = AnimEvent;
		ability.AnimEventEnd = AnimEventEnd;
		ability.AnimSpeedScale = AnimSpeedScale <= 0f ? 1f : AnimSpeedScale;
		ability.ChargeReleaseAnimSpeedScale = Mathf.Max(0.01f, ChargeReleaseAnimSpeedScale);

		// 连招参数行（P8）：**强类型赋值**。只有 `MeleeComboAbility` 有 `Series` 这一个字段，
		// 所以这里用类型判断挑出来 —— 其它能力子类没有它，跳过它不是"错配"（不写警告）。
		// ⚠️ 这里**不再走 Get()/Set() 字符串反射**：属性名写错时反射不报错、只是静默不生效
		// （旧的 `ComboSeriesPath` 就是这么一条脆弱机制，P8 删掉了）。
		if (ComboSeries != null && ability is MeleeComboAbility comboAbility)
		{
			comboAbility.Series = ComboSeries;
		}

		// 冷却缩减 / 层数 / 蓄力：**0 = 不覆盖**（保留场景值），与 Cooldown 同一条纪律。
		if (CooldownMultiplier > 0f)
		{
			ability.CooldownMultiplier = CooldownMultiplier;
		}

		if (MaxCharges > 0)
		{
			ability.MaxCharges = MaxCharges;
		}

		if (RechargeSeconds > 0f)
		{
			ability.RechargeSeconds = RechargeSeconds;
		}

		// 蓄力这几个是"行了就是权威"（行里空着 = 0 = 没有基础蓄力/没有后置蓄力），
		// 因为非蓄力技能根本不会读它们（HoldToCharge 才是开关）。
		ability.ChargeBaseSeconds = ChargeBaseSeconds;
		ability.AllowEarlyChargeRelease = AllowEarlyChargeRelease;
		ability.EarlyReleaseDamageScale = Mathf.Clamp(EarlyReleaseDamageScale, 0f, 1f);
		ability.ChargeStepSeconds = ChargeStepSeconds;
		ability.ChargeMaxSteps = ChargeMaxSteps;
		ability.ChargeMaxSeconds = ChargeMaxSeconds;
		ability.PerfectWindowSeconds = PerfectWindowSeconds;
		if (ChargeStepScale > 0f)
		{
			ability.ChargeStepScale = ChargeStepScale;
		}

		if (Cooldown > 0f)
		{
			ability.Cooldown = Cooldown;
		}

		ability.Windup = Windup;
		ability.Recover = Recover;
		ability.Enabled = Enabled;
		if (ApproachMinRange >= 0f)
		{
			ability.ApproachMinRange = ApproachMinRange;
		}

		if (ApproachMaxRange >= 0f)
		{
			ability.ApproachMaxRange = ApproachMaxRange;
		}

		foreach (ParamEntry entry in Params)
		{
			if (string.IsNullOrEmpty(entry.Key))
			{
				continue;
			}

			// 属性不存在时 Get() 返回 Nil —— 用它判定，避免静默写进一个无效属性。
			if (ability.Get(entry.Key).VariantType == Variant.Type.Nil)
			{
				GD.PushWarning($"[AbilityConfig] {Id} 的参数 {entry.Key} 在 {ability.GetType().Name} 上不存在，已跳过。");
				continue;
			}

			ability.Set(entry.Key, entry.Value);
		}

		ApplyAttackParams(ability);
		if (ability is FallAttackAbility fall) FallAttack?.ApplyTo(fall);
	}

	/// <summary>命中体参数 → 这个技能的命中体（没有命中体时退到武器组件）。</summary>
	private void ApplyAttackParams(Ability ability)
	{
		if (AttackParams.Length == 0)
		{
			return;
		}

		Node? strike = (Node?)NodeSearch.FindInSubtree<AttackHitbox>(ability)
			?? NodeSearch.FindInSubtree<Weapons.Weapon>(ability);
		if (strike == null)
		{
			GD.PushWarning($"[AbilityConfig] {Id} 填了命中体参数，但 {ability.Name} 下面既没有命中体也没有武器组件，已跳过。");
			return;
		}

		foreach (ParamEntry entry in AttackParams)
		{
			if (string.IsNullOrEmpty(entry.Key))
			{
				continue;
			}

			if (strike.Get(entry.Key).VariantType == Variant.Type.Nil)
			{
				GD.PushWarning($"[AbilityConfig] {Id} 的命中体参数 {entry.Key} 在 {strike.GetType().Name} 上不存在，已跳过。");
				continue;
			}

			strike.Set(entry.Key, entry.Value);
		}
	}

	/// <summary>按 key 找数值参数（找不到返回 fallback）。词条处理器里读参数统一走它。</summary>
	public float Param(string key, float fallback)
	{
		foreach (ParamEntry entry in Params)
		{
			if (entry.Key == key)
			{
				return entry.AsFloat(fallback);
			}
		}

		return fallback;
	}

	/// <summary>按 key 找文本参数（节点名这类）。</summary>
	public string ParamText(string key, string fallback)
	{
		foreach (ParamEntry entry in Params)
		{
			if (entry.Key == key)
			{
				return entry.AsText(fallback);
			}
		}

		return fallback;
	}
}
