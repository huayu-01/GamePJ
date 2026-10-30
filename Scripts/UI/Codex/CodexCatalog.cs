using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

public sealed record CodexEntry(string Key, string Category, string Name, string Description, string Information,
	Texture2D? Icon, string ModelPath = "", Transform3D ModelTransform = default, bool ExtractModel = false);

/// <summary>只读取正式配置，不创建战斗单位、不发奖励、不写发现进度；显示文本不充当内容身份。</summary>
public static class CodexCatalog
{
	public static readonly string[] Categories = { "all", "characters", "weapons", "skills", "enhancements", "evolutions", "mutations", "specializations", "runes", "scene_runes", "enemies", "bosses", "elite_traits", "boss_traits", "effects", "nodes", "stages", "cards", "summons" };
	private static readonly string[] Titles = { "全部", "角色", "武器", "技能", "强化", "进化", "异化", "特化", "携带符文", "场景符文", "敌人", "首领", "精英特性", "首领强化", "战斗效果", "关卡交互", "关卡模式", "局外能力", "召唤物" };
	public static string Text(string key, string fallback) => ContentText.Get("ui.codex." + key, fallback);
	public static string CategoryName(string key)
	{
		int index = Array.IndexOf(Categories, key);
		return Text("category." + key, index < 0 ? key : Titles[index]);
	}
	public static Color CategoryColor(string key) => key switch {
		"enemies" or "bosses" or "elite_traits" => new("a24938"),
		"evolutions" => new("167783"), "specializations" => new("4d64a3"),
		"mutations" or "effects" => new("7953a6"),
		_ => new("34764b")
	};
	public static IReadOnlyList<CodexEntry> Build()
	{
		var index = ConfigIndex.Settings; var entries = new List<CodexEntry>();
		void Row(ConfigRow row, string category, Texture2D? icon = null, string info = "", string model = "", Transform3D? transform = null, string description = "", bool extractModel = false)
		{
			string body = string.IsNullOrWhiteSpace(row.LocalizedDescription) ? description : row.LocalizedDescription;
			entries.Add(new(category + "/" + row.Id, category, row.LocalizedName, body, info, icon ?? UiIcons.Holdings, model, transform ?? Transform3D.Identity, extractModel));
		}
		string UnitInfo(UnitConfig row) => string.Format(Text("unit_stats", "基础生命 {0:0}  ·  初始护甲 {1:0}\n基础移速 {2:0.0}\n\n实战数值会随关卡和精英效果变化。"), row.MaxHealth, row.StartingArmor, row.MoveSpeed);
		foreach (var row in index.Characters) Row(row, "characters", UiIcons.Named("ic_codex_character"), info: UnitInfo(row), model: row.ModelPath, transform: row.VisualProfile?.ModelTransform);
		foreach (var row in index.Weapons) Row(row, "weapons", LoadIcon(row.IconPath),
			(row.OwnerCharacter?.LocalizedName ?? "") + "\n\n" + string.Join("\n", row.Skills.Select(s => s.LocalizedName)), row.ModelPath);
		foreach (var row in index.Skills)
			Row(row, "skills", UiIcons.ForAbility(row), string.Format(Text("skill_stats", "基础冷却 {0:0.##} 秒\n\n实战冷却受构筑影响。"), row.Cooldown));
		foreach (var row in index.Upgrades.Where(r => r.Weight > 0))
		{
			var definition = RewardCatalog.Find(row.Id); if (definition == null) continue;
			string category = row.Specialization != default ? "specializations" : row.RewardKind switch { RewardKind.Evolution => "evolutions", RewardKind.Mutation => "mutations", _ => "enhancements" };
			Row(row, category, UiIcons.ForReward(definition), RewardPresentation.Details(definition));
		}
		foreach (var row in index.Runes)
		{
			var definition = RewardEffectRegistry.Compile(row);
			string details = definition == null ? "" : RewardPresentation.Details(definition);
			int firstRule = details.IndexOf("\n\n", StringComparison.Ordinal); if (firstRule >= 0) details = details[(firstRule + 2)..];
			Row(row, "runes", LoadIcon(row.IconPath), Text("rune_rule", "出击前在基地携带一枚，效果随本次冒险生效。") + "\n\n" + details);
		}
		foreach (var row in index.SceneRunes.Where(r => r.Weight > 0)) Row(row, "scene_runes", row.Icon, Text("scene_rune_rule", "每个场景仅有一种场景符文。当前效果显示在死战HUD中。"));
		foreach (var row in index.Enemies.Where(r => !r.IsGlobal && r.Weight > 0))
			Row(row, "enemies", UiIcons.ForEnemy(row.Id), info: UnitInfo(row), model: string.IsNullOrEmpty(row.ModelPath) ? row.ScenePath : row.ModelPath, transform: row.VisualProfile?.ModelTransform,
				description: string.Join("\n\n", row.PressureBehaviors.Select(key => Text("behavior." + key, Text("special_behavior", "留意其特殊行动和攻击预警。")))), extractModel: string.IsNullOrEmpty(row.ModelPath));
		foreach (var row in index.Bosses) Row(row, "bosses", UiIcons.Named("ic_codex_boss"), info: UnitInfo(row), model: string.IsNullOrEmpty(row.ModelPath) ? row.ScenePath : row.ModelPath, transform: row.VisualProfile?.ModelTransform,
			description: Text("boss_rule", "首领的攻击不会被普通受击打断。观察预警，利用技能间隙输出。"), extractModel: string.IsNullOrEmpty(row.ModelPath));
		var traits = RuntimeResources.Load<EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres");
		foreach (var row in traits.Traits.Where(r => r.Weight > 0))
			entries.Add(new("elite_traits/" + row.Id, "elite_traits", ContentText.Get(row.NameKey, row.Title), ContentText.Get(row.DescriptionKey, row.Description),
				Text("elite_rule", "每波最多一种精英特性。强效特性可能只出现在一名精英身上。"), UiIcons.Named("ic_ui_reward_mutation")));
		foreach (var row in index.Effects) Row(row, "effects", UiIcons.Named("ic_status_aura"));
		foreach (var row in index.PressureMods) Row(row, "boss_traits", UiIcons.Named("ic_ui_reward_evolution"));
		foreach (var row in index.Nodes) Row(row, "nodes", UiIcons.ForNodeKind(row.Kind), Text("node_rule", "靠近目标，按F交互，查看具体收益与挑战要求。"));
		foreach (var row in index.Stages) entries.Add(new("stages/" + row.Id, "stages", row.DisplayName, Text("adventure_rule", "冒险关卡：探索节点、完成战斗、获取构筑，再挑战首领。"), "", UiIcons.ForNodeKind(Interaction.RewardNodeKind.Portal)));
		var mode = RuntimeResources.Load<SurvivalDefinition>("res://Config/Modes/twilight_survival.tres");
		entries.Add(new("stages/" + mode.Id, "stages", mode.Title, Text("survival_rule", "死战：保留手动战斗，拾取经验升级，限时后挑战首领。"),
			Text("survival_objects", "蓝绿色信标：靠近按F启动，留在圆环内完成净化，回复15%生命并选增幅；三座全部净化可削弱首领。\n\n橙色遗物：阶段强敌掉落，靠近按F选择死战专属增幅。\n\n金色撤离门：击败终局首领后开启，按F完成本次冒险。"), UiIcons.ForNodeKind(Interaction.RewardNodeKind.Portal)));
		foreach (var row in index.AbilityCards) Row(row, "cards", UiIcons.ForAbilityCard(row), row.EffectText(1));
		foreach (var row in index.MetaUpgrades) Row(row, "cards");
		foreach (var row in index.Summons) Row(row, "summons", UiIcons.Named("ic_codex_enemy"), info: UnitInfo(row), model: string.IsNullOrEmpty(row.ModelPath) ? row.ScenePath : row.ModelPath, transform: row.VisualProfile?.ModelTransform, extractModel: string.IsNullOrEmpty(row.ModelPath));
		return entries.DistinctBy(e => e.Key).ToArray();
	}
	public static IEnumerable<CodexEntry> Filter(IEnumerable<CodexEntry> entries, string category, string query)
	{
		query = query.Trim();
		return entries.Where(e => (category == "all" || e.Category == category) && (query.Length == 0
			|| (e.Name + "\n" + e.Description + "\n" + e.Information).Contains(query, StringComparison.OrdinalIgnoreCase)));
	}
	private static Texture2D? LoadIcon(string path) => !string.IsNullOrEmpty(path) && ResourceLoader.Exists(path) ? RuntimeResources.Load<Texture2D>(path) : null;
}
