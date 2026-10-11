using ActRoguelike.Config;
using ActRoguelike.Interaction;
using Godot;

namespace ActRoguelike.Run;

/// <summary>
/// 关卡节点的价值分层与摆放（从 StageController 抽出的独立职责）。
///
/// 关卡核心文档：每个 Stage 固定 10 个节点 = 高价值 2 + 中价值 5 + 低价值 3，
/// 高价值节点的挑战波数也更多（3 / 2 / 1 波）。
/// </summary>
public static class NodePlacement
{
	/// <summary>
	/// 节点价值层：2 高（进化/异化）、1 中（强化/商店）、0 低（局外资源/回复/特殊）。
	/// </summary>
	public static int LayerOf(RewardNodeKind kind) => kind switch
	{
		RewardNodeKind.Evolution or RewardNodeKind.Mutation => 2,
		RewardNodeKind.Enhancement or RewardNodeKind.Shop => 1,
		_ => 0,
	};

	/// <summary>某个价值层的节点会开几波（关卡核心文档 §波数：高 3 / 中 2 / 低 1）。</summary>
	public static int WavesFor(RewardNodeKind kind) => LayerOf(kind) switch { 2 => 3, 1 => 2, _ => 1 };

	/// <summary>
	/// 把节点放到最近的可站立位置（不与障碍物重叠）。
	/// 有导航网格时直接吸附；否则退回"射线找地面 + 球形探测螺旋外扩"。
	/// </summary>
	public static void PlaceSafely(Node3D node)
	{
		PhysicsDirectSpaceState3D? space = node.GetWorld3D()?.DirectSpaceState;
		if (space == null)
		{
			return;
		}

		Vector3 origin = node.GlobalPosition;

		// 1) 找脚下地面（World 层 = 1）。射线起点只抬高 2 米，避免打到上层平台/斜坡顶面。
		var ray = PhysicsRayQueryParameters3D.Create(origin + (Vector3.Up * 2f), origin + (Vector3.Down * 10f), 1);
		Godot.Collections.Dictionary hit = space.IntersectRay(ray);
		float groundY = hit.Count > 0 ? ((Vector3)hit["position"]).Y : origin.Y;

		// 2) 有导航网格就直接吸附（比探测更准）。
		if (World.MapManager.Instance is { IsReady: true } map)
		{
			node.GlobalPosition = map.NodeAnchor(new Vector3(origin.X, groundY, origin.Z));
			return;
		}

		// 3) 螺旋外扩找空位（球心要抬到半径之上，否则永远与脚下地面重叠）。
		for (int step = 0; step < 28; step++)
		{
			float dist = step * 0.7f;
			float angle = step * 1.15f;
			var candidate = new Vector3(
				origin.X + (Mathf.Cos(angle) * dist),
				groundY + 0.25f,
				origin.Z + (Mathf.Sin(angle) * dist));

			var probe = new PhysicsShapeQueryParameters3D
			{
				Shape = new SphereShape3D { Radius = 1.3f },
				Transform = new Transform3D(Basis.Identity, candidate + (Vector3.Up * 1.65f)),
				CollisionMask = 1,
			};

			if (space.IntersectShape(probe, 1).Count == 0)
			{
				if (candidate.DistanceTo(origin) > 0.05f)
				{
					Core.GameLog.Write("Node", $"节点避障：{node.Name} 从 {origin} 挪到 {candidate}");
				}

				node.GlobalPosition = candidate;
				return;
			}
		}

		Core.GameLog.Write("Node", $"节点避障：{node.Name} 附近找不到空位，保持原位 {origin}");
	}
}
