using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class SurvivalPhase : Resource, IContentValidatable
{
	[Export] public string Title { get; set; } = "入夜";
	[Export(PropertyHint.Range, "0,1,0.01")] public float StartFraction { get; set; }
	[Export] public EnemyConfig[] Enemies { get; set; } = System.Array.Empty<EnemyConfig>();
	[Export] public float BudgetPerSecond { get; set; } = 2;
	[Export] public float MaxAlivePressure { get; set; } = 36;
	[Export] public int MaxAlive { get; set; } = 20;
	[Export] public float HealthScale { get; set; } = .7f;
	[Export] public float DamageScale { get; set; } = .8f;
	[Export] public float EliteChance { get; set; }
	public IEnumerable<string> Validate()
	{
		if (!float.IsFinite(StartFraction) || StartFraction < 0 || StartFraction >= 1) yield return "StartFraction：阶段起点必须在[0,1)。";
		if (Enemies.Length == 0) yield return "Enemies：阶段必须指定敌人。";
		foreach (var enemy in Enemies) if (enemy == null || enemy.IsGlobal || enemy.Weight <= 0 || string.IsNullOrEmpty(enemy.ScenePath) || !ResourceLoader.Exists(enemy.ScenePath) || enemy.SpawnResourceCost == int.MaxValue) yield return "Enemies：敌人引用、权重、场景或压力配置无效。";
		if (!float.IsFinite(BudgetPerSecond) || BudgetPerSecond <= 0 || !float.IsFinite(MaxAlivePressure) || MaxAlivePressure <= 0 || MaxAlive < 1) yield return "生成预算、存活压力和人数必须大于零且有限。";
		if (Enemies.Length > 0 && !System.Array.Exists(Enemies, e => e != null && e.SpawnResourceCost <= MaxAlivePressure)) yield return "MaxAlivePressure：阶段必须至少容纳一种敌人，否则无法生成。";
		if (!float.IsFinite(HealthScale) || HealthScale <= 0 || !float.IsFinite(DamageScale) || DamageScale <= 0 || !float.IsFinite(EliteChance) || EliteChance < 0 || EliteChance > 1) yield return "生命、伤害倍率必须为正，精英概率须在[0,1]。";
	}
}
