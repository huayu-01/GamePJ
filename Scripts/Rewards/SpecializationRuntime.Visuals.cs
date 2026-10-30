using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.UI;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

public partial class SpecializationRuntime
{
	private readonly Dictionary<K, EffectConfig> _buffVisuals = new();
	public IEnumerable<EffectConfig> ActiveStatusVisuals()
	{
		if (_owner.IsDead) yield break;
		foreach (var entry in _behaviors) if (entry.Value.StatusColor is { } color) yield return BuffVisual(entry.Key, color);
	}
	private EffectConfig BuffVisual(K kind, Color color)
	{
		if (_buffVisuals.TryGetValue(kind, out var visual)) return visual;
		var row = _rows[kind];
		visual = new EffectConfig { Id = "SpecializationBuff_" + kind, DisplayName = row.DisplayName,
			VisualIcon = UiIcons.ForReward(RewardCatalog.Find(row.Id)!), VisualStyle = StatusVisualStyle.Orbit,
			VisualColor = color, VisualHeight = 2.4f, VisualRadius = .8f, VisualCount = 4 };
		_buffVisuals.Add(kind, visual); return visual;
	}
}
