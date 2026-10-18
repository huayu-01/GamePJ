using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实基地节点与界面输入验证，不读写玩家符文存档。</summary>
public partial class RuneUiProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[RuneUI] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds = 0.35) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	public override async void _Ready()
	{
#if DEBUG
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			RuneCarry.UseInMemoryStoreForSelfTest();
			AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate());
			await Wait(1);
			var screen = UiService.Instance!.Screen<RuneScreen>()!;
			var terminal = Interactable.All.OfType<RuneTerminal>().Single();
			var player = ActRoguelike.Core.PlayerLocator.Player!;
			player.GlobalPosition = terminal.GlobalPosition;
			await Wait();
			Check(terminal.CanInteract, "真实基地符文台范围接受");
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = true });
			await Wait(0.05);
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, PhysicalKeycode = Key.F, Pressed = false });
			Check(screen.IsOpen && GetTree().Paused && Input.MouseMode == Input.MouseModeEnum.Visible, "打开暂停并显示鼠标");
			Check(screen.OptionCount == 12, "11枚表内符文加不携带");
			screen.EquipPreview(); Check(RuneCarry.Current == "", "开启保护期不误选");
			await Wait();
			foreach (RuneConfig row in ConfigIndex.Settings.Runes)
			{
				Check(ResourceLoader.Exists(row.IconPath) && ResourceLoader.Load<Texture2D>(row.IconPath) != null, $"图标 {row.Id}");
				var button = screen.GetNode<Control>("Root").FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == row.DisplayName);
				button.GrabFocus();
				Check(screen.PreviewId == row.Id, $"焦点预览 {row.DisplayName}");
				button.EmitSignal(Button.SignalName.Pressed);
				Check(RuneCarry.Current == row.Id && !terminal.Used, $"携带唯一且符文台可复用 {row.DisplayName}");
			}
			var empty = screen.GetNode<Control>("Root").FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "不携带符文");
			empty.EmitSignal(Button.SignalName.Pressed);
			Check(RuneCarry.Current == "", "卸下符文");
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
			await Wait();
			Check(!screen.IsOpen && !GetTree().Paused, "真实Esc关闭并恢复游戏");
			Check(terminal.Interact(), "关闭后可重复打开"); await Wait();
			if (DisplayServer.GetName() != "headless")
			{
				GetViewport().GetTexture().GetImage().SavePng("res://docs/character-design-j31-rune-ui.png");
				Check(true, "符文面板渲染截图");
			}
			screen.Close(); await Wait();
			var cfg = new ConfigFile(); cfg.SetValue("rune", "carried", "rune_lei_seal");
			string path = "res://.ai_rune_roundtrip.cfg";
			Check(cfg.Save(path) == Error.Ok, "独立临时目录写入正式ConfigFile格式");
			var loaded = new ConfigFile();
			Check(loaded.Load(path) == Error.Ok && loaded.GetValue("rune", "carried").AsString() == "rune_lei_seal", "携带字段磁盘往返");
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[RuneUI] === 符文界面验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
#endif
	}
}
