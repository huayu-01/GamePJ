using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ActRoguelike.Combat.Elites;

[Tool, GlobalClass]
public partial class EliteTraitCatalog : Resource
{
	[Export] public Godot.Collections.Array<EliteTraitDefinition> Traits { get; set; } = new();
	[Export] public int ExpectedCount { get; set; }
	public IEnumerable<string> Validate()
	{
		var ids = new HashSet<string>();
		if (ExpectedCount > 0 && Traits.Count != ExpectedCount) yield return $"目录数量{Traits.Count}与制作基线{ExpectedCount}不符";
		foreach (var row in Traits)
		{
			if (row == null) { yield return "目录不能含空行"; continue; }
			if (!ids.Add(row.Id)) yield return $"重复特性 {row.Id}";
			foreach (var error in row.Validate()) yield return $"{row.Id}: {error}";
		}
	}
	public EliteTraitDefinition? Pick(IReadOnlyList<Unit> prototypes)
	{
		var candidates = Traits.Where(d => d.Weight > 0 && prototypes.Any(d.Supports)).ToArray();
		float total = candidates.Sum(d => d.Weight); if (total <= 0) return null;
		float roll = GD.Randf() * total;
		foreach (var definition in candidates) { roll -= definition.Weight; if (roll <= 0) return definition; }
		return candidates.LastOrDefault();
	}
}
