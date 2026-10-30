using Godot;

namespace ActRoguelike.Combat;

/// <summary>目标选择（执行文档 §4.1 Targeting）。按阵营与标签筛选，不绑定具体单位 ID。</summary>
public static class Targeting
{
	/// <summary>瞄准身体中心，不依赖可替换模型的骨骼名。</summary>
	public static Vector3 AimPoint(Unit target) => target.GetNodeOrNull<CollisionShape3D>("Body")?.GlobalPosition
		?? target.GlobalPosition + Vector3.Up * .9f;
	public static Unit? FindNearestHostile(Node3D from, Unit self, float maxDistance = float.MaxValue)
		=> FindNearestHostile(from.GlobalPosition, self, maxDistance);

	/// <summary>离手武器等独立位置的索敌；阵营与有效性仍按拥有者判断。</summary>
	public static Unit? FindNearestHostile(Vector3 from, Unit self, float maxDistance = float.MaxValue)
	{
		string group = self.Faction == Faction.Player ? "enemy_units" : "player_units";
		Unit? best = null;
		float bestDistance = maxDistance * maxDistance;

		foreach (Node node in self.GetTree().GetNodesInGroup(group))
		{
			if (node is not Unit unit || !unit.IsValidTarget || !self.IsHostileTo(unit))
			{
				continue;
			}

			float distance = from.DistanceSquaredTo(unit.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = unit;
			}
		}

		return best;
	}
}
