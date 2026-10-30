using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Config;

public sealed record ContentIssue(string Resource, string Field, string Consumer, string Reason, string Fix)
{
	public override string ToString() => $"{Resource} → {Field} → {Consumer}：{Reason}；修复：{Fix}";
}

/// <summary>制作期只读校验；不进入场景树、不执行技能，也不替代正式运行的显式索引。</summary>
public static class ContentValidator
{
	// 目录扫描从首次读取起就绕过原生缓存；不能先用默认缓存预热再改用IgnoreDeep。
	internal static ConfigRow? LoadAuthoredRow(string path) => RuntimeResources.Load<Resource>(path) as ConfigRow;

	public static IReadOnlyList<ContentIssue> Validate(ConfigIndexData index, bool scanUnindexed = false)
	{
		var issues = new List<ContentIssue>();
		var numericIds = new Dictionary<int, Resource>();
		var eliteTraits = RuntimeResources.Load<ActRoguelike.Combat.Elites.EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres");
		if (eliteTraits == null) issues.Add(new("res://Config/EliteTraits/catalog.tres", "Traits", "精英波次", "缺少显式特性目录", "恢复精英特性目录"));
		else foreach (string error in eliteTraits.Validate()) issues.Add(new(eliteTraits.ResourcePath, "Traits", "精英波次", error, "修正特性参数或重复Id"));
		var tables = index.GetType().GetProperties().Where(p => p.PropertyType.IsArray && (typeof(ConfigRow).IsAssignableFrom(p.PropertyType.GetElementType()) || p.PropertyType.GetElementType() == typeof(StageDefinition))).ToArray();
		var rows = new List<Resource>();
		foreach (var property in tables)
		{
			var ids = new HashSet<string>(StringComparer.Ordinal); var slots = new HashSet<int>();
			int position = 0;
			foreach (Resource? row in (IEnumerable)property.GetValue(index)!)
			{
				if (row == null) { issues.Add(new(index.ResourcePath, $"{property.Name}[{position++}]", "ConfigRows", "索引含空引用", "删除空元素或重新指定资源")); continue; }
				position++; rows.Add(row);
				ValidateNumeric(row);
				string id = row.Get("Id").AsString();
				if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) Add(row, "Id", "ConfigRows.ById", "Id 为空或在同一张表重复", "填写同表唯一的稳定 Id");
				if (row is ConfigRow config && config.Slot > 0 && !slots.Add(config.Slot)) Add(row, "Slot", "ConfigRows.BySlot", "同一张表的正槽位重复", "分配唯一槽位；不进槽位则填 0");
			}
		}
		// IgnoreDeep 加载会为相同文件产生独立实例，制作身份按文件、类型与稳定 Id 比较。
		var indexed = rows.Select(Identity).ToHashSet(StringComparer.Ordinal);
		if (index.ContentLocales.Length == 0 && rows.OfType<ConfigRow>().Any(r => !string.IsNullOrEmpty(r.NameKey) || !string.IsNullOrEmpty(r.DescriptionKey)))
			issues.Add(new(index.ResourcePath, "ContentLocales", "ContentText.Initialize", "内容声明了文本键但未加载任何语言目录", "将语言资源添加到索引，tres 自定义属性应放在 script 赋值之后"));
		var rewardIds = rows.OfType<RewardConfig>().Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
		var modes = scanUnindexed ? ResourceFiles("res://Config/Modes").Select(RuntimeResources.Load<Resource>).OfType<RunModeDefinition>().ToArray() : Array.Empty<RunModeDefinition>();
		var modeIds = scanUnindexed ? modes.Select(m => m.Id).ToHashSet(StringComparer.Ordinal) : null;
		foreach (var mode in modes) ValidateNumeric(mode);
		if (eliteTraits != null) foreach (var trait in eliteTraits.Traits) if (trait != null) ValidateNumeric(trait);
		var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (RuneConfig rune in index.Runes.Where(r => r != null))
			foreach (string alias in rune.LegacyPrerequisiteAliases)
			{
				if (rewardIds.Contains(alias) || (aliases.TryGetValue(alias, out string? prior) && prior != rune.Id)) Add(rune, "LegacyPrerequisiteAliases", "ContentIdentity", $"别名 {alias} 与其他身份冲突", "移除歧义别名，保留旧条件的一对一映射");
				else aliases[alias] = rune.Id;
			}
		var pressure = RuntimeResources.Load<EnemyBehaviorPressure>("res://Config/EnemyBehaviorPressure.tres");
		foreach (Resource resource in rows)
		{
			Visit(resource, resource, string.Empty, new HashSet<ulong>());
			if (resource is UnitConfig { VisualProfile: { } visual })
			{
				foreach (string error in visual.Validate()) Add(resource, "VisualProfile", "CharacterDisplay / UnitAnimator", error, "修正表现档案的模型、骨架、挂点与播放器路径");
				if (resource is CharacterConfig && visual.GripScene == null) Add(resource, "VisualProfile.GripScene", "CharacterDisplay", "角色表现档案未声明握持挂点", "在档案中指定该骨架专用的挂点场景");
			}
			if (resource is ConfigRow textRow)
			{
				var parameters = textRow is RewardConfig rr ? rr.HandlerParams.Concat(rr.Params).ToArray() : textRow.Params;
				foreach (ContentLocale catalog in index.ContentLocales.Where(c => c != null))
					foreach (var entry in new[] { (textRow.NameKey, textRow.DisplayName), (textRow.DescriptionKey, textRow.DescriptionTemplate) })
					{
						if (string.IsNullOrEmpty(entry.Item1)) continue;
						if (!catalog.Messages.TryGetValue(entry.Item1, out string? translated) || translated == null)
						{
							if (catalog.RequireComplete) Add(textRow, entry.Item1, "ContentText", $"{catalog.Locale} 缺少整句译文", "运行 sync_content_text.py --apply 同步原文，或在相应语言目录补译");
							continue;
						}
						if (!ConfigText.ParameterKeys(entry.Item2).Order().SequenceEqual(ConfigText.ParameterKeys(translated).Order())) Add(textRow, entry.Item1, "ConfigText.Format", $"{catalog.Locale} 译文的命名参数与原文不一致", "保留相同的完整参数名，允许调整顺序与格式");
						foreach (string key in ConfigText.UnresolvedParameters(translated, textRow, parameters)) Add(textRow, entry.Item1, "ConfigText.Format", $"{catalog.Locale} 无法读取参数 {key}", "让参数引用实际效果字段，或补充已声明的参数");
					}
			}
			if (resource is RewardConfig reward)
			{
				if (reward.Replaces is { } previous)
				{
					if (!rewardIds.Contains(previous.Id) || !reward.Requires.Contains(previous.Id)) Add(reward, "Replaces", "RunBuild.Add", "升级前级必须已索引且声明为前置", "选择已索引的直接上一级并填写Requires");
					var chain = new HashSet<string> { reward.Id };
					for (var ancestor = previous; ancestor != null; ancestor = ancestor.Replaces)
						if (!chain.Add(ancestor.Id)) { Add(reward, "Replaces", "RunBuild.Add", "升级替换链存在循环", "移除自引用或循环引用"); break; }
				}
				foreach (string id in reward.RequiredModeIds)
					if (string.IsNullOrWhiteSpace(id) || (modeIds != null && !modeIds.Contains(id))) Add(reward, "RequiredModeIds", "RewardPool", $"模式 {id} 不存在", "填写模式设置中的稳定Id");
				if (reward.SpecializationBehavior is { } behavior && behavior.Kind != reward.Specialization) Add(reward, "SpecializationBehavior", "SpecializationRuntime", "行为定义与特化类型不匹配", "选择与 Specialization 相同类型的行为定义");
				if (!reward.HasKnownHandler) Add(reward, "Effects / ApplyHandler", "RewardEffectRegistry.Compile", "没有可执行的效果", "添加效果定义资源或选择现有兼容处理器");
				if (!float.IsFinite(reward.Weight) || reward.Weight < 0) Add(reward, "Weight", "RewardPool", "权重必须是有限非负数", "填写合法权重；0 表示不抽取");
				for (int i = 0; i < reward.Effects.Length; i++)
				{
					if (reward.Effects[i] == null) { Add(reward, $"Effects[{i}]", "RewardEffectCompiler", "效果为空", "指定效果资源"); continue; }
				}
				foreach (string token in reward.Requires.Concat(reward.ConflictsWith).Concat(reward.Excludes))
					if (!rewardIds.Contains(token) && !aliases.ContainsKey(token)) Add(reward, "Requires / ConflictsWith / Excludes", "RewardPool", $"未知条件 {token}", "填写已索引奖励或符文的稳定 Id");
				foreach (string id in reward.RequiredCharacterIds)
					if (!index.Characters.Any(c => c != null && c.Id == id)) Add(reward, "RequiredCharacterIds", "RewardDefinition.AllowsCharacter", $"角色 {id} 不存在", "填写已索引角色 Id");
			}
			if (resource is EnemyConfig enemy && !enemy.IsGlobal)
			{
				if (pressure == null || !pressure.TryCalculate(enemy.PressureBehaviors, out _)) Add(enemy, "PressureBehaviors", "EnemyBehaviorPressure.SpawnCost", "存在未定义或非法压力权重", "在 Config/EnemyBehaviorPressure.tres 声明行为及非负权重");
				ValidateUnit(enemy, enemy.ScenePath);
			}
			else if (resource is CharacterConfig character) ValidateUnit(character, "res://Scenes/Player/Player.tscn");
			else if (resource is BossConfig boss) ValidateUnit(boss, boss.ScenePath);
			else if (resource is SummonConfig summon) ValidateUnit(summon, summon.ScenePath);
			else if (resource is WeaponConfig weapon)
			{
				// 武器使用所属角色的动画库；不同角色换骨架后不能继续借用索引第一行。
				var player = index.Characters.FirstOrDefault(c => c != null && c.Id == weapon.OwnerCharacter?.Id);
				if (player == null) Add(weapon, "OwnerCharacter", "PlayerLoadout", "武器未指定已索引的归属角色", "选择该武器实际所属的角色行");
				ValidateSkills(weapon, weapon.Skills, "res://Scenes/Player/Player.tscn", player?.AnimLibraryDir ?? string.Empty);
			}
			if (resource is StageDefinition stage)
				foreach (string id in stage.AllowedEnemyIds)
					if (!index.Enemies.Any(e => e != null && !e.IsGlobal && e.Id == id)) Add(stage, "AllowedEnemyIds", "SpawnPlanner", $"敌人 {id} 不在索引中", "指定已索引的非全局敌人 Id");
			if (resource is SkillAnimationConfig animation && animation.EventNames.Length != animation.EventTimes.Length)
				Add(animation, "EventNames / EventTimes", "SkillAnimationGenerator", "事件与时刻数量不一致", "逐项对应事件名和时刻");
		}
		if (scanUnindexed)
			foreach (string path in ResourceFiles("res://Config/Tables"))
				if (LoadAuthoredRow(path) is { } row && !indexed.Contains(Identity(row))) Add(row, "索引", "ConfigIndexData", "制作资源未列入显式索引", "将该行加入 Config/ConfigIndex.tres 的对应数组；废弃资源移出 Tables");
		return issues;

		void Add(Resource row, string field, string consumer, string reason, string fix) => issues.Add(new(string.IsNullOrEmpty(row.ResourcePath) ? $"<memory:{row.Get("Id").AsString()}>" : row.ResourcePath, field, consumer, reason, fix));
		void ValidateNumeric(Resource row)
		{
			if (row is not INumericContentIdentity identity) return;
			if (identity.NumericId <= 0) Add(row, "NumericId", "ConfigRows.ByNumericId", "数字ID必须为正", "用assign_content_ids.py分配全局唯一数字ID");
			else if (numericIds.TryGetValue(identity.NumericId, out var prior))
			{
				if (Identity(prior) != Identity(row)) Add(row, "NumericId", "ConfigRows.ByNumericId", $"数字ID与{prior.ResourcePath}重复", "分配新数字ID，不复用已保留编号");
			}
			else numericIds.Add(identity.NumericId, row);
		}
		void Visit(Resource current, Resource owner, string prefix, HashSet<ulong> visited)
		{
			if (!visited.Add(current.GetInstanceId())) return;
			ValidateNumeric(current);
			if (current is IContentValidatable definition)
				foreach (string error in definition.Validate()) Add(owner, prefix.TrimEnd('.'), current.GetType().Name, error, "修正该内容定义的专用参数或引用");
			foreach (PropertyInfo property in current.GetType().GetProperties().Where(p => p.IsDefined(typeof(ExportAttribute))))
			{
				object? value = property.GetValue(current); string field = prefix + property.Name;
				if (value is Resource child) Reference(child, field);
				else if (value is IEnumerable sequence && value is not string)
				{
					int n = 0; foreach (object? item in sequence) { if (item is Resource nested) Reference(nested, $"{field}[{n}]"); n++; }
				}
			}
			void Reference(Resource target, string field)
			{
				if (target is ConfigRow && !indexed.Contains(Identity(target))) Add(owner, field, current.GetType().Name, "引用的配置行未进入索引", "把被引用行加入对应索引数组");
				else if (target is not ConfigRow) Visit(target, owner, field + ".", visited);
			}
		}
		void ValidateUnit(UnitConfig unit, string scene) => ValidateSkills(unit, unit.Skills, scene, unit.AnimLibraryDir);
		void ValidateSkills(Resource owner, AbilityConfig[] skills, string scene, string animationDirectory)
		{
			if (string.IsNullOrEmpty(scene) || !ResourceLoader.Exists(scene)) { Add(owner, "ScenePath", "UnitConfig", $"预制体不存在：{scene}", "指定可加载的单位场景"); return; }
			Node? root = null;
			try
			{
				root = RuntimeResources.Load<PackedScene>(scene)?.Instantiate();
				foreach (AbilityConfig skill in skills.Where(s => s != null))
				{
					if (root?.GetNodeOrNull<Ability>(skill.NodeName) is not { } ability) Add(owner, $"Skills.{skill.Id}.NodeName", "UnitConfig.ApplySkills", $"场景 {scene} 缺少能力节点 {skill.NodeName}", "在预制体安装该能力或修正技能行 NodeName");
					else if (!string.IsNullOrEmpty(skill.ClassHint) && ability.GetType().Name != skill.ClassHint) Add(owner, $"Skills.{skill.Id}.ClassHint", "AbilityConfig.ApplyTo", $"节点类型为 {ability.GetType().Name}，期望 {skill.ClassHint}", "修正能力节点类型或配置的类型声明");
					else
					{
						foreach (ParamEntry parameter in skill.Params)
							if (parameter == null || string.IsNullOrEmpty(parameter.Key) || ability.Get(parameter.Key).VariantType == Variant.Type.Nil) Add(owner, $"Skills.{skill.Id}.Params", "AbilityConfig.ApplyTo", $"能力没有参数 {parameter?.Key}", "填写实际能力导出的字段，优先使用强类型参数资源");
					}
					var names = skill.ComboSeries?.ComboAnimations.ToArray() ?? (string.IsNullOrEmpty(skill.AnimName) ? Array.Empty<string>() : new[] { skill.AnimName });
					foreach (string name in names)
					{
						string path = $"{animationDirectory.TrimEnd('/')}/{name.Split('/').Last()}.tres";
						var animation = ResourceLoader.Exists(path) ? RuntimeResources.Load<Animation>(path) : null;
						if (animation == null) { Add(owner, $"Skills.{skill.Id}.AnimName", "SkillAnimationLibrary.ResolveExact", $"缺少动画 {path}", "生成对应技能动画并检查单位的 AnimLibraryDir"); continue; }
						foreach (string evt in new[] { skill.AnimEvent, skill.AnimEventEnd }.Where(e => !string.IsNullOrEmpty(e)))
							if (!HasEvent(animation, evt)) Add(owner, $"Skills.{skill.Id}.AnimEvent", "Ability.OnAnimEvent", $"动画 {path} 没有启用的 anim_event({evt}) 方法帧", "在动画方法轨道补正确事件并重新生成，普通播放成功不算通过");
					}
				}
			}
			finally { root?.Free(); }
		}
	}
	public static bool HasEvent(Animation animation, string expected)
	{
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Method || !animation.TrackIsEnabled(track)) continue;
			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
			{
				var call = animation.TrackGetKeyValue(track, key).AsGodotDictionary();
				if (call.TryGetValue("method", out Variant method) && method.AsString() == "anim_event" && call.TryGetValue("args", out Variant args) && args.AsGodotArray().Count > 0 && args.AsGodotArray()[0].AsString() == expected) return true;
			}
		}
		return false;
	}
	private static string Identity(Resource row) => string.IsNullOrEmpty(row.ResourcePath)
		? $"instance:{row.GetInstanceId()}" : $"{row.GetType().Name}:{row.ResourcePath}:{row.Get("Id").AsString()}";
	private static IEnumerable<string> ResourceFiles(string path)
	{
		using var directory = DirAccess.Open(path);
		if (directory == null) yield break;
		foreach (string file in directory.GetFiles()) if (file.EndsWith(".tres", StringComparison.Ordinal)) yield return path + "/" + file;
		foreach (string child in directory.GetDirectories().Where(s => !s.StartsWith('.'))) foreach (string file in ResourceFiles(path + "/" + child)) yield return file;
	}
}
