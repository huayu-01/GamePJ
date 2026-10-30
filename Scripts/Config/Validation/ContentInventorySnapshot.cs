using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Config;

/// <summary>制作期显式内容数量基线；不会按当前索引自动改写，新增或移除内容时明确更新。</summary>
[Tool, GlobalClass]
public partial class ContentInventorySnapshot : Resource, IContentValidatable
{
	[Export] public Godot.Collections.Dictionary<string, int> ExpectedCounts { get; set; } = new();
	[Export] public Godot.Collections.Dictionary<string, int> ExpectedRewardKinds { get; set; } = new();
	public static ContentInventorySnapshot Current => RuntimeResources.Load<ContentInventorySnapshot>("res://Config/Validation/content_inventory.tres");
	public int Count(string table) => ExpectedCounts.TryGetValue(table, out int count) ? count : throw new InvalidOperationException($"内容基线缺少 {table}。");
	public int RewardCount(Rewards.RewardKind kind) => ExpectedRewardKinds.TryGetValue(kind.ToString(), out int count) ? count : throw new InvalidOperationException($"内容基线缺少奖励类别 {kind}。");
	public IEnumerable<string> Validate()
	{
		if (ExpectedCounts.Count == 0) yield return "ExpectedCounts：必须显式声明内容数量基线。";
		foreach (var pair in ExpectedCounts) if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0) yield return "ExpectedCounts：表名不能为空，数量必须非负。";
	}
	public IReadOnlyList<ContentIssue> Check(ConfigIndexData index)
	{
		var actual = index.GetType().GetProperties().Where(p => p.PropertyType.IsArray && typeof(Resource).IsAssignableFrom(p.PropertyType.GetElementType()))
			.ToDictionary(p => p.Name, p => ((Array)p.GetValue(index)!).Length, StringComparer.Ordinal);
		actual.Add("Specializations", index.Upgrades.Count(r => r != null && r.Specialization != SpecializationKind.None));
		var issues = new List<ContentIssue>();
		foreach (string error in Validate()) Add("ExpectedCounts", error);
		foreach (var pair in actual)
			if (!ExpectedCounts.TryGetValue(pair.Key, out int expected)) Add(pair.Key, "内容表没有显式基线");
			else if (expected != pair.Value) Add(pair.Key, $"期望 {expected}，实际 {pair.Value}");
		foreach (string unknown in ExpectedCounts.Keys.Except(actual.Keys)) Add(unknown, "基线引用了不存在的内容表");
		foreach (var kind in Enum.GetValues<Rewards.RewardKind>())
			if (!ExpectedRewardKinds.TryGetValue(kind.ToString(), out int expected) || expected != index.Upgrades.Count(r => r != null && r.RewardKind == kind)) Add("RewardKinds." + kind, "奖励分类数量与显式基线不符");
		foreach (var kind in ExpectedRewardKinds.Keys) if (!Enum.TryParse<Rewards.RewardKind>(kind, out _)) Add("RewardKinds." + kind, "未知奖励分类");
		return issues;
		void Add(string field, string reason) => issues.Add(new(string.IsNullOrEmpty(ResourcePath) ? "<memory:ContentInventorySnapshot>" : ResourcePath,
			"ExpectedCounts." + field, "ConfigIndexData", reason, "核对新增／移除内容是否有意，再更新唯一基线；不要在验收时自动接受当前数量"));
	}
}
