using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 场景切换用的加载页面（用户需求：进关卡要有加载页面并暂停玩家输入）。
/// 刻意不继承 OverlayScreen —— 它不走界面栈，而是自己暂停整棵树再切场景，
/// 所以 ProcessMode 必须是 Always，否则暂停期间计时器不会走、永远切不过去。
/// </summary>
public partial class LoadingScreen : CanvasLayer
{
	/// <summary>最短显示时间，避免一闪而过看不到。</summary>
	[Export] public float MinShowSeconds { get; set; } = 0.4f;

	private string _target = string.Empty;
	private Label _label = null!;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		_label = GetNodeOrNull<Label>("Root/Panel/Label") ?? new Label();
	}

	/// <summary>显示加载页 → 暂停游戏与输入 → 最短显示时间后切换场景。</summary>
	public void BeginLoad(string scenePath)
	{
		_target = scenePath;
		Visible = true;
		_label.Text = $"加载中…\n{scenePath}";
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameLog.Write("Flow", $"加载页面：准备进入 {scenePath}");
		GetTree().CreateTimer(MinShowSeconds, processAlways: true).Timeout += Finish;
	}

	private void Finish()
	{
		if (string.IsNullOrEmpty(_target))
		{
			return;
		}

		string target = _target;
		_target = string.Empty;
		GetTree().Paused = false;
		GameLog.Write("Flow", $"加载页面：切换到 {target}");
		GetTree().ChangeSceneToFile(target);
	}
}
