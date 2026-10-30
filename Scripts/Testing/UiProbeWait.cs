using System;
using System.Threading.Tasks;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>正向流程等待真实弹窗就绪，不把旧0.3秒假设写死；保护期反例由专用探针验证。</summary>
public static class UiProbeWait
{
	public static async Task Interactive(Node owner, OverlayScreen? screen = null)
	{
		ulong deadline = Time.GetTicksMsec() + 5000;
		while ((screen ?? UiRouter.Instance?.TopScreen)?.InputBlocked == true)
		{
			if (Time.GetTicksMsec() >= deadline) throw new InvalidOperationException("等待弹窗输入保护结束超时");
			await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
