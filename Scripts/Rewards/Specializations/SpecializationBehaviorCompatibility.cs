using System;
using System.Linq;
using ActRoguelike.Config;

namespace ActRoguelike.Rewards;

/// <summary>旧行只在此转换；工厂按定义类型发现，生产行不使用字符串参数。</summary>
internal static class SpecializationBehaviorCompatibility
{
	private static readonly Lazy<System.Collections.Generic.Dictionary<SpecializationKind, Type>> Types = new(() =>
		typeof(SpecializationBehaviorDefinition).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && typeof(SpecializationBehaviorDefinition).IsAssignableFrom(t))
			.Select(t => { using var definition = (SpecializationBehaviorDefinition)Activator.CreateInstance(t)!; return (Type: t, Kind: definition.Kind); })
			.ToDictionary(p => p.Kind, p => p.Type));
	public static SpecializationBehaviorDefinition? Definition(RewardConfig row)
	{
		if (row.SpecializationBehavior != null) return row.SpecializationBehavior;
		if (!Types.Value.TryGetValue(row.Specialization, out var type)) return null;
		var definition = (SpecializationBehaviorDefinition)Activator.CreateInstance(type)!;
		foreach (var entry in row.HandlerParams)
		{
			var property = type.GetProperty(entry.Key);
			if (property == null || !property.CanWrite) throw new InvalidOperationException($"旧特化参数 {row.Id}.{entry.Key} 无对应定义字段。");
			definition.Set(entry.Key, entry.Value);
		}
		definition.Targets = row.SpecializationTargets;
		definition.ProjectileScene = row.SpecializationProjectile;
		definition.ConvertedRewards = row.ConvertedRewards;
		return definition;
	}
}
