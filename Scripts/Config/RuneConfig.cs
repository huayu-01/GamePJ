using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 基地一次携带一枚的符文行，不进入局内奖励池。
/// Effects与增幅共用编译、实例、事务和来源清理；UnlocksRequires声明稳定前置Id。
/// 旧中文条件由LegacyPrerequisiteAliases兼容，显示名及翻译不参与身份判定。
/// 新增同类符文只需制作行资源并加入ConfigIndex.Runes，步骤见docs/rune-system.md。
/// </summary>
[Tool]
[GlobalClass]
public partial class RuneConfig : RewardConfig
{
	[ExportGroup("符文 · 携带时满足的前置条件")]
	/// <summary>
	/// 携带本符文时被满足的稳定前置 Id（例如雷之印填 <c>["rune_lei_seal"]</c>）。
	///
	/// <para>判定方式：增幅项行 `RewardConfig.Requires` 里的每一项，只要落在这份名单里
	/// **或者等于本符文行的 Id**，就当作"已经拥有"（`RunBuild.SatisfiesPrerequisite`）。
	/// 旧文档的派系名通过 LegacyPrerequisiteAliases 映射，不能把翻译后的显示名作为逻辑身份。</para>
	///
	/// <para>⚠️ 用 `Godot.Collections.Array&lt;string&gt;` 而不是"一整串文本 + 运行期解析"：
	/// `architecture.md` 第 12 条（P8 立的规矩）。</para>
	///
	/// <para>空 = 本符文不解锁任何前置（默认，例如「行军」「大力符」这类纯数值符文）。</para>
	/// </summary>
	[Export] public Godot.Collections.Array<string> UnlocksRequires { get; set; } = new();
	/// <summary>仅兼容旧配置的条件词；新前置统一填写稳定 Id，与显示名和语言无关。</summary>
	[Export] public string[] LegacyPrerequisiteAliases { get; set; } = System.Array.Empty<string>();

	[ExportGroup("符文 · 表现")]
	/// <summary>
	/// 图标资源路径（`res://` 开头）。
	/// 基地选择面板直接读这一列，留空时只显示符文名。
	/// </summary>
	[Export(PropertyHint.File, "*.png,*.svg,*.webp")] public string IconPath { get; set; } = string.Empty;

	/// <summary>这份前置解锁名单的只读快照（判定与日志用；不含本行 Id —— 那个在 `RunBuild` 里补）。</summary>
	public string[] UnlockList()
	{
		string[] list = new string[UnlocksRequires.Count];
		for (int i = 0; i < UnlocksRequires.Count; i++)
		{
			list[i] = UnlocksRequires[i];
		}

		return list;
	}
}
