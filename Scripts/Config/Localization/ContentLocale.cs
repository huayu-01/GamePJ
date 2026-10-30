using Godot;

namespace ActRoguelike.Config;

/// <summary>整句文案目录；Id、事件、属性键与存档键永远不放进译文。</summary>
[Tool, GlobalClass]
public partial class ContentLocale : Resource
{
	[Export] public string Locale { get; set; } = "zh";
	/// <summary>正式语言要求完整词条；示例／翻译中的目录可显式允许原文回退。</summary>
	[Export] public bool RequireComplete { get; set; } = true;
	[Export] public Godot.Collections.Dictionary<string, string> Messages { get; set; } = new();
}
