using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 敌人表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Enemies/enemy_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Enemies"/>。
///
/// 本阶段落地的是**全局倍率**那一行（<c>enemy_global</c>），取代 `Balance.tres` 的
/// <c>EnemyHealthMultiplier</c> / <c>EnemyDamageMultiplier</c> / <c>EnemyMoveSpeedMultiplier</c> /
/// <c>EliteChanceMultiplier</c> 四个字段。以后要按敌种调，就再加行、由生成项按 <see cref="ConfigRow.Id"/> 指过来
/// （`StageDefinition.AllowedEnemies` 的显示名匹配也会在这一步换成 Id 匹配）。
/// </summary>
[GlobalClass]
public partial class EnemyConfig : ConfigRow
{
	/// <summary>这一行的作用范围：<c>true</c> = 所有敌人通用的全局倍率行（只能有一行）。</summary>
	[Export] public bool IsGlobal { get; set; }

	[ExportGroup("数值倍率（生成时立刻套用，会回满生命）")]
	[Export] public float HealthMultiplier { get; set; } = 1f;

	[Export] public float DamageMultiplier { get; set; } = 1f;

	[Export] public float MoveSpeedMultiplier { get; set; } = 1f;

	/// <summary>精英出现概率倍率（1 = 按各节点自己的 EliteChance；0 = 不出精英）。</summary>
	[Export] public float EliteChanceMultiplier { get; set; } = 1f;

	[ExportGroup("按敌种的覆盖（可选；全局行的这些字段不用）")]
	/// <summary>敌人预制体路径（按敌种的行用）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = string.Empty;

	/// <summary>生成项显示名（对应 `Main.tscn` 上的 <c>DisplayName</c>，用于和现有生成项对接）。</summary>
	[Export] public string SpawnDisplayName { get; set; } = string.Empty;

	/// <summary>生成权重（0 = 不参与随机）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	/// <summary>最少经过多少秒才允许出现。</summary>
	[Export] public float MinElapsedSeconds { get; set; }

	/// <summary>精英化概率。</summary>
	[Export] public float EliteChance { get; set; }
}
