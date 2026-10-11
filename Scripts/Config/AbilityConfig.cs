using ActRoguelike.Abilities;
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
/// <item><b>首领行为词条</b>（<see cref="ApplyHandler"/> 非空）：<b>Boss Build</b>（首领实际获得的战斗行为）。
///   由 <c>ActRoguelike.Boss.BossPerkCatalog</c> 编译成运行期词条。⚠️ 关卡核心文档 §7.1：行为型（本类）与
///   数值型（`Config/Tables/Pressure/` 的 <c>pmod_*.tres</c>）**必须能按来源分开**，不得混成一个无法区分来源的池。</item>
/// </list>
///
/// 数值权威：**这一行**。行读不到时调用方什么都不做，能力保留场景里的值。
/// </summary>
[GlobalClass]
public partial class AbilityConfig : ConfigRow
{
	[ExportGroup("寻址")]
	/// <summary>
	/// 目标节点名（`AbilityMelee` / `AbilityScream` / `AbilityDash` …）—— 单位表按它找能力节点。
	/// 首领行为词条行留空（它们不挂在某个节点上，而是改 Boss 的构筑/压力字段）。
	/// </summary>
	[Export] public string NodeName { get; set; } = string.Empty;

	/// <summary>期望的脚本类名（`DashAbility` 等）。填了就在套用时校验，不匹配只警告、不静默写坏。</summary>
	[Export] public string ClassHint { get; set; } = string.Empty;

	/// <summary>按键动作名（`dash` / `burst` / `summon` / `control`）。空 = 无按键（敌人技能）。</summary>
	[Export] public string InputAction { get; set; } = string.Empty;

	[ExportGroup("通用项（多数技能填这一组）")]
	/// <summary>释放方向来源（行里给了就用行里的；所有行都会显式给出，与场景里的值一致）。</summary>
	[Export] public Ability.AimSource Aim { get; set; } = Ability.AimSource.Facing;

	/// <summary>冷却（秒）。<b>&lt;= 0 = 不覆盖</b> —— 单位的"主攻击"由单位行的 <see cref="UnitConfig.AttackInterval"/> 说了算。</summary>
	[Export] public float Cooldown { get; set; }

	[Export] public float Windup { get; set; } = 0.3f;

	[Export] public float Recover { get; set; } = 0.3f;

	/// <summary>未解锁的能力不参与行为决策（Boss 分阶段解锁技能用）。</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>行为提示：目标进入这个距离区间时适合触发本能力。<b>&lt; 0 = 不覆盖</b>（保留场景里的值）。</summary>
	[Export] public float ApproachMinRange { get; set; } = -1f;

	[Export] public float ApproachMaxRange { get; set; } = -1f;

	[ExportGroup("首领行为词条（Boss Build；这两列只有词条行用）")]
	/// <summary>
	/// 处理器 id（<c>BossPerkCatalog</c> 里注册的字符串）。**留空 = 这是一条普通单位技能行**；
	/// 非空 = 这是一条首领行为词条，会被编译成运行期词条参与 Boss Build 抽取。
	/// </summary>
	[Export] public string ApplyHandler { get; set; } = string.Empty;

	/// <summary>词条抽中概率（只在 <see cref="ApplyHandler"/> 非空时有意义）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	/// <summary>这条行是不是"首领行为词条"（Boss Build），而不是挂在单位身上的普通技能。</summary>
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
