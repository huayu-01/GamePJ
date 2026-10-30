using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class SurvivalDefinition : RunModeDefinition
{
	[Export] public float DurationSeconds { get; set; } = 720;
	[Export] public SceneRuneDefinition[] SceneRunePool { get; set; } = System.Array.Empty<SceneRuneDefinition>();
	[Export] public SceneRuneDefinition? FixedSceneRune { get; set; }
	[Export] public SurvivalPhase[] Phases { get; set; } = System.Array.Empty<SurvivalPhase>();
	[Export] public BossConfig? FinalBoss { get; set; }
	[Export] public EnemyConfig? MilestoneEnemy { get; set; }
	[Export] public float[] MilestoneFractions { get; set; } = new[] { .25f, .5f, .75f };
	[Export] public float SpawnMinDistance { get; set; } = 15;
	[Export] public float SpawnMaxDistance { get; set; } = 25;
	[Export] public float SpawnWarningSeconds { get; set; } = .75f;
	[Export] public float SpawnInterval { get; set; } = .45f;
	[Export] public float ExperiencePerPressure { get; set; } = 2;
	[Export] public int BaseLevelExperience { get; set; } = 14;
	[Export] public int LevelExperienceStep { get; set; } = 7;
	[Export] public int MaxExperienceMotes { get; set; } = 384;
	[Export] public float PickupRadius { get; set; } = 4;
	[Export] public int EliteKillShardDrop { get; set; } = 10;
	[Export] public int BossKillShardDrop { get; set; } = 50;
	[Export] public float ShardPickupRadius { get; set; } = 1.4f;
	[Export] public PackedScene? ShardPickupScene { get; set; }
	[Export] public float BeaconCaptureSeconds { get; set; } = 8;
	[Export] public float BeaconRadius { get; set; } = 5;
	[Export] public float BossHealthScale { get; set; } = .8f;
	[Export] public float CompletedBeaconsBossScale { get; set; } = .7f;
	[Export] public Vector3[] BeaconPositions { get; set; } = new[] { new Vector3(-25, 0, -22), new Vector3(25, 0, -17), new Vector3(0, 0, 28) };
	public int ExperienceForLevel(int level) => BaseLevelExperience + Mathf.Max(0, level - 1) * LevelExperienceStep;
	public override string DescribeForDeparture() => $"生存阶段约 {DurationSeconds / 60:0.#} 分钟 → 终局首领 → 互动撤离。\n击杀拾取经验，升级暂停三选一；保留当前武技、Q／E技能和手动瞄准。\n三处信标是可选目标：范围内守候 {BeaconCaptureSeconds:0.#} 秒取得收益，全部完成后削弱首领。\n增援有落点预告；暂停与奖励选择不推进计时。Esc可取消本局并回到出击前。";
	public override IEnumerable<string> Validate()
	{
		if (EliteKillShardDrop < 0 || BossKillShardDrop < 0 || !float.IsFinite(ShardPickupRadius) || ShardPickupRadius <= 0)
			yield return "碎片掉落不能为负，拾取范围必须为有限正数。";
		var sceneRuneIds = new HashSet<string>();
		foreach (var rune in SceneRunePool)
		{
			if (rune == null || !sceneRuneIds.Add(rune.Id)) { yield return "SceneRunePool：不能包含空项或重复场景符文。"; continue; }
			foreach (string error in rune.Validate()) yield return "SceneRunePool：" + error;
		}
		if (FixedSceneRune != null) foreach (string error in FixedSceneRune.Validate()) yield return "FixedSceneRune：" + error;
		if (string.IsNullOrWhiteSpace(Id)) yield return "Id：必须填写模式的稳定Id。";
		if (string.IsNullOrWhiteSpace(Title) || !float.IsFinite(DurationSeconds) || DurationSeconds <= 0) yield return "Title / DurationSeconds：名称不能为空，时长必须为有限正数。";
		if (Phases.Length == 0 || Phases[0] == null || Phases[0].StartFraction != 0) yield return "Phases：必须从0开始。";
		float previous = -1;
		foreach (var phase in Phases)
		{
			if (phase == null) { yield return "Phases：存在空阶段。"; continue; }
			if (phase.StartFraction <= previous) yield return "Phases：阶段必须严格递增。";
			previous = phase.StartFraction;
			foreach (var error in phase.Validate()) yield return error;
		}
		if (FinalBoss == null || !ResourceLoader.Exists(FinalBoss.ScenePath)) yield return "FinalBoss：必须指定有效首领。";
		if (MilestoneEnemy == null || MilestoneEnemy.IsGlobal || !ResourceLoader.Exists(MilestoneEnemy.ScenePath)) yield return "MilestoneEnemy：必须指定有效强化敌人。";
		previous = 0;
		foreach (float time in MilestoneFractions)
		{
			if (!float.IsFinite(time) || time <= previous || time >= 1) yield return "MilestoneFractions：时间点必须在(0,1)且严格递增。";
			previous = time;
		}
		foreach (var position in BeaconPositions) if (!position.IsFinite()) yield return "BeaconPositions：信标坐标必须为有限值。";
		foreach (float value in new[] { SpawnMinDistance, SpawnMaxDistance, SpawnWarningSeconds, SpawnInterval, ExperiencePerPressure, PickupRadius, BeaconCaptureSeconds, BeaconRadius, BossHealthScale, CompletedBeaconsBossScale }) if (!float.IsFinite(value) || value <= 0) yield return "生存参数必须为有限正数。";
		if (SpawnMaxDistance < SpawnMinDistance || BaseLevelExperience < 1 || LevelExperienceStep < 0 || MaxExperienceMotes < 1 || BeaconPositions.Length != 3) yield return "生成距离、经验门槛、拾取物上限或三处信标配置无效。";
	}
}
