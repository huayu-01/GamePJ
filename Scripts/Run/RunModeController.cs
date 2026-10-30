using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Run;

/// <summary>独立模式提供玩法与统计；配装、构筑、回退和单次结算仍由整局控制器负责。</summary>
public abstract partial class RunModeController : Node3D
{
	public virtual string ModeId => string.Empty;
	public abstract string DisplayName { get; }
	public abstract StageStatistics CaptureStatistics();
	public abstract void Begin(RunController run, Unit player);
	public abstract void Stop();
	public virtual int SettlementShards(bool victory) => RunSettlement.ShardsFor(0, CaptureStatistics().ChallengesStarted, victory);
	public virtual string DescribeSettlement(bool victory, string build, int earned, int total)
	{
		var stats = CaptureStatistics();
		return $"{(victory ? "试炼完成" : "本局结束")} · {DisplayName}\n有效用时 {stats.ElapsedSeconds:0} 秒 · 完成目标 {stats.ChallengesStarted}\n{build}\n获得碎片 +{earned}（累计 {total}）";
	}
}
