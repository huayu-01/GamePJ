using ActRoguelike.Core;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>所有表行的公共接口：Id + Slot。让 <see cref="ConfigTable"/> 的寻址方法只需要写一遍。</summary>
public interface IConfigRow
{
	string Id { get; }

	int Slot { get; }
}

/// <summary>
/// 配置表读取的**统一入口**（设计文档 config-schema.md §4.1）：整个体系只有这一个"读文件"的地方。
/// 纪律（§4.2）：① 绝不抛异常；② 读不到 → 返回 null + 一条 warning，由调用方回退到**组件/行类自身的
/// [Export] 默认值**（不允许再写第二个兜底表 —— RunController.DefaultStages() 就是反面教材）；
/// ③ 回退要留痕，调用方负责写 GameLog，日志能直接证明这一局走的是"表"还是"默认值"。
/// </summary>
public static class ConfigTable
{
	/// <summary>表文件不存在 / 类型不符 / 加载失败 → 返回 null。复用存活包装器，失效时重新读取独立资源图。</summary>
	public static T? TryLoad<T>(string path) where T : Resource
	{
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
		{
			GD.PushWarning($"[Config] 未找到配置表 {path}，使用代码默认值。");
			return null;
		}

		T? loaded = RuntimeResources.Load<T>(path);
		if (loaded == null)
		{
			GD.PushWarning($"[Config] {path} 存在但不是 {typeof(T).Name}，使用代码默认值。");
		}

		return loaded;
	}

	/// <summary>按槽位号取行（1 起）。越界 / 空表 / 空行 → null。</summary>
	public static T? BySlot<T>(IReadOnlyList<T>? rows, int slot) where T : class
	{
		if (rows == null)
		{
			return null;
		}

		foreach (T row in rows)
		{
			if (row is IConfigRow withId && withId.Slot == slot)
			{
				return row;
			}
		}

		return null;
	}

	/// <summary>按 Id 取行（区分大小写 —— Id 一律小写 snake_case）。空行会被跳过。</summary>
	public static T? ById<T>(IReadOnlyList<T>? rows, string id) where T : class
	{
		if (rows == null || string.IsNullOrEmpty(id))
		{
			return null;
		}

		foreach (T row in rows)
		{
			if (row is IConfigRow withId && withId.Id == id)
			{
				return row;
			}
		}

		return null;
	}
}
