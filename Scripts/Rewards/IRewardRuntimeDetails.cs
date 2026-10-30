namespace ActRoguelike.Rewards;

/// <summary>内容行为自行提供本局进度，界面不按增幅Id识别具体规则。</summary>
public interface IRewardRuntimeDetails
{
	string DescribeRuntime();
}
