using Godot;

namespace ActRoguelike.Config;

/// <summary>近战连招专用定义；段落参数仍复用独立AbilitySeries组件。</summary>
[Tool, GlobalClass]
public partial class ComboAbilityConfig : AbilityConfig
{
	[ExportGroup("连招")]
	[Export] public override AbilitySeries? ComboSeries { get; set; }
}
