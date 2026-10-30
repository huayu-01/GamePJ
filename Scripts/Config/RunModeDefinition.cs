using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>模式设置为入口提供同源说明，运行状态留在模式控制器。</summary>
[Tool, GlobalClass]
public abstract partial class RunModeDefinition : Resource, IContentValidatable, INumericContentIdentity
{
	[Export] public int NumericId { get; set; }
	private string _legacyId = string.Empty;
	[Export] public string Id { get => NumericContentIdentity.Key(_legacyId, NumericId); set => _legacyId = value; }
	public override void _ValidateProperty(Godot.Collections.Dictionary property) => NumericContentIdentity.HideLegacy(property);
	[Export] public string Title { get; set; } = "试炼";
	public abstract string DescribeForDeparture();
	public abstract IEnumerable<string> Validate();
}
