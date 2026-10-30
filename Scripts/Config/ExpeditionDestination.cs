using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>入口的目的地与玩家说明，避免按场景文件名猜测模式。</summary>
[Tool, GlobalClass]
public partial class ExpeditionDestination : Resource, IContentValidatable
{
	[Export] public string Title { get; set; } = "出征";
	[Export(PropertyHint.MultilineText)] public string Instructions { get; set; } = string.Empty;
	[Export] public RunModeDefinition? ModeSettings { get; set; }
	public string ResolvedTitle => ModeSettings?.Title ?? Title;
	public string ResolvedInstructions => ModeSettings?.DescribeForDeparture() ?? Instructions;
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = string.Empty;
	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(ResolvedTitle)) yield return "Title：必须填写目的地名称。";
		if (string.IsNullOrWhiteSpace(ScenePath) || !ResourceLoader.Exists(ScenePath)) yield return "ScenePath：目的地场景不存在。";
		if (ModeSettings != null) foreach (string error in ModeSettings.Validate()) yield return "ModeSettings：" + error;
	}
}
