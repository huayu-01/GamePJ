using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 按 Id / Slot 查配置行的运行期索引（设计文档 config-schema.md §4.1）。
///
/// **表里没有"表文件"**：磁盘上是一堆行 `.tres`（`Config/Tables/&lt;分组&gt;/xxx_*.tres`），
/// 索引用的是 <see cref="BalanceConfig"/> 上的 <c>[Export] XxxConfig[]</c> 数组 —— 不扫目录。
///
/// 纪律（与 <see cref="ConfigTable"/> 一致）：
/// ① 读不到行 → 返回 null，**绝不抛异常、绝不另建兜底表**；
/// ② 调用方拿到 null 时必须保留目标对象自身的 <c>[Export]</c> 默认值（"读不到则使用自身的默认值"）；
/// ③ 索引为空是**正常状态**（例如 `Balance.tres` 没加载），只在真正要查具体 Id 时才需要在意。
/// </summary>
public static class ConfigRows
{
	private static readonly Dictionary<string, Dictionary<string, ConfigRow>> ByIdMap = new();
	private static readonly Dictionary<string, Dictionary<int, ConfigRow>> BySlotMap = new();
	private static readonly Dictionary<string, List<ConfigRow>> RowsMap = new();
	private static readonly Dictionary<string, string> KindMap = new();

	private static string KeyOf<T>() => typeof(T).Name;

	private static string KindKeyOf<TKind>(TKind kind) where TKind : struct, System.Enum
		=> $"{typeof(TKind).Name}.{kind}";

	/// <summary>用一整张表的行重建索引。表为空 = 清空该表（读行会返回 null，由调用方回退默认值）。</summary>
	public static void Rebuild<T>(IReadOnlyList<T>? rows) where T : ConfigRow
	{
		string key = KeyOf<T>();
		var ids = new Dictionary<string, ConfigRow>();
		var slots = new Dictionary<int, ConfigRow>();
		var all = new List<ConfigRow>();

		if (rows != null)
		{
			foreach (T row in rows)
			{
				if (row == null)
				{
					continue;
				}

				all.Add(row);

				if (string.IsNullOrEmpty(row.Id))
				{
					GD.PushWarning($"[Config] {row.ResourcePath} 没有填 Id，已跳过。");
					continue;
				}

				if (ids.ContainsKey(row.Id))
				{
					GD.PushWarning($"[Config] {key} 表里 Id \"{row.Id}\" 重复（{row.ResourcePath}），后面的行不会生效。");
					continue;
				}

				ids[row.Id] = row;
				if (row.Slot > 0)
				{
					slots.TryAdd(row.Slot, row);
				}
			}
		}

		ByIdMap[key] = ids;
		BySlotMap[key] = slots;
		RowsMap[key] = all;
	}

	/// <summary>按 Id 取行。读不到 → null（调用方回退到自身默认值）。</summary>
	public static T? ById<T>(string? id) where T : ConfigRow
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}

		return ByIdMap.TryGetValue(KeyOf<T>(), out Dictionary<string, ConfigRow>? ids)
			&& ids.TryGetValue(id, out ConfigRow? row)
			? (T)row
			: null;
	}

	/// <summary>按槽位号取行（1 起；<see cref="IConfigRow.Slot"/> 语义见各表）。</summary>
	public static T? BySlot<T>(int slot) where T : ConfigRow
	{
		if (slot <= 0)
		{
			return null;
		}

		return BySlotMap.TryGetValue(KeyOf<T>(), out Dictionary<int, ConfigRow>? slots)
			&& slots.TryGetValue(slot, out ConfigRow? row)
			? (T)row
			: null;
	}

	/// <summary>
	/// 按**预制体路径**找行（靠行里的 <c>ScenePath</c> 属性，走引擎自带的 <c>Get</c>，不需要类型特判）。
	/// 用在"场景里的生成项 ↔ 表里的行"这种配对上：敌人表 / 首领表 / 召唤物表都是这么配对的，
	/// 所以改文案、改显示名都不会再把配对搞丢。
	/// </summary>
	public static T? ByScenePath<T>(string? scenePath) where T : ConfigRow
	{
		if (string.IsNullOrEmpty(scenePath) || !RowsMap.TryGetValue(KeyOf<T>(), out List<ConfigRow>? rows))
		{
			return null;
		}

		foreach (ConfigRow row in rows)
		{
			Variant path = row.Get("ScenePath");
			if ((path.VariantType is Variant.Type.String or Variant.Type.StringName) && path.AsString() == scenePath)
			{
				return (T)row;
			}
		}

		return null;
	}

	/// <summary>本表当前索引到多少行（自测/日志用）。</summary>
	public static int Count<T>() where T : ConfigRow
		=> ByIdMap.TryGetValue(KeyOf<T>(), out Dictionary<string, ConfigRow>? ids) ? ids.Count : 0;

	/// <summary>本表当前索引到的所有 Id（自测/日志用，顺序不保证）。</summary>
	public static List<string> Ids<T>() where T : ConfigRow
		=> ByIdMap.TryGetValue(KeyOf<T>(), out Dictionary<string, ConfigRow>? ids)
			? new List<string>(ids.Keys)
			: new List<string>();

	/// <summary>
	/// 登记一个"按枚举取行"的索引键（枚举 → 该表里的行 Id）。
	/// 用在需要"节点类型 → 行""奖励类型 → 行"这种查找的表上（<see cref="NodeConfig"/> 就是这么用的）。
	/// </summary>
	public static void SetKind<TKind>(TKind kind, string id) where TKind : struct, System.Enum
		=> KindMap[KindKeyOf(kind)] = id;

	/// <summary>按枚举取行。值留空 / 行读不到 → null（调用方回退到代码里的原逻辑）。</summary>
	public static T? ByKind<T, TKind>(TKind kind) where T : ConfigRow where TKind : struct, System.Enum
		=> KindMap.TryGetValue(KindKeyOf(kind), out string? id) ? ById<T>(id) : null;
}
