using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>编译效果资源与奖励元数据；旧字符串仅交给兼容翻译器。</summary>
public static class RewardEffectRegistry
{
	/// <summary>这个 handler id 注册过吗（读表校验用）。</summary>
	public static bool IsRegistered(string handler)
		=> !string.IsNullOrEmpty(handler) && RewardEffectCompatibility.Contains(handler);

	/// <summary>所有已注册的 handler id（开场日志用，便于人肉核对表里写了什么）。</summary>
	public static IEnumerable<string> RegisteredIds => RewardEffectCompatibility.Ids;

	/// <summary>
	/// 把一行翻译成运行期的 <c>RewardDefinition</c>。
	/// handler 未注册 → 返回 null（调用方警告并跳过这一行，绝不让它静默变成"没有效果的奖励"）。
	/// </summary>
	public static RewardDefinition? Compile(RewardConfig row)
	{
		Action<RewardContext>? apply;
		if (row.Effects.Length > 0) { apply = RewardEffectCompiler.Compile(row); if (apply == null) return null; }
		else if (RewardEffectCompatibility.TryCompile(row, out apply)) { if (apply == null) return null; }
		else
		{
			GD.PushWarning($"[Config] 增幅项 {row.Id} 的 ApplyHandler \"{row.ApplyHandler}\" 未注册，已跳过这一行。");
			return null;
		}

		var textParams = new List<ParamEntry>(row.HandlerParams);
		textParams.AddRange(row.Params);
		return new RewardDefinition
		{
			Id = row.Id,
			ReplacesId = row.Replaces?.Id ?? string.Empty,
			ShowUpgradeLevel = row.ShowUpgradeLevel,
			Name = string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName,
			Description = ConfigText.Format(row.DescriptionTemplate, row, textParams),
			NameResolver = () => row.LocalizedName,
			DescriptionResolver = () => ContentText.Description(row, textParams),
			Kind = row.RewardKind,
			RequiredModeIds = row.RequiredModeIds.ToArray(),
			IconName = row.IconName,
			ApplicablePoolIds = PoolIdsOf(row),
			RequiredWeaponTags = row.RequiredWeaponTags,
			RequiredCharacterIds = row.RequiredCharacterIds,
			Requires = row.Requires,
			ConflictsWith = row.ConflictsWith,
			Excludes = row.Excludes,
			Weight = row.Weight,
			MaxStacks = row.MaxStacks,
			Apply = apply,
		};
	}

	/// <summary>
	/// 把行上的"专属升级池"引用编译成池 Id 列表（<c>RewardDefinition.ApplicablePoolIds</c>）。
	///
	/// 只把 **Id 字符串**交给运行期视图，不带 `Config.UpgradePoolConfig` 这个磁盘类型过去 ——
	/// 运行期视图不认识配置文件类（config-schema.md §2.2 的分层）；而"引用会不会悬空"的问题
	/// 在**磁盘侧**已经由"资源引用"解决了（池行被删 → 这里收到 null → 返回空 → 落到②③两层）。
	/// </summary>
	private static string[] PoolIdsOf(RewardConfig row)
	{
		if (row.Pool == null)
		{
			return Array.Empty<string>();
		}

		if (string.IsNullOrEmpty(row.Pool.Id))
		{
			GD.PushWarning($"[Config] 增幅项 {row.Id} 指向的升级池行没有填 Id，已忽略（这一条会落到「标签匹配 → 通用」两层）。");
			return Array.Empty<string>();
		}

		return new[] { row.Pool.Id };
	}

}
