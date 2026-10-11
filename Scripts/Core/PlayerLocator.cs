using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Core;

/// <summary>
/// 玩家引用的唯一来源（解耦 + 省开销）。
///
/// 之前 HUD、每个交互物、EncounterController、StageController 都各自
/// `GetNodesInGroup("player_units")`，10 个交互物 × 60Hz 就是每帧十几次组查询。
/// 现在统一从这里取：缓存引用，失效或死亡时自动重建。
///
/// 注意：只在**帧驱动**的循环里用它；一次性逻辑仍可直接查组。
/// </summary>
public static class PlayerLocator
{
	private static Unit? _cached;

	/// <summary>当前玩家单位（找不到返回 null）。</summary>
	public static Unit? Player
	{
		get
		{
			if (_cached != null && GodotObject.IsInstanceValid(_cached) && !_cached.IsDead)
			{
				return _cached;
			}

			_cached = null;
			SceneTree? tree = Engine.GetMainLoop() as SceneTree;
			if (tree == null)
			{
				return null;
			}

			foreach (Node node in tree.GetNodesInGroup("player_units"))
			{
				if (node is Unit unit && GodotObject.IsInstanceValid(unit) && !unit.IsDead)
				{
					_cached = unit;
					break;
				}
			}

			return _cached;
		}
	}

	/// <summary>玩家位置（找不到时返回 origin 的等价占位：世界原点）。</summary>
	public static Vector3 Position => Player?.GlobalPosition ?? Vector3.Zero;

	/// <summary>是否已经找到玩家。</summary>
	public static bool Found => Player != null;

	/// <summary>场景切换 / 玩家重生后清缓存。</summary>
	public static void Invalidate() => _cached = null;
}
