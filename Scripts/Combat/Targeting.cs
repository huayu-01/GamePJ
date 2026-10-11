using Godot;

namespace ActRoguelike.Combat;

/// <summary>目标选择（执行文档 §4.1 Targeting）。按阵营与标签筛选，不绑定具体单位 ID。</summary>
public static class Targeting
{
	public static Unit? FindNearestHostile(Node3D from, Unit self, float maxDistance = float.MaxValue)
	{
		string group = self.Faction == Faction.Player ? "enemy_units" : "player_units";
		Unit? best = null;
		float bestDistance = maxDistance * maxDistance;

		foreach (Node node in from.GetTree().GetNodesInGroup(group))
		{
			if (node is not Unit unit || !unit.IsValidTarget || !self.IsHostileTo(unit))
			{
				continue;
			}

			float distance = from.GlobalPosition.DistanceSquaredTo(unit.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = unit;
			}
		}

		return best;
	}
}
