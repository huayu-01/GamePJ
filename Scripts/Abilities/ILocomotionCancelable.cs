namespace ActRoguelike.Abilities;

/// <summary>明确允许玩家移动／跳跃取消的动作，不改变其他技能的打断规则。</summary>
public interface ILocomotionCancelable
{
	void CancelForLocomotion();
}
