using System.Collections.Generic;

namespace ActRoguelike.Config;

/// <summary>逻辑条件只用稳定 Id；旧文档词条由资源声明别名，绝不读取翻译后的显示名。</summary>
public static class ContentIdentity
{
	private static readonly Dictionary<string, string> Prerequisites = new(System.StringComparer.Ordinal);
	public static void Rebuild(IEnumerable<RuneConfig> runes)
	{
		Prerequisites.Clear();
		foreach (RuneConfig rune in runes)
		{
			if (rune == null || string.IsNullOrEmpty(rune.Id)) continue;
			Prerequisites.TryAdd(rune.Id, rune.Id);
			foreach (string alias in rune.LegacyPrerequisiteAliases) if (!string.IsNullOrEmpty(alias)) Prerequisites.TryAdd(alias, rune.Id);
		}
	}
	public static string Prerequisite(string token) => Prerequisites.TryGetValue(token, out string? id) ? id : token;
}
