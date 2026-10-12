using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **价值标签表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/ValueTags/tag_*.tres`）。
/// 索引用 <see cref="BalanceConfig.ValueTags"/>。
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
}
