using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class CombatArtStyleConfig : Resource
{
	[Export] public bool Enabled { get; set; } = true;
	[Export] public bool OutlineEnabled { get; set; } = true;
	[Export] public float OutlineWidth { get; set; } = .006f;
	[Export] public Color OutlineColor { get; set; } = new("292338");
	[Export] public bool UseApprovedPalette { get; set; } = true;
}
