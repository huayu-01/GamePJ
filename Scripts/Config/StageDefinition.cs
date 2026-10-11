using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 一个 Stage 的配置（策划文档 §6.2：Stage 之间靠敌人强度与进度区分）。
/// 每一关复用同一个 Stage 场景，只有这里的数值不同，代码不认具体关卡。
/// </summary>
[GlobalClass]
public partial class StageDefinition : Resource
{
	[Export] public string DisplayName { get; set; } = "Stage 1";

	[ExportGroup("战斗")]
	[Export] public float CombatBudget { get; set; } = 10f;
	[Export] public int MaxConcurrent { get; set; } = 3;
	[Export] public float SpawnInterval { get; set; } = 2f;
	[Export] public float EnemyHealthMultiplier { get; set; } = 1f;
	[Export] public float EnemyDamageMultiplier { get; set; } = 1f;

	/// <summary>精英出现概率的额外缩放（加在各生成项自己的 EliteChance 上）。</summary>
	[Export] public float EliteChanceScale { get; set; }

	/// <summary>本关允许生成的敌人（按生成项的 DisplayName 匹配；留空 = 不限制）。</summary>
	[Export] public string[] AllowedEnemies { get; set; } = System.Array.Empty<string>();

	[ExportGroup("节点与挑战")]
	[Export] public int RewardNodeCount { get; set; } = 3;
	[Export] public int FreeChallengeQuota { get; set; } = 2;

	[ExportGroup("Boss")]
	[Export] public int BossGuardCount { get; set; } = 2;
	[Export] public float BossHealthMultiplier { get; set; } = 1f;
	[Export] public float BossDamageMultiplier { get; set; } = 1f;

	/// <summary>Boss 词条数量（策划文档 §12 Boss 构筑）。</summary>
	[Export] public int BossPerkCount { get; set; } = 2;

	/// <summary>最终 Boss（打完即通关）。</summary>
	[Export] public bool IsFinalBoss { get; set; }

	public bool Allows(string displayName)
		=> AllowedEnemies.Length == 0 || System.Array.IndexOf(AllowedEnemies, displayName) >= 0;
}
