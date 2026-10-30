using System.IO;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>验证图片统一写入证据目录；旧截图调用保留名称，不再覆盖历史文档配图。</summary>
internal static class ValidationArtifacts
{
	public static string Path(string requested)
	{
		const string oldPrefix = "res://docs/";
		string path = requested;
		if (path.StartsWith(oldPrefix, System.StringComparison.Ordinal) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
			path = "res://Logs/evidence/" + path[oldPrefix.Length..];
		else
		{
			string oldDirectory = ProjectSettings.GlobalizePath(oldPrefix);
			if (path.StartsWith(oldDirectory, System.StringComparison.OrdinalIgnoreCase) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
				path = ProjectSettings.GlobalizePath("res://Logs/evidence/") + path[oldDirectory.Length..];
		}
		string absolute = ProjectSettings.GlobalizePath(path);
		if (System.IO.Path.GetDirectoryName(absolute) is { Length: > 0 } directory) Directory.CreateDirectory(directory);
		return absolute;
	}
}
