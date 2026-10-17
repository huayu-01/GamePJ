using ActRoguelike.Audio;
using Godot;
using System.Threading.Tasks;

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
		ulong stoppedAt = Time.GetTicksMsec();
		// 混音线程按真实时间工作；无头不限帧时，游戏计时器的0.08秒可能只经过数毫秒。
		// 捕获Godot主线程同步上下文，真实等待后仍回主线程退出；暂停和慢时间均不影响。
		await Task.Delay(80);
		GameLog.Write("Exit", $"音频退出回收：真实等待{Time.GetTicksMsec() - stoppedAt}ms。");
		tree.Quit(exitCode);
	}
}
