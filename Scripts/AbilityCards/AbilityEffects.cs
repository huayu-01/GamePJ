using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.AbilityCards;

/// <summary>
/// 能力卡的**生效层**：把"激活中的卡"套到玩家身上（本局开局一次）。
///
/// <para>调用点只有一处 —— <c>RunController.ApplyBalance()</c> 的末尾（1 行），
/// 也就是"角色数值 / 技能表 / 能力开关矩阵都套完之后"。顺序是刻意的：
/// 角色的 <c>DisabledAbilityIds</c> 会关掉召唤/控制能力，而一张"授予技能"的卡应该**赢过**它
/// （卡 = 玩家花局外资源换来的额外能力）。</para>
///
/// <para>幂等：所有修饰都挂在一个**专用 Source</b> 上，进函数先 <c>RemoveBySource</c>，
/// 所以换角色 / 重开一局重复调用不会叠加（同一张卡不会 ×2）。</para>
/// </summary>
public static class AbilityEffects
{
	/// <summary>成组移除用的来源标记（<see cref="StatSheet.RemoveBySource"/>）。</summary>
	private static readonly object SourceToken = new();

	/// <summary>本局最后一次套用的效果条数（自检可读）。</summary>
	public static int LastAppliedCount { get; private set; }

	/// <summary>本局最后一次套用的效果文本（自检可读）。</summary>
	public static string LastSummary { get; private set; } = string.Empty;

	/// <summary>
	/// 把激活中的卡套到玩家身上：加修饰（<c>StatAdd</c> / <c>StatMultiply</c>）+ 授予技能
	/// （<c>GrantSkill</c>），然后重算派生属性并回满（开局语义：<c>ApplyBaseStats</c> 本来就回满）。
	/// </summary>
	/// <param name="player">玩家单位。</param>
	/// <param name="owner">日志里记的调用方（就是 RunController）。</param>
	public static void ApplyToPlayer(Unit player, Node owner)
	{
		ApplyCore(player);
		GameLog.Write(
			"Ability",
			$"能力卡：激活 {AbilityStore.ActiveCount}/{AbilityStore.Rows.Count} 张 · 额度 {AbilityStore.QuotaUsed}/{AbilityStore.QuotaCap}"
			+ $" · 碎片 {Run.MetaProgress.Shards} —— {(LastAppliedCount == 0 ? "本局没有激活的卡（全部效果为空）" : LastSummary)}");
	}

	/// <summary>真正的套用（不含日志，自检的"预览"也走它，所以测的就是同一条路径）。</summary>
	private static void ApplyCore(Unit player)
	{
		AbilityStore.Load();
		player.Stats.RemoveBySource(SourceToken);

		var applied = new List<string>();
		foreach (AbilityCardConfig row in AbilityStore.Rows)
		{
			if (row == null || !AbilityStore.IsActive(row.Id))
			{
				continue;
			}

			int level = Mathf.Max(1, AbilityStore.LevelOf(row.Id));
			switch (row.Effect)
			{
				case AbilityCardEffectKind.StatAdd when !string.IsNullOrEmpty(row.Stat):
					player.Stats.AddModifier(new StatModifier
					{
						Stat = row.Stat,
						Operation = ModifierOperation.Add,
						Value = row.PerLevel * level,
						Source = SourceToken,
					});
					applied.Add($"{row.DisplayName} {row.EffectText(level)}");
					break;

				case AbilityCardEffectKind.StatMultiply when !string.IsNullOrEmpty(row.Stat):
					player.Stats.AddModifier(new StatModifier
					{
						Stat = row.Stat,
						Operation = ModifierOperation.Multiply,
						Value = 1f + (row.PerLevel * level),
						Source = SourceToken,
					});
					applied.Add($"{row.DisplayName} {row.EffectText(level)}");
					break;

				case AbilityCardEffectKind.GrantSkill:
					applied.Add(GrantSkill(player, row, level));
					break;

				default:
					applied.Add($"{row.DisplayName}（效果 {row.Effect} 没填属性名 → 跳过）");
					break;
			}
		}

		player.RefreshStats();
		player.FillVitals();
		LastAppliedCount = applied.Count;
		LastSummary = string.Join("、", applied);
	}

	/// <summary>
	/// 授予技能：把卡指向的**技能行**套到玩家身上那个能力节点上并**启用**它。
	///
	/// <para>三个动作缺一不可：① <see cref="ConfigRow.ApplyTo"/>（冷却/前摇/后摇/朝向/子类参数/命中体）；
	/// ② <c>Enabled = true</c>（角色表的能力开关矩阵会把它关掉，卡的语义是"我买下了它"）；
	/// ③ 按卡的等级**降冷却**（升级对"授予技能"卡的实际意义）。</para>
	/// </summary>
	private static string GrantSkill(Unit player, AbilityCardConfig row, int level)
	{
		if (row.Skill == null)
		{
			return $"{row.DisplayName}（卡里没填技能行 → 跳过）";
		}

		string nodeName = row.Skill.NodeName;
		Node? found = string.IsNullOrEmpty(nodeName) ? null : player.FindChild(nodeName, true, false);
		if (found is not Ability ability)
		{
			return $"{row.DisplayName}（玩家身上找不到能力节点 {nodeName} → 跳过）";
		}

		row.Skill.ApplyTo(ability);
		ability.Enabled = true;

		float cooldown = row.GrantedCooldown(level);
		if (cooldown > 0f)
		{
			ability.Cooldown = cooldown;
		}

		return $"{row.DisplayName} 授予 {row.Skill.Id}（{nodeName}，冷却 {ability.Cooldown:0.##}s）";
	}

	/// <summary>
	/// **自检 / 探针用**：临时激活一张数值卡（2 级）与一张授予技能卡，量出"激活到底改变了什么"，
	/// 然后**逐项还原**（修饰、卡的激活状态、被授予能力的三个字段），返回一行可贴进日志的证据。
	/// 全程 <c>save:false</c>，不碰玩家的存档。
	/// </summary>
	public static string DebugPreviewLine(Unit player)
	{
		AbilityStore.Load();

		AbilityCardConfig? statCard = null;
		AbilityCardConfig? grantCard = null;
		foreach (AbilityCardConfig row in AbilityStore.Rows)
		{
			if (row == null)
			{
				continue;
			}

			if (statCard == null && row.Effect == AbilityCardEffectKind.StatAdd && !string.IsNullOrEmpty(row.Stat))
			{
				statCard = row;
			}

			if (grantCard == null && row.Effect == AbilityCardEffectKind.GrantSkill && row.Skill != null)
			{
				grantCard = row;
			}
		}

		if (statCard == null && grantCard == null)
		{
			return "能力卡实测：表里没有可测的卡（期望至少有一张数值卡或授予技能卡）。";
		}

		// ① 记下会动到的一切。
		var snapshot = new List<(string Id, int Level, bool Active)>();
		foreach (AbilityCardConfig row in AbilityStore.Rows)
		{
			if (row != null)
			{
				snapshot.Add((row.Id, AbilityStore.LevelOf(row.Id), AbilityStore.IsActive(row.Id)));
			}
		}

		Ability? granted = null;
		bool grantedEnabled = false;
		string grantedId = string.Empty;
		float grantedCooldown = 0f;
		if (grantCard?.Skill != null)
		{
			granted = player.FindChild(grantCard.Skill.NodeName, true, false) as Ability;
			if (granted != null)
			{
				grantedEnabled = granted.Enabled;
				grantedId = granted.AbilityId;
				grantedCooldown = granted.Cooldown;
			}
		}

		float healthBefore = player.MaxHealth;
		string statText = "（没有数值卡）";
		string grantText = "（没有授予技能卡）";

		try
		{
			if (statCard != null)
			{
				AbilityStore.DebugSetState(statCard.Id, 2, true);
			}

			if (grantCard != null)
			{
				AbilityStore.DebugSetState(grantCard.Id, 1, true);
			}

			ApplyCore(player);

			float healthAfter = player.MaxHealth;
			if (statCard != null)
			{
				statText =
					$"{statCard.DisplayName}（{statCard.Id} Lv2）{statCard.EffectText(2)} → 最大生命 {healthBefore:0} → {healthAfter:0}"
					+ $"（{(healthAfter - healthBefore >= 0 ? "+" : string.Empty)}{healthAfter - healthBefore:0}）";
			}

			if (grantCard?.Skill != null)
			{
				grantText = granted == null
					? $"{grantCard.DisplayName}：玩家身上找不到能力节点 {grantCard.Skill.NodeName}"
					: $"{grantCard.DisplayName}（{grantCard.Id} Lv1）授予 {grantCard.Skill.Id} →"
						+ $" {grantCard.Skill.NodeName}.Enabled {grantedEnabled} → {granted.Enabled}"
						+ $"（AbilityId「{grantedId}」→「{granted.AbilityId}」，冷却 {grantedCooldown:0.##} → {granted.Cooldown:0.##}）";
			}
		}
		finally
		{
			// ② 还原：先摘修饰、再还原卡状态与被授予能力的字段。
			player.Stats.RemoveBySource(SourceToken);
			player.RefreshStats();
			foreach ((string id, int level, bool active) in snapshot)
			{
				AbilityStore.DebugSetState(id, level, active);
			}

			if (granted != null)
			{
				granted.Enabled = grantedEnabled;
				granted.AbilityId = grantedId;
				granted.Cooldown = grantedCooldown;
			}
		}

		return $"能力卡实测（激活真的影响局内）：{statText} ｜ {grantText}"
			+ $" ｜ 清理后最大生命 {player.MaxHealth:0}（期望还原到 {healthBefore:0}）";
	}
}
