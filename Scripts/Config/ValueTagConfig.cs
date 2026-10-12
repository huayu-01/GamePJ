using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **价值标签表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/ValueTags/tag_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.ValueTags"/>。
///
/// <b>它解决什么</b>：节点开几波以前写死在代码里（`Layer` 的开关：高 3 / 中 2 / 低 1），
/// 加一种奖励就得回去改那三个数字。现在改成**按价值标签驱动**：
/// 节点行（<see cref="NodeConfig.ValueTags"/>）挂标签 → 标签行在这里定波数。
/// 后期加彩蛋 / 新奖励时，只需要给新节点挂一个已存在的标签（或加一行标签），**不用碰任何代码**。
///
/// <b>取值链</b>（只有一条，见 <c>NodePlacement.WavesFor</c>）：
/// ① 节点行的 <see cref="NodeConfig.ValueTags"/> 里有标签、且那张标签行读得到 → 用
/// <see cref="WaveCount"/>（多个标签命中时取**最大**的，标签是"这个节点对得起多少波"的下限声明）；
/// ② 节点行自己的 <see cref="NodeConfig.WaveCount"/>（老字段，保留作第二个真源之外的兜底）；
/// ③ 按价值层的原开关（高 4 / 中 3 / 低 1）—— 整张标签表读不到时的回退，保证回退路径还能玩。
/// </summary>
[Tool]
[GlobalClass]
public partial class ValueTagConfig : ConfigRow
{
	[ExportGroup("波数（这个价值标签的节点开几波）")]
	/// <summary>
	/// 挂了这个标签的节点开几波（用户需求：高价值 4 波 / 中价值 3 波 / 低价值 1 波）。
	///
	/// <b>留 0 = 这个标签不表态</b>，落到下一个真源（节点行自己的 <c>WaveCount</c> →
	/// 按价值层的代码兜底）。这样以后加一个"只用来做筛选、不管波数"的标签也不会误伤。
	/// </summary>
	[Export] public int WaveCount { get; set; } = 1;

	[ExportGroup("周身光圈颜色（价值程度的表现，2026-10 新增）")]
	/// <summary>
	/// 挂了这个价值标签的节点，脚下那圈**周身光圈**用什么颜色。
	///
	/// <b>需求原话</b>：「后面其实是考虑加入一些周身特效来做信息展示的，比如光圈，光圈颜色来表示价值程度」。
	///
	/// <b>为什么颜色放在这一行</b>：光圈要表达的就是"**价值程度**"，而"一个节点算几档"这件事的
	/// **唯一真源**就是这张表（节点行 <see cref="NodeConfig.ValueTags"/> → 本表 → <see cref="WaveCount"/>）。
	/// 颜色与波数是同一个语义（价值档位）的两种表现，放同一行 → **改颜色不用碰代码，也不用碰场景**
	/// （项目纪律「数值 / 表现进配置表」）。
	///
	/// <b>谁在读</b>：`RewardNode.ApplyAuraColor()` —— 取值链与 <c>NodePlacement.WavesFor</c> **完全一致**：
	/// 节点行命中的标签里取 <see cref="WaveCount"/> **最大**的那一行（多个标签 = "这份奖励至少值几档"）。
	/// 一个标签都没命中 / 行读不到 → 用中性白兜底（并在日志里说一声，不静默）。
	///
	/// <b>挑颜色的两条硬约束</b>（用户要求"在亮地板与暗天空都看得清"）：
	/// ① 光圈材质是 **Unshaded**（不参与光照）→ 颜色在亮 / 暗两种背景下**一致**，不会有一半发黑；
	/// ② 只有**色相**能区分三档（低=绿 / 中=蓝 / 高=金），所以三行务必**不同色相**，不要只改明度。
	/// 另外光圈外面还有一圈固定的**暗描边**（`RewardNode.tscn` 的 `Aura/Rim`），
	/// 就是准星那套"亮色 + 黑描边"的思路：亮色负责暗背景，描边负责白地板。
	///
	/// 默认值（不填）是中性的淡蓝白：**故意**选一个"看起来就像没配"的颜色，免得被当成配好的。
	/// </summary>
	[Export] public Color AuraColor { get; set; } = new Color(0.85f, 0.9f, 1.0f);
}
