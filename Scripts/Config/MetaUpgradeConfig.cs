using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 局外成长（元进度）表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Meta/meta_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.MetaUpgrades"/>。
///
/// 取代 `MetaProgress.cs` 里 5 个硬编码系数（10 / 0.05 / 0.03 / 0.03 / 0.06）与价格公式 <c>10*(level+1)</c>。
/// </summary>
[Tool]
[GlobalClass]
public partial class MetaUpgradeConfig : ConfigRow
{
	[ExportGroup("存档与效果（每级加什么）")]
	/// <summary>
	/// 存档键名。⚠️ **不能用 <see cref="ConfigRow.Id"/> 当存档键** —— `MetaProgress` 用
	/// `user://meta.cfg` 的 <c>&lt;id&gt;_level</c> 存 int 等级，改了键名老玩家进度会归零。
	/// 留空 = 用行 Id + "_level"。
	/// </summary>
	[Export] public string SaveKey { get; set; } = string.Empty;

	/// <summary>套用的属性键（与 <c>StatSheet</c> 一致）。只有 <see cref="Operation"/> = Add 时按"每级加固定值"处理。</summary>
	[Export] public string Stat { get; set; } = string.Empty;

	/// <summary>运算方式（<c>Add</c> 加法 / <c>Multiply</c> 乘法；<c>Multiply</c> 时 <see cref="PerLevel"/> 是每级的倍率增量）。</summary>
	[Export] public ModifierOperation Operation { get; set; } = ModifierOperation.Add;

	/// <summary>每升一级的效果量（例：最大生命 +10 / 伤害 +5%）。</summary>
	[Export] public float PerLevel { get; set; }

	[ExportGroup("价格与上限（碎片）")]
	/// <summary>1 级的价格（碎片）。</summary>
	[Export] public int BaseCost { get; set; } = 10;

	/// <summary>每级的涨价幅度（当前实现是线性的：<c>BaseCost + CostGrowth × level</c>）。</summary>
	[Export] public int CostGrowth { get; set; } = 10;

	/// <summary>等级上限（0 = 不限）。</summary>
	[Export] public int MaxLevel { get; set; }

	/// <summary>算第 <paramref name="level"/> 级（0 起）买下一级要多少碎片。</summary>
	public int CostOf(int level) => BaseCost + (CostGrowth * level);

	/// <summary>实际用哪个存档键（空则按行 Id 推导）。</summary>
	public string ResolvedSaveKey => string.IsNullOrEmpty(SaveKey) ? $"{Id}_level" : SaveKey;
}
