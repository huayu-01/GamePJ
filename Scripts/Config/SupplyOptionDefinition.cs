using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

public enum SupplyKind { Heal, Armor, Status, Experience, Shards }

/// <summary>补给内容和投放权重；临时增益直接引用统一状态定义，不复制一套计时系统。</summary>
[Tool, GlobalClass]
public partial class SupplyOptionDefinition : Resource, IContentValidatable
{
	[Export] public SupplyKind Kind { get; set; }
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string NameKey { get; set; } = "";
	[Export] public float Amount { get; set; }
	[Export] public float Duration { get; set; }
	[Export] public float AdventureWeight { get; set; } = 1;
	[Export] public float SurvivalWeight { get; set; } = 1;
	[Export] public EffectConfig? Status { get; set; }
	[Export] public Color Color { get; set; } = Colors.Gold;
	public string LocalizedName => ContentText.Get(NameKey, DisplayName);
	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(DisplayName) || !float.IsFinite(Amount) || Amount < 0 || !float.IsFinite(Duration) || Duration < 0
			|| !float.IsFinite(AdventureWeight) || AdventureWeight < 0 || !float.IsFinite(SurvivalWeight) || SurvivalWeight < 0) yield return "补给名称、数值与权重必须有效。";
		if (Kind == SupplyKind.Status && Status == null) yield return "状态补给必须引用状态资源。";
		if (Kind == SupplyKind.Experience && AdventureWeight > 0) yield return "经验补给不能投入普通冒险。";
	}
}
