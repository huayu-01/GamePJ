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
/// ⚠️ 诚实交代（当前状态）：两张表**在数据上是分开的、可分别追踪**，但本阶段 Boss 开场词条的抽取
/// 仍然是"两张表合起来抽"（与分表前的 7 条行为一致，改动前后玩法不变）。等 §7.5 的 Pressure 转换池
/// 真正接线时，抽取点按来源各抽各的即可 —— 数据侧已经就位，且开局日志会把两组的条数分开打出来。
///
/// ⚠️ 也**没有**为 Pressure 设计"可被玩家增幅修改"的接口（§7.2 明令禁止）。
/// </summary>
[Tool]
[GlobalClass]
public partial class PressureModConfig : ConfigRow
{
	/// <summary>抽中概率（0 = 不参与；加权抽取的权重下限是 1%，见 <see cref="WeightedPick"/>）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	[ExportGroup("数值强化内容（只允许数值操作，不允许挂行为/技能）")]
	/// <summary>这一行改哪些属性。与增幅项表用的是同一个 <see cref="StatOverride"/>（已落地并在 evo_blast 上验证）。</summary>
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();
}
