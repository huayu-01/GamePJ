using Godot;
using System.Linq;

public partial class VisualMainMenuKeypadProbe : MainMenu
{
    public override async void _Ready()
    {
        base._Ready();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var joinButton = FindChildren("*", "Button", true, false)
            .OfType<Button>()
            .First(button => button.Text == "加入房间");
        joinButton.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var address = (LineEdit)FindChild("JoinAddressInput", true, false);
        address.EmitSignal(Control.SignalName.FocusEntered);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var keypad = (Panel)FindChild("AddressKeypad", true, false);
        var keypadButtons = keypad.FindChildren("*", "Button", true, false).OfType<Button>().ToList();
        keypadButtons.First(button => button.Text == "清空").EmitSignal(Button.SignalName.Pressed);
        foreach (var character in "192.168.1.20:7000")
        {
            keypadButtons.First(button => button.Text == character.ToString()).EmitSignal(Button.SignalName.Pressed);
        }

        keypadButtons.First(button => button.Text == "1").EmitSignal(Button.SignalName.Pressed);
        keypadButtons.First(button => button.Text == "←").EmitSignal(Button.SignalName.Pressed);
        var port = (LineEdit)FindChild("JoinPortInput", true, false);
        port.EmitSignal(Control.SignalName.FocusEntered);
        keypadButtons.First(button => button.Text == "清空").EmitSignal(Button.SignalName.Pressed);
        foreach (var character in "8080")
        {
            keypadButtons.First(button => button.Text == character.ToString()).EmitSignal(Button.SignalName.Pressed);
        }

        var dotDisabled = keypadButtons.First(button => button.Text == ".").Disabled;
        var colonDisabled = keypadButtons.First(button => button.Text == ":").Disabled;
        address.EmitSignal(Control.SignalName.FocusEntered);

        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"KEYPAD_PROBE address={address.Text} port={port.Text} visible={keypad.Visible}");
        if (address.Text != "192.168.1.20:7000" || port.Text != "8080" ||
            !dotDisabled || !colonDisabled || !keypad.Visible)
        {
            GetTree().Quit(1);
            return;
        }

        var image = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://test_outputs"));
        var path = "res://test_outputs/main_menu_keypad.png";
        image.SavePng(path);
        GD.Print($"KEYPAD_PROBE screenshot={ProjectSettings.GlobalizePath(path)}");
        GetTree().Quit();
    }
}
