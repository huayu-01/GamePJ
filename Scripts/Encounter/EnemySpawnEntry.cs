using Godot;

namespace ActRoguelike.Encounter;

/// <summary>
/// 一条生成项：作为 Encounter 的子节点配置（预制体式，直接在场景树里编辑）。
/// 成本决定占用多少 Spawn Budget，权重决定抽中概率，精英概率决定这次生成是否强化。
/// </summary>
public partial class EnemySpawnEntry : Node
{
	[Export] public PackedScene? Scene { get; set; }

	/// <summary>本关是否允许生成（由 StageDefinition.AllowedEnemies 控制）。</summary>
	[Export] public bool Enabled { get; set; } = true;

	[Export] public string DisplayName { get; set; } = string.Empty;

	/// <summary>占用多少 Spawn Budget。</summary>
	[Export] public int Cost { get; set; } = 1;

	[Export] public float Weight { get; set; } = 1f;

	/// <summary>从 Encounter 开始后多少秒才允许出现。</summary>
	[Export] public float MinElapsedSeconds { get; set; }

	/// <summary>这次生成强化为精英的概率。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float EliteChance { get; set; }
}
