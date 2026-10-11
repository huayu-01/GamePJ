using System.Collections.Generic;
using ActRoguelike.Boss;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 首领词条在界面上的**统一写法**（HUD 血条栏与首领预览面板共用一份，避免两处各写一套、口径漂移）。
///
/// 两套来源在显示上也要能分开（关卡核心文档 §7.1：Boss Pressure 与 Boss Build 不得合并成一个
/// 无法区分来源的池）：
/// <list type="bullet">
/// <item><b>◆ 行为型</b>（Boss Build，`Config/Tables/Skills/bhv_*.tres`）—— 首领实际获得的战斗行为/技能，
///   来自 <c>EncounterController.BossPerks</c>。</item>
/// <item><b>◇ 数值型</b>（Pressure，`Config/Tables/Pressure/pmod_*.tres`）—— 阈值转换抽到的数值强化，
///   来自 <c>StageController.PressurePerks</c>。</item>
/// </list>
///
/// **可读性约定**（用户要求"词条多了不能挤爆血条栏"）：
/// ① 同名词条折叠成 <c>名字×N</c>（Pressure 池会重复抽到同一条，例如 疾行×2）；
/// ② 显示的**不同词条**数量有上限，超出部分折叠成 <c>…+N</c>（N = 被藏起来的条数，重复也计入）。
/// </summary>
public static class BossPerkText
{
	/// <summary>行为型（Boss Build）的标记。</summary>
	public const string BehaviorMark = "◆";

	/// <summary>数值型（Pressure）的标记。</summary>
	public const string PressureMark = "◇";

	/// <summary>两类词条的总条数（重复也算，用于"词条 4"这种计数）。</summary>
	public static int Total(IReadOnlyList<BossPerk>? behavior, IReadOnlyList<BossPerk>? pressure)
		=> (behavior?.Count ?? 0) + (pressure?.Count ?? 0);

	/// <summary>
	/// 压成**一行**显示（HUD 血条栏用）：行为型在前（它们是首领的"身份"），数值型在后；
	/// 同名折叠、超过 <paramref name="max"/> 个不同词条就截断成 <c>…+N</c>。没有词条 → 空字符串。
	/// </summary>
	public static string FormatLine(IReadOnlyList<BossPerk>? behavior, IReadOnlyList<BossPerk>? pressure, int max)
	{
		List<string> marked = MarkedNames(behavior, pressure);
		if (marked.Count == 0)
		{
			return string.Empty;
		}

		List<(string Name, int Count)> folded = Fold(marked);
		int shown = Mathf.Min(Mathf.Max(1, max), folded.Count);
		var parts = new List<string>(shown + 1);
		int shownCount = 0;
		for (int i = 0; i < shown; i++)
		{
			(string name, int count) = folded[i];
			parts.Add(count > 1 ? $"{name}×{count}" : name);
			shownCount += count;
		}

		if (marked.Count > shownCount)
		{
			parts.Add($"…+{marked.Count - shownCount}");
		}

		return string.Join(" ", parts);
	}

	/// <summary>
	/// 铺成**多行**（首领预览面板用）：每条一行，带名字与说明（说明来自 `.tres` 的
	/// `DescriptionTemplate`，配置表阶段 3 起就有，以前没人显示）。
	/// </summary>
	public static string Lines(IReadOnlyList<BossPerk>? perks, string mark)
	{
		if (perks == null || perks.Count == 0)
		{
			return string.Empty;
		}

		var text = new System.Text.StringBuilder();
		foreach ((string name, int count) in Fold(Mark(perks, mark)))
		{
			BossPerk? sample = First(perks, name, mark);
			string repeat = count > 1 ? $" ×{count}" : string.Empty;
			string description = string.IsNullOrEmpty(sample?.Description) ? string.Empty : $" —— {sample!.Description}";
			text.Append($"      {mark} {sample?.Name ?? Strip(name)}{repeat}{description}\n");
		}

		return text.ToString();
	}

	/// <summary>两类词条按"行为型在前"的显示顺序拼成带标记的名字列表。</summary>
	private static List<string> MarkedNames(IReadOnlyList<BossPerk>? behavior, IReadOnlyList<BossPerk>? pressure)
	{
		var marked = new List<string>();
		Append(marked, behavior, BehaviorMark);
		Append(marked, pressure, PressureMark);
		return marked;
	}

	private static void Append(List<string> target, IReadOnlyList<BossPerk>? perks, string mark)
	{
		if (perks == null)
		{
			return;
		}

		foreach (BossPerk perk in perks)
		{
			if (perk != null)
			{
				target.Add($"{mark}{perk.Name}");
			}
		}
	}

	private static List<string> Mark(IReadOnlyList<BossPerk> perks, string mark)
	{
		var marked = new List<string>(perks.Count);
		Append(marked, perks, mark);
		return marked;
	}

	/// <summary>同名的折叠成一项（按首次出现排序），保留出现次数。</summary>
	private static List<(string Name, int Count)> Fold(List<string> marked)
	{
		var order = new List<string>();
		var counts = new Dictionary<string, int>();
		foreach (string name in marked)
		{
			if (!counts.ContainsKey(name))
			{
				order.Add(name);
			}

			counts[name] = counts.TryGetValue(name, out int seen) ? seen + 1 : 1;
		}

		var folded = new List<(string, int)>(order.Count);
		foreach (string name in order)
		{
			folded.Add((name, counts[name]));
		}

		return folded;
	}

	private static BossPerk? First(IReadOnlyList<BossPerk> perks, string markedName, string mark)
	{
		foreach (BossPerk perk in perks)
		{
			if (perk != null && $"{mark}{perk.Name}" == markedName)
			{
				return perk;
			}
		}

		return null;
	}

	private static string Strip(string markedName) => markedName.Length > 1 ? markedName[1..] : markedName;
}
