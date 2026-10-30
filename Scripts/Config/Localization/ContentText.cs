using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>先翻译完整模板再填参数。目录由索引显式引用，导出包无需扫描目录。</summary>
public static class ContentText
{
	private static readonly List<Translation> Installed = new();
	public static string Locale => TranslationServer.GetLocale();
	public static string Revision => _revision + ":" + Locale;
	private static int _revision;
	public static void Initialize(IEnumerable<ContentLocale> locales)
	{
		bool firstLoad = _revision == 0;
		foreach (Translation old in Installed) { TranslationServer.RemoveTranslation(old); old.Dispose(); }
		Installed.Clear();
		foreach (ContentLocale catalog in locales)
		{
			if (catalog == null || string.IsNullOrWhiteSpace(catalog.Locale)) continue;
			var translation = new Translation { Locale = catalog.Locale };
			foreach (var entry in catalog.Messages) translation.AddMessage(entry.Key, entry.Value);
			TranslationServer.AddTranslation(translation); Installed.Add(translation);
		}
		_revision++;
		if (firstLoad) SetLocale("zh");
	}
	public static void SetLocale(string locale) => TranslationServer.SetLocale(locale);
	public static string Get(string key, string fallback)
	{
		if (string.IsNullOrEmpty(key)) return fallback;
		string translated = TranslationServer.Translate(key);
		return translated == key ? fallback : translated;
	}
	public static string Name(ConfigRow row) => Get(row.NameKey, string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName);
	public static string Description(ConfigRow row, IReadOnlyList<ParamEntry>? parameters = null)
		=> ConfigText.Format(Get(row.DescriptionKey, row.DescriptionTemplate), row.RuntimeDescriptionSource ?? row, parameters ?? row.Params);
}
