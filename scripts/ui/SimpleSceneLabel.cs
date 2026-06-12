using Godot;

public partial class SimpleSceneLabel : Control
{
    [Export] public string Title { get; set; } = "Texas Hold'em";

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var label = new Label { Text = Title, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 32);
        center.AddChild(label);
    }
}
