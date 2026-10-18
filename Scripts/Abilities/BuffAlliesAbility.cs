using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 增益光环（技能精英用）：给半径内的友军挂一个状态效果。
/// 效果本身走 StatusPresets，不在这里写具体数值分支。
/// </summary>
public partial class BuffAlliesAbility : Ability
{
	[Export] public float Radius { get; set; } = 10f;
	[Export] public float BuffDuration { get; set; } = 5f;
	[Export] public string StatusId { get; set; } = "Empowered";
	[Export] public bool IncludeSelf { get; set; } = true;

	protected override bool CanActivate(Unit? target) => OwnerUnit != null;

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null)
		{
			Finish();
			return;
		}

		float radiusSquared = Radius * Radius;
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit ally || !ally.IsValidTarget || ally.IsDead)
			{
				continue;
			}

			if (ally == OwnerUnit && !IncludeSelf)
			{
				continue;
			}

			if (OwnerUnit.GlobalPosition.DistanceSquaredTo(ally.GlobalPosition) > radiusSquared)
			{
				continue;
			}

			if (StatusPresets.Create(StatusId, BuffDuration) is { } buff)
			{
				ally.ApplyStatus(buff, OwnerUnit, AbilityId);
			}
		}

		Finish();
	}
}

/// <summary>
/// 召唤：在自身周围生成若干单位（Boss 第三阶段的增援）。
/// 召唤物是完整 Unit，走同一套战斗与死亡流程。
/// </summary>
