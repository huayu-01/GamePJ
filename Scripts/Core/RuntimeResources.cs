using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Core;

/// <summary>
/// 运行期资源读取入口：只复用仍存活的托管包装器；失效时深度绕过原生缓存。
/// Godot 4.7.2 的原生缓存可能复活已收集但尚未终结的 C# Resource（上游 #83762）。
/// 弱引用不延长资源寿命；不禁用 GC、不拦截终结器异常、不使用无限强引用池。
/// </summary>
public static class RuntimeResources
{
	private static readonly object Gate = new();
	private static readonly Dictionary<string, WeakReference<Resource>> Cache = new(StringComparer.Ordinal);
	private static int _loads;

	public static T Load<T>(string path) where T : Resource
	{
		lock (Gate)
		{
			if (++_loads % 64 == 0)
			{
				var expired = new List<string>();
				foreach (var entry in Cache)
					if (!entry.Value.TryGetTarget(out Resource? value) || !GodotObject.IsInstanceValid(value)) expired.Add(entry.Key);
				foreach (string key in expired) Cache.Remove(key);
			}

			// TryGetTarget 原子取得强引用，避免“先检查弱引用，再去原生缓存拿对象”的竞态。
			if (Cache.TryGetValue(path, out var weak) && weak.TryGetTarget(out Resource? live) && GodotObject.IsInstanceValid(live))
				return live as T ?? null!;

			T loaded = ResourceLoader.Load<T>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
			if (loaded != null) Cache[path] = new WeakReference<Resource>(loaded);
			return loaded!;
		}
	}

	/// <summary>保持引擎原有切场景顺序，由 ChangeSceneToPacked 管理当前场景退出与延迟加入。</summary>
	public static Error ChangeScene(SceneTree tree, string path)
	{
		PackedScene scene = Load<PackedScene>(path);
		return scene == null ? Error.CantOpen : tree.ChangeSceneToPacked(scene);
	}
}
