using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Rewards;

namespace ActRoguelike.UI;

/// <summary>构筑详情、奖励卡和GM列表共用的可读规则信息。</summary>
public static class RewardPresentation
{
	public static string KindLabel(RewardKind kind) => kind switch
	{
		RewardKind.Evolution => "进化",
		RewardKind.Mutation => "异化",
		_ => "强化",
	};

	private static string RuleName(string id) => RewardCatalog.Find(id)?.Name
		?? ConfigRows.ById<RuneConfig>(ContentIdentity.Prerequisite(id))?.LocalizedName ?? id;

	public static string Details(RewardDefinition row, RunBuild? build = null)
	{
		var lines = new List<string>
		{
			$"分类：{KindLabel(row.Kind)}",
		};
		lines.Add("前置：" + (row.Requires.Length == 0 ? "无" : string.Join("、", row.Requires.Select(id =>
			RuleName(id) + (build == null ? string.Empty : build.SatisfiesPrerequisite(id) ? "（已满足）" : "（未满足）")))));
		var excludes = row.Excludes.Concat(row.ConflictsWith).Concat(RewardCatalog.All
			.Where(other => Array.IndexOf(other.Excludes, row.Id) >= 0).Select(other => other.Id)).Distinct().ToArray();
		lines.Add("互斥：" + (excludes.Length == 0 ? "无" : string.Join("、", excludes.Select(RuleName))));
		lines.Add("角色限定：" + (row.RequiredCharacterIds.Length == 0 ? "不限" : string.Join("、",
			row.RequiredCharacterIds.Select(id => ConfigRows.ById<CharacterConfig>(id)?.LocalizedName ?? id))));
		lines.Add("模式限定：" + (row.RequiredModeIds.Length == 0 ? "不限" : string.Join("、", row.RequiredModeIds.Select(id => ContentText.Get("mode."+id+".name", id)))));
		if (ConfigRows.ById<RewardConfig>(row.Id) is { } resource)
			foreach (var details in resource.Effects.OfType<IRewardRuntimeDetails>()) { string text = details.DescribeRuntime(); if (!string.IsNullOrEmpty(text)) lines.Add(text); }
		if (row.ApplicablePoolIds.Length > 0)
		{
			var weapons = ConfigRows.Ids<WeaponConfig>().Select(ConfigRows.ById<WeaponConfig>)
				.Where(weapon => weapon != null && Array.IndexOf(row.ApplicablePoolIds, weapon.Pool?.Id ?? string.Empty) >= 0)
				.Select(weapon => weapon!.LocalizedName).Distinct().ToArray();
			lines.Add("武器限定：" + (weapons.Length > 0 ? string.Join("、", weapons) : string.Join("、", row.ApplicablePoolIds)));
		}
		else lines.Add("武器限定：" + (row.RequiredWeaponTags.Length == 0 ? "不限" : string.Join("、", row.RequiredWeaponTags.Select(TagName))));
		return string.Join("\n\n", lines);
	}

	private static string TagName(string tag) => tag switch
	{
		"Projectile" => "投射物", "Melee" => "近战", "Physical" => "物理", "Gun" => "射击",
		"Bow" => "弓", "Crossbow" => "飞刀", "Scythe" => "镰刀", "Spear" => "长枪",
		"SpiritSword" => "灵剑", "EnergyPalm" => "能量波", _ => tag,
	};
}
