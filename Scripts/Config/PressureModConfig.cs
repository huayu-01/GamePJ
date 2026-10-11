using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **Pressure 数值强化池**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Pressure/pmod_*.tres`）。
/// 索引用 <see cref="BalanceConfig.PressureMods"/>。
///
/// 关卡核心文档 §7.1 的硬约束：**Boss Pressure（数值型强化）与 Boss Build（行为/技能型）必须分离**，
/// 不得合并成一个无法区分来源的强化池。所以：
/// <list type="bullet">
/// <item>数值型（生命/伤害/攻速/移速这类乘区）→ <b>本表</b>，只允许走 <see cref="StatOverride"/> 的数值操作。</item>
/// <item>行为/技能型（提前觉醒 / 召唤师 / 尖啸强化 / 冲锋强化）→ `Config/Tables/Skills/` 里
///   <see cref="AbilityConfig.ApplyHandler"/> 非空的那几行（"首领行为表本质也是一些技能"）。</item>
/// </list>
///
/// <b>§7.5 的转换池已经接线（配置表阶段 6）</b>：Pressure 每跨过一次阈值就<b>立即</b>从本表抽一条
/// （<see cref="ActRoguelike.Boss.BossPerkCatalog.RollPressure"/>，按 <see cref="Weight"/> 真加权），
/// 抽签点在 <c>StageController.AddPressureForNode</c>，套用点在 Boss 出生时。
/// 而 Boss <b>开场词条</b>那条路只从行为型里抽（<c>BossPerkCatalog.Roll</c>）——
/// 两套来源各有各的抽取点，不再合成一个池。
///
/// ⚠️ 也**没有**为 Pressure 设计"可被玩家增幅修改"的接口（§7.2 明令禁止）。
/// </summary>
[Tool]
[GlobalClass]
public partial class PressureModConfig : ConfigRow
{
	[ExportGroup("出率与候选标签（抽取点用它筛「这一条能不能被抽到」）")]
	/// <summary>
	/// 抽中概率（加权抽取；权重 0 的行不会进池，但 <see cref="WeightedPick.MinWeight"/> 会给它兜 1% 的下限，
	/// 所以想彻底排除一行请把它的 <see cref="Tags"/> 设成不被任何抽取点命中的标签，或直接下线那一行）。
	/// </summary>
	[Export] public float Weight { get; set; } = 1f;

	/// <summary>
	/// 这条强化属于哪些**数值**类别（§7.4「强化池权重或适用规则」/ §7.5「候选内容和权重由配置定义」）。
	/// 抽取点用"节点行的 <see cref="NodeConfig.BossModifierTags"/>（优先）→ 关卡行的
	/// <see cref="StageDefinition.PressureUpgradeTags"/> → 留空"决定这次允许哪些标签：
	/// 候选 = 标签有交集的那些行；两层都留空 = 不限制（本表全部行都是候选）。
	/// ⚠️ 只描述数值型强化，**绝不包含行为或技能型词条**（那是 Boss Build 的职责）。
	/// </summary>
	[Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

	[ExportGroup("数值强化内容（只允许数值操作，不允许挂行为/技能）")]
	/// <summary>这一行改哪些属性。与增幅项表用的是同一个 <see cref="StatOverride"/>（已落地并在 evo_blast 上验证）。</summary>
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();

	[ExportGroup("技能参数（用户要求：「把一些技能的参数也加入词条里」）")]
	/// <summary>
	/// 这一行要改的**技能节点名**（首领的子节点，例如 <c>AbilityScream</c>）。留空 = 本行只改单位属性
	/// （5 条"数值型参数栏"都是这一类）。
	///
	/// 关卡核心文档 §7.5 明列「生命值、伤害、攻击速度或**技能冷却**等参数」都属于 **Pressure 数值型**，
	/// 所以技能**参数**进的是本表；**行为型**（开不开这个技能 / 新增技能）仍然只走
	/// `Config/Tables/Skills/bhv_*.tres` —— 这条边界由 <c>BossPerkCatalog</c> 兜着
	/// （数值行想写 <c>Enabled</c> 会被警告并跳过）。
	///
	/// ⚠️ 填了它 = 自动带一条**限定词**"这个首领必须真的有这个技能节点"：
	/// 换个没这个技能的 Boss，它会在**首领出生时**被判为不成立并改抽别的。
	/// </summary>
	[Export] public string AbilityNode { get; set; } = string.Empty;

	/// <summary>
	/// **能力节点**上的参数倍率：<c>Key</c> = 能力属性名（<c>Cooldown</c> / <c>TriggerRadius</c> /
	/// <c>ChargeSpeed</c> …），<c>Value</c> = **相对当前值的倍率**（不是绝对赋值 ——
	/// 所以同一条抽到两次会继续叠乘：7 → 5.6 → 4.48，而不是"第二次白抽"）。
	/// </summary>
	[Export] public ParamEntry[] AbilityScales { get; set; } = System.Array.Empty<ParamEntry>();

	/// <summary>
	/// 这个技能的**命中体**上的参数倍率：<c>Key</c> = 命中体属性名（<c>Damage</c> / <c>Knockback</c> / …）。
	/// 与 <see cref="AbilityScales"/> 分开，是因为它们作用在两个不同的对象上（能力节点 vs 它的命中体）——
	/// 与技能表的 <c>Params</c> / <c>AttackParams</c> 是同一条分工。
	/// </summary>
	[Export] public ParamEntry[] AttackScales { get; set; } = System.Array.Empty<ParamEntry>();

	[ExportGroup("限定词（词条的适用条件；每次首领出生时重新判定）")]
	/// <summary>
	/// 只对哪些**首领**生效：写单位行 Id（<c>boss_final</c>），也接受场景路径兜底。空 = 不限首领。
	/// 有了它，"这条词条是给谁的技能配的"在表里是**显式**的 —— 以后加第二个首领时，
	/// 按这一列筛选就知道哪几条要跟着改。
	/// </summary>
	[Export] public string[] BossIds { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 只对哪几关生效：写关卡行 Id（<c>stg_inner</c>），也接受关号字符串（<c>"2"</c>）。空 = 不限关卡。
	///
	/// ⚠️ <b>判定时机 = 首领出生时</b>（不是抽签时）：抽签只筛 <see cref="Tags"/> 那层候选，
	/// 限定词一律留到出生那一刻重新判 —— 判不成立就**换抽**另一条
	/// （见 <c>BossPerkCatalog.ApplyWithRefresh</c>）。所以"同一局里先抽到、后出生"不会复用抽签时的结论。
	/// </summary>
	[Export] public string[] StageIds { get; set; } = System.Array.Empty<string>();
}
