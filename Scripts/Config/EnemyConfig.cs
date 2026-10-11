using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 敌人表的一行 —— **一个 `.tres` 文件就是一行**（`Config/Tables/Enemies/enemy_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Enemies"/>。
///
/// 分两类行（同一张表，用 <see cref="IsGlobal"/> 区分）：
/// <list type="bullet">
/// <item><b>全局倍率行</b>（<c>enemy_global</c>，<see cref="IsGlobal"/> = true）：难度总开关，
///   取代 `Balance.tres` 的 <c>EnemyHealthMultiplier</c> / <c>EnemyDamageMultiplier</c> /
///   <c>EnemyMoveSpeedMultiplier</c> / <c>EliteChanceMultiplier</c>。只能有一行。</item>
/// <item><b>按敌种的行</b>（每个敌人场景一行）：继承 <see cref="UnitConfig"/> 的通用单位参数
///   （攻击 / 生命 / 移速 / 持有技能 / 行为&amp;类 / 表现），再加这一类的私有列 ——
///   刷怪成本、权重、解锁时间、精英概率。<b>生成项按 <see cref="ConfigRow.Id"/> 匹配，
///   不再按中文显示名匹配</b>（改文案不再会静默放行/屏蔽敌人）。</item>
/// </list>
/// </summary>
[Tool]
[GlobalClass]
public partial class EnemyConfig : UnitConfig
{
	[ExportGroup("全局倍率（只有 enemy_global 这一行用；它们是难度总开关）")]
	/// <summary>这一行是"所有敌人通用的全局倍率行"（只能有一行；<c>enemy_global.tres</c>）。</summary>
	[Export] public bool IsGlobal { get; set; }

	/// <summary>所有普通敌人的生命倍率（1 = 原值；当前 0.75）。只影响普通敌人，不影响 Boss。</summary>
	[Export] public float HealthMultiplier { get; set; } = 1f;

	/// <summary>所有普通敌人的伤害倍率（**难度总开关**，1 = 原值；当前 0.3）。只影响普通敌人，不影响 Boss。</summary>
	[Export] public float DamageMultiplier { get; set; } = 1f;

	/// <summary>所有普通敌人的移速倍率（1 = 原值）。</summary>
	[Export] public float MoveSpeedMultiplier { get; set; } = 1f;

	/// <summary>精英出现概率倍率（1 = 按各生成项自己的 EliteChance；0 = 不出精英）。</summary>
	[Export] public float EliteChanceMultiplier { get; set; } = 1f;

	[ExportGroup("刷怪（按敌种的行用；与 Main.tscn 的生成项一一对应）")]
	/// <summary>敌人预制体路径。生成项与行**按这个路径配对**（同一个敌种只写一次）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = string.Empty;

	/// <summary>占用多少 Spawn Budget。</summary>
	[Export] public int Cost { get; set; } = 1;

	/// <summary>生成权重（0 = 不参与随机）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	/// <summary>开场多少秒之后才允许出现。</summary>
	[Export] public float MinElapsedSeconds { get; set; }

	/// <summary>精英化概率。</summary>
	[Export] public float EliteChance { get; set; }
}
