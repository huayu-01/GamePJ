using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Core;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>通过正式界面按钮和信号验证开启保护、连续选择与结算误触。</summary>
public partial class UiChoiceGuardProbe : Node
{
	private int _pass,_fail;
	private void Check(bool ok,string label){if(ok)_pass++;else _fail++;GD.Print($"[UiChoiceGuard] {(ok?"PASS":"FAIL")} {label}");}
	private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds,true),SceneTreeTimer.SignalName.Timeout);
	private async Task WaitUntilInteractive(ChooseScreen screen){for(int i=0;i<100 && screen.InputBlocked;i++)await Wait(.02);}
	private static Button Card(ChooseScreen screen)=>screen.GetNode<VBoxContainer>("Root/Panel/VBox/Cards").GetChildren().OfType<Button>().Last(b=>b.Visible);
	public override async void _Ready()
	{
		ProcessMode=ProcessModeEnum.Always;
		try
		{
			await Wait(.05);
			var screen=RuntimeResources.Load<PackedScene>("res://Scenes/UI/ChooseScreen.tscn").Instantiate<ChooseScreen>(); AddChild(screen);
			int choices=0,signals=0; screen.OptionChosen+=_=>signals++;
			screen.Open("防误触验收",new[]{"第一项"},_=>choices++);
			Button first=Card(screen); first.EmitSignal(Button.SignalName.Pressed);
			Check(screen.IsOpen && screen.InputBlocked && choices==0,"通用选择默认保护期内点击不关闭、不领取");
			await WaitUntilInteractive(screen); first.EmitSignal(Button.SignalName.Pressed); first.EmitSignal(Button.SignalName.Pressed);
			Check(!screen.IsOpen && choices==1 && signals==1,$"保护期后只选取一次：open={screen.IsOpen}, callback={choices}, signal={signals}, top={UiRouter.Instance?.IsTopScreen(screen)}");
			await Wait(.2);
			screen.Open("下一页",new[]{"第二项"},_=>choices++); await WaitUntilInteractive(screen);
			// 保留已退树的旧按钮，模拟旧页面信号迟到；不让自动释放抹掉这个反例。
			var old=Card(screen); old.GetParent().RemoveChild(old);
			old.EmitSignal(Button.SignalName.Pressed); await Wait(.2);
			screen.Open("新一轮",new[]{"第三项"},_=>choices++); await WaitUntilInteractive(screen); old.EmitSignal(Button.SignalName.Pressed); old.Free();
			Check(screen.IsOpen && choices==2 && signals==2,"已过保护期的新一轮也拒绝旧按钮迟到回调");
			Card(screen).EmitSignal(Button.SignalName.Pressed); Check(choices==3 && signals==3,"当前按钮仍能正常完成新一轮");
			await Wait(.2); screen.Open("连续弹窗",new[]{"继续"},_=>screen.Open("后续弹窗",new[]{"后续"},_=>choices++));
			await WaitUntilInteractive(screen); Card(screen).EmitSignal(Button.SignalName.Pressed);
			Check(screen.IsOpen && screen.InputBlocked,"连续弹窗重新计保护时间，旧关闭动画不能隐藏新页");
			await WaitUntilInteractive(screen); Check(screen.GetNode<Control>("Root").Visible,"连续弹窗缓动结束后仍可见");
			Card(screen).EmitSignal(Button.SignalName.Pressed); await Wait(.2); screen.QueueFree();
			var death=RuntimeResources.Load<PackedScene>("res://Scenes/UI/DeathScreen.tscn").Instantiate<DeathScreen>(); AddChild(death);
			death.BeginDeath("结算防误触验收","测试记录"); death.ContinueDeathFlow();
			Check(death.AwaitingConfirmation && death.SummaryInputBlocked && !death.ContinueDeathFlow(),"跳过演出后同帧重复确认不能误返回基地");
			await Wait(.65); Check(death.SummaryInputBlocked,"056结算在旧0.3秒后仍保持1秒保护");
			await Wait(.45); Check(!death.SummaryInputBlocked,"结算保护结束后恢复可确认状态");
			death.QueueFree(); GetTree().Paused=false;
		}
		catch(Exception exception){Check(false,exception.ToString());}
		GetTree().Paused=false; GD.Print($"[UiChoiceGuard] SUMMARY pass={_pass} fail={_fail}");GameExit.Request(GetTree(),_fail==0?0:1);
	}
}
