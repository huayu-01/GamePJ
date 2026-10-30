using System.Globalization;
using Godot;

namespace ActRoguelike.Config;

public interface INumericContentIdentity
{
	int NumericId { get; set; }
	string Id { get; set; }
}

/// <summary>数字是新内容身份；旧语义键只为既有存档、条件与脚本保留兼容入口。</summary>
public static class NumericContentIdentity
{
	public static string Key(string legacy, int number) => !string.IsNullOrEmpty(legacy) ? legacy : number > 0 ? number.ToString(CultureInfo.InvariantCulture) : string.Empty;
	public static string ResourceName(string type, int number, string extension = "tres")
	{
		if (number <= 0 || string.IsNullOrWhiteSpace(type) || type.IndexOfAny(new[] { '/', '\\', '.', ':' }) >= 0)
			throw new System.ArgumentException("资源类型必须是文件名前缀，数字ID必须为正。");
		return $"{type}_{number}.{extension.TrimStart('.')}";
	}
	public static void HideLegacy(Godot.Collections.Dictionary property)
	{
		if (property["name"].AsString() == "Id") property["usage"] = (long)((PropertyUsageFlags)property["usage"].AsInt64() & ~PropertyUsageFlags.Editor);
	}
}
