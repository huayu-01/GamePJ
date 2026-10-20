using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 格挡（战斗规则说明 §14）：独立于伤害减免的防御机制，支持部分格挡与正面判定。
/// 挂在 Unit 下名为 Block 的节点上，是否格挡由 Behavior/Ability 决定。
/// </summary>
public partial class BlockComponent : Node
{
	[Export] public bool Blocking { get; set; } = true;
	[Export] public float BlockStunSeconds { get; set; } = 0.15f;

	/// <summary>格挡后剩余的伤害比例：0 = 完全格挡，0.2 = 只吃 20%。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float BlockDamageMultiplier { get; set; } = 0.2f;

	/// <summary>正面判定阈值：入射方向与自身前方的点积小于等于该值才算挡到。</summary>
	[Export(PropertyHint.Range, "-1,1,0.05")] public float FrontDotThreshold { get; set; } = -0.35f;

	/// <summary>超过这个伤害值无法格挡（破防），0 = 不限制。</summary>
	[Export] public float BreakThreshold { get; set; }

	/// <summary>返回伤害倍率：1 = 没挡到。</summary>
	public float ResolveMultiplier(HitInstance hit)
	{
		if (!Blocking || BlockDamageMultiplier >= 1 || GetParent() is not Unit owner)
		{
			return 1f;
		}

		if (BreakThreshold > 0f && hit.Damage >= BreakThreshold)
		{
			return 1f;
		}

		Vector3 forward = -owner.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();

		Vector3 incoming = hit.Direction;
		incoming.Y = 0f;
		if (incoming.LengthSquared() < 0.0001f)
		{
			owner.Reactions?.BlockRecoil(BlockStunSeconds);
			return BlockDamageMultiplier;
		}

		if (incoming.Normalized().Dot(forward) > FrontDotThreshold) return 1f;
		owner.Reactions?.BlockRecoil(BlockStunSeconds);
		return BlockDamageMultiplier;
	}
}
