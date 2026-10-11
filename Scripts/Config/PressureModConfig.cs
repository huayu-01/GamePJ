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
}
