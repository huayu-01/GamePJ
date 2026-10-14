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
	/// 目标节点名（`AbilityMelee` / `AbilityScream` / `AbilityDash` …）—— 单位表按它找能力节点。
	/// 首领行为技能行留空（它们不挂在某个节点上，而是改 Boss 的构筑/压力字段）。
	/// </summary>
	[Export] public string NodeName { get; set; } = string.Empty;

	/// <summary>期望的脚本类名（`DashAbility` 等）。填了就在套用时校验，不匹配只警告、不静默写坏。</summary>
	[Export] public string ClassHint { get; set; } = string.Empty;

	/// <summary>
	/// <b>这个技能绑哪个按键动作</b>（`project.godot` 的 `[input]` 名，例如 <c>weapon_skill_1</c>）。
	///
	/// <para>⚠️ 本轮**推翻了**原来的设计决定。原注释写的是："刻意**没有** InputAction 列：按键动作名现在由
	/// <c>PlayerAbilityInput</c> 按能力**类**硬编码"—— 那条路让"加一个技能/角色/武器"必须改 C# 代码
	/// （用户硬要求：只加表行）。现在输入层（<c>PlayerAbilityInput</c>）**只读这一列**。</para>
	///
	/// <para>留空 = 这个技能不由玩家按键直接触发（敌人技能 / 被动 / 由别的技能驱动）。
	/// 填了但在 <c>project.godot</c> 里不存在的动作名，输入层会写一条警告并跳过（不静默）。</para>
	/// </summary>
	[Export] public string InputAction { get; set; } = string.Empty;

	/// <summary>按住式输入（蓄力类技能用）：按住期间每帧回调，松开时回调一次"释放"。</summary>
	[Export] public bool HoldToCharge { get; set; }

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

	/// <summary>动画速度倍率（&gt;1 加速释放，蓄力类 &lt;1 拉长）。写在每个单位自己的播放器上。</summary>
	[Export] public float AnimSpeedScale { get; set; } = 1f;

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
		ability.AnimName = AnimName;
		ability.AnimEvent = AnimEvent;
		ability.AnimSpeedScale = AnimSpeedScale <= 0f ? 1f : AnimSpeedScale;
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
