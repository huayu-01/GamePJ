using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>
/// 通用“到距离就放技能”行为：读取能力上的 ApproachMinRange / ApproachMaxRange 提示，
/// 不需要为冲锋、跳跃、尖啸、自爆各写一个行为（执行文档 §4.6：靠数据而不是靠分支）。
/// </summary>
public partial class UseAbilityBehavior : Behavior
{
	/// <summary>指定能力节点名；留空则取拥有者子树里的第一个 Ability。</summary>
	[Export] public string AbilityNodeName { get; set; } = string.Empty;

	private Ability? _ability;

	protected override void OnReady()
	{
		_ability = string.IsNullOrEmpty(AbilityNodeName)
			? NodeSearch.FindInSubtree<Ability>(OwnerUnit)
			: OwnerUnit.GetNodeOrNull<Ability>(AbilityNodeName);
	}

	public override bool CanRun()
	{
		if (_ability is not { IsReady: true } || Target == null || !base.CanRun())
		{
			return false;
		}

		float distance = HorizontalDistanceTo(Target);
		float max = _ability.ApproachMaxRange > 0f ? _ability.ApproachMaxRange : float.MaxValue;
		return distance >= _ability.ApproachMinRange && distance <= max;
	}

	public override void Tick(float delta)
	{
		if (Target == null || _ability == null)
		{
			return;
		}

		Movement?.FaceDirection(HorizontalDirectionTo(Target), delta);
		Movement?.SetIntent(Vector3.Zero, 0f);
		_ability.TryActivate(Target);
	}
}
