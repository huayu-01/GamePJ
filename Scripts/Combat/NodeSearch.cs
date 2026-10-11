using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>按类型在子树里找组件（能力、武器、命中体…），避免依赖节点名。</summary>
public static class NodeSearch
{
	public static T? FindInSubtree<T>(Node root) where T : class
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T found)
			{
				return found;
			}

			T? nested = FindInSubtree<T>(child);
			if (nested != null)
			{
				return nested;
			}
		}

		return null;
	}

	public static void CollectInSubtree<T>(Node root, List<T> results) where T : class
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T found)
			{
				results.Add(found);
			}

			CollectInSubtree(child, results);
		}
	}
}
