using ActRoguelike.Interaction;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 节点表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Nodes/node_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Nodes"/>。
///
/// 它一处替掉三处硬编码：`RewardService.TitleFor` / <c>FilterFor</c> 两个 switch、
/// `NodePlacement.LayerOf` 与 `StageController.WavesFor`（同一段逻辑写了两遍）、以及商店三项报价。
/// </summary>
[Tool]
[GlobalClass]
public partial class NodeConfig : ConfigRow
{
	[ExportGroup("身份 · 节点类型与奖励过滤")]
	/// <summary>节点类型（对应 <see cref="RewardNodeKind"/> 与 `Main.tscn` 上的 <c>Kind</c>）。</summary>
	[Export] public RewardNodeKind Kind { get; set; } = RewardNodeKind.Enhancement;

	/// <summary>界面标题（取代 <c>RewardService.TitleFor</c>，例如"进化池"）。</summary>
	[Export] public string Title { get; set; } = string.Empty;

	/// <summary>奖励池过滤（取代 <c>RewardService.FilterFor</c>）。<c>true</c> = 不按类型过滤、走全池。</summary>
	[Export] public bool AnyRewardKind { get; set; }

	/// <summary>本节点发哪一类奖励（强化 / 进化 / 异化；<see cref="AnyRewardKind"/> = true 时忽略它）。</summary>
	[Export] public RewardKind RewardKind { get; set; } = RewardKind.Enhancement;

	[ExportGroup("分层与波数（取代 NodePlacement.LayerOf / StageController.WavesFor）")]
	/// <summary>价值层：2 高 / 1 中 / 0 低。</summary>
	[Export] public int Layer { get; set; } = 1;

	/// <summary>这个节点开几波。</summary>
	[Export] public int WaveCount { get; set; } = 2;

	/// <summary>
	/// **价值标签**（用户需求：「节点刷怪调整，高价值节点会刷 4 波怪，中刷 3，低刷 1，对应
	/// 异化 / 升级 / 双倍强化 / 多倍资源强化 / 特殊奖励 / 双倍资源恢复 / 一倍资源；这些不写死，
	/// 假设后期有彩蛋或其他奖励也会在这些不同的价值标签里」）。
	///
	/// 元素是**价值标签表**（`Config/Tables/ValueTags/tag_*.tres`）里某一行的 <see cref="ConfigRow.Id"/>
	/// （`tag_high` / `tag_mid` / `tag_low`）。一个节点可以挂多个标签。
	///
	/// <b>为什么用标签而不是枚举</b>：波数不再按 <see cref="Kind"/> 硬编码 ——
	/// 以后加"彩蛋"这类新奖励，只要给它挂上已有标签（或新加一行标签）即可，
	/// 不必回头改 <c>NodePlacement.WavesFor</c> 里的 switch。
	///
	/// 取值链见 <see cref="ValueTagConfig"/>（标签行 → 本行 <see cref="WaveCount"/> → 按价值层的代码兜底）。
	/// 留空 = 这一行不表态，直接落到后两层。
	/// </summary>
	[Export] public string[] ValueTags { get; set; } = System.Array.Empty<string>();

	[ExportGroup("表现（节点外观；空 = 用场景自带）")]
	/// <summary>节点材质（.tres；空 = 用场景自带材质）。</summary>
	[Export(PropertyHint.File, "*.tres")] public string MaterialPath { get; set; } = string.Empty;

	/// <summary>节点图标（界面用；空 = 无图标）。</summary>
	[Export(PropertyHint.File, "*.png,*.svg")] public string IconPath { get; set; } = string.Empty;

	/// <summary>挑战这个节点会不会触发一场战斗。</summary>
	[Export] public bool SpawnsEncounter { get; set; } = true;

	[ExportGroup("商店价目（只有 Kind = Shop 的行用；单位 = 局外碎片）")]
	/// <summary>花局外碎片恢复全部生命与护甲的价格。</summary>
	[Export] public int ShopHealCost { get; set; } = 5;

	/// <summary>花局外碎片买一件随机强化奖励的价格。</summary>
	[Export] public int ShopRewardCost { get; set; } = 12;

	/// <summary>花局外碎片买一件随机进化（改攻击方式）的价格。</summary>
	[Export] public int ShopWeaponCost { get; set; } = 18;

	[ExportGroup("Boss Pressure（关卡核心文档 §7.2/§7.4/§7.5：节点驱动抽奖）")]
	/// <summary>
	/// 挑战这个节点给 Boss 加多少 **Pressure 点数**（关卡核心文档 §7.2「不同挑战类型具有各自的
	/// Pressure 增加值」）。这是**基础点数**，之后还会乘上关卡行的增长倍率与进化/异化的额外比例
	/// （组合顺序见 <c>StageController.AddPressureForNode</c>，全项目只有那一处定义）。
	///
	/// 取值优先级（只有一个真源）：本行（挑战类型最具体）→ 关卡行按价值层的
	/// <c>PressurePerNodeHighValue</c> / <c>PressurePerNodeNormal</c> → <c>Balance.PressurePerHighValueNode</c>。
	/// <b>留 0 = 本行不表态</b>，落到下一层（三行当前都填了 150，与文档 §7.2 的固定值一致）。
	/// </summary>
	[Export] public float BossPressurePoints { get; set; }

	/// <summary>
	/// 挑战这个节点**额外**给 Boss 抽几次数值强化（与阈值转换无关；配置成 0 就是不加）。
	/// 阈值转换的次数由点数与阈值算出来，这一列是"这个挑战类型的额外机会"。
	/// </summary>
	[Export] public int BossModifierRolls { get; set; }

	/// <summary>
	/// 这个节点触发的抽取允许哪些**数值型**候选标签（留空 = 不表态 → 用关卡行的
	/// <see cref="StageDefinition.PressureUpgradeTags"/>；两层都空 = 不限制）。
	/// ⚠️ 只描述数值型强化（关卡核心文档 §7.1，= 界面上的**词条**）—— 行为/技能型（**技能**）是 Boss Build 的职责，不在这里。
	/// </summary>
	[Export] public string[] BossModifierTags { get; set; } = System.Array.Empty<string>();
}
