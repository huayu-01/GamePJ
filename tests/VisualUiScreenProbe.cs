using Godot;
using System.Linq;

public partial class VisualUiScreenProbe : Control
{
    [Export] public string ScreenshotName { get; set; } = "ui_screen.png";
    [Export] public string PressButtonText { get; set; } = "";

    public override async void _Ready()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

        if (!string.IsNullOrWhiteSpace(PressButtonText))
        {
            var button = FindChildren("*", "Button", true, false)
                .OfType<Button>()
                .FirstOrDefault(item => item.Text == PressButtonText);
            if (button == null)
            {
                GD.PushError($"VISUAL_UI_PROBE button not found: {PressButtonText}");
                GetTree().Quit(1);
                return;
            }

            button.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        }

        var image = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://test_outputs"));
        var path = $"res://test_outputs/{ScreenshotName}";
        image.SavePng(path);
        GD.Print($"VISUAL_UI_PROBE screenshot={ProjectSettings.GlobalizePath(path)}");
        GetTree().Quit();
    }
}
