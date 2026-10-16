using ActRoguelike.Config;
using ActRoguelike.Interaction;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>
/// 关卡节点的价值分层与摆放（从 StageController 抽出的独立职责）。
///
/// 关卡核心文档：每个 Stage 固定 10 个节点 = 高价值 2 + 中价值 5 + 低价值 3。
/// 高价值节点的挑战波数也更多 —— 用户本轮把映射从 **3 / 2 / 1** 改成 **4 / 3 / 1**，
/// 并且**改成按价值标签驱动**（`Config/Tables/ValueTags/tag_*.tres`），不再按节点类型枚举硬编码。
/// </summary>
public static class NodePlacement
{
	/// <summary>
	/// 节点价值层：2 高（进化/异化）、1 中（强化/商店）、0 低（局外资源/回复/特殊）。
	/// 层与波数**优先读节点表**（`Config/Tables/Nodes/node_*.tres` 的 `Layer` / `WaveCount`），
	/// 读不到行时回退到下面这份 switch（= 分表前的规则）。
	///
	/// ⚠️ `Weapon` 也在这里表态（用户 A3 + C2）："武器限定池"这个叫法指的就是**进化池**，
	/// 属于高价值；C2 把重复的 `node_weapon` 行合并进 `node_evolution` 之后，节点表里
	/// 再没有 `Kind = Weapon` 的行，这个分支就是它唯一的价值层来源（不加就会退化成 Layer 0 / 1 波，
	/// 与"进化池 = 高价值"自相矛盾）。场景里本来就没有 `Weapon` 实例，所以数字一个都不变。
	/// </summary>
	public static int LayerOf(RewardNodeKind kind)
	{
		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
		if (row != null)
		{
			return row.Layer;
		}

		return kind switch
		{
			RewardNodeKind.Weapon or RewardNodeKind.Evolution or RewardNodeKind.Mutation => 2,
			RewardNodeKind.Enhancement or RewardNodeKind.Shop => 1,
			_ => 0,
		};
	}

	/// <summary>
	/// 某个节点开几波 —— **按价值标签驱动**（用户需求：高价值 4 波 / 中价值 3 波 / 低价值 1 波，
	/// 而且「这些不写死，假设后期有彩蛋或其他奖励也会在这些不同的价值标签里」）。
	///
	/// 取值链（只有这一处定义，<see cref="StageController"/> 只调用它）：
	/// ① 节点行 <see cref="NodeConfig.ValueTags"/> 里的标签 → 价值标签表（`Config/Tables/ValueTags/tag_*.tres`）
	///    的 <see cref="ValueTagConfig.WaveCount"/>，多个标签命中时取**最大**值；
	/// ② 没挂标签 / 标签行读不到 → 节点行自己的 <see cref="NodeConfig.WaveCount"/>；
	/// ③ 连节点表都读不到 → 按价值层的代码兜底（高 4 / 中 3 / 低 1，与表里的值一致）。
	///
	/// ⚠️ 加一种新奖励（彩蛋之类）时**不用改这个方法**：给它的节点行挂一个已有的价值标签
	/// （或往价值标签表里加一行）。这就是"按标签驱动"而不是"按 <see cref="RewardNodeKind"/> 枚举硬编码"的意义。
	/// </summary>
	public static int WavesFor(RewardNodeKind kind)
	{
		NodeConfig? row = ConfigRows.ByKind<NodeConfig, RewardNodeKind>(kind);
		if (row == null)
		{
			return LayerOf(kind) switch { 2 => 4, 1 => 3, _ => 1 };
		}

		// ① 价值标签 → 波数（多标签取最大：标签是"这个节点至少值几波"的声明）。
		int fromTags = 0;
		foreach (string tag in row.ValueTags)
		{
			if (ConfigRows.ById<ValueTagConfig>(tag) is { WaveCount: > 0 } tagRow)
			{
				fromTags = Mathf.Max(fromTags, tagRow.WaveCount);
			}
		}

		if (fromTags > 0)
		{
			return fromTags;
		}

		// ② 节点行自己的波数（> 0 才算表过态）。
		if (row.WaveCount > 0)
		{
			return row.WaveCount;
		}

		// ③ 按价值层的代码兜底。
		return row.Layer switch { 2 => 4, 1 => 3, _ => 1 };
	}

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
		var ray = PhysicsRayQueryParameters3D.Create(origin + (Vector3.Up * 2f), origin + (Vector3.Down * 10f), CombatLayers.World);
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
				CollisionMask = CombatLayers.World,
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
