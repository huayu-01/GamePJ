using ActRoguelike.Audio;
using Godot;

namespace ActRoguelike.Core;

/// <summary>统一退出入口；给音频混音线程留出回收播放流的时间。</summary>
public static class GameExit
{
	private static bool _requested;
	public static async void Request(SceneTree tree, int exitCode = 0)
	{
		if (_requested) return;
		_requested = true;
		AudioManager.StopForShutdown();
		// 异步等待，不阻塞主线程；暂停和子弹时间均不能阻止用户退出。
		await tree.ToSignal(tree.CreateTimer(0.08, processAlways: true, processInPhysics: false, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);
		tree.Quit(exitCode);
	}
}
