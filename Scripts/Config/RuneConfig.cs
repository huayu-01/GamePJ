using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **符文表**的一行 —— 一个 `.tres` 文件就是一枚符文（`Config/Tables/Runes/rune_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Runes"/>。
///
/// <para><b>符文是什么（用户 2026-10-17 澄清的原话）</b>：
/// 「就是哈迪斯那样的，原文想表达的意思就是携带了这个东西之后，会产生些效果，比如雷之印，
/// 这个效果就是最大生命值受限，仅此而已，然后新增的增幅里，有一些它是有前置，要求雷之印，
/// 所以携带了这个雷之印等于满足它们的前置要求」。</para>
///
/// <para>所以一枚符文 = <b>两件事</b>：</para>
/// <list type="number">
/// <item><b>一个效果</b>（多为负面，例如「最大生命值限制为 30」「受到伤害 +300%」）——
///   走**既有的** `RewardEffectRegistry` 处理器通路（本类继承 <see cref="RewardConfig"/>，
///   所以 `ApplyHandler` / `Modifiers` / `HandlerParams` 三列与增幅项行**完全同构**）；</item>
/// <item><b>解锁一批前置条件</b>（<see cref="UnlocksRequires"/>）—— 携带本符文时，
///   所有 `Requires` 命中这些字符串的增幅项都视为"前置已满足"。
///   判定点在 `RewardPool.HasPrerequisites`，**不携带任何符文时那一层逐位不变**。</item>
/// </list>
///
/// <para>⚠️ <b>为什么继承 <see cref="RewardConfig"/> 而不是另写一份字段</b>：用户的要求是
/// 「负面效果走既有的 handler 通路、能复用就复用」。处理器注册表的签名是
/// <c>Handler(RewardConfig row)</c>，继承之后 <c>RewardEffectRegistry.Compile(row)</c> 可以直接编译符文行
/// —— 一行新代码都不用加，`StatSet` / `GrantArmor` / `RuneEffect` 全部对符文可用，
/// 也不会出现"增幅项一套 handler、符文第二套 handler"这种迟早漂移的第二真源。</para>
///
/// <para>⚠️ 符文与增幅项**不是同一张表**（`Runes` 与 `Upgrades` 是两个索引数组）：
/// 符文是"开局携带一件、不进奖励池"（哈迪斯的信物），增幅项是"局内抽到的祝福"。
/// 混进同一张表会让"增幅项 62 条"这个读数与奖励池候选数漂掉。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class RuneConfig : RewardConfig
{
	[ExportGroup("符文 · 携带时满足的前置条件")]
	/// <summary>
	/// 携带本符文时**被满足的前置条件名单**（例如「雷之印」填 <c>["雷之印"]</c>）。
	///
	/// <para>判定方式：增幅项行 `RewardConfig.Requires` 里的每一项，只要落在这份名单里
	/// **或者等于本符文行的 Id**，就当作"已经拥有"（`RunBuild.SatisfiesPrerequisite`）。
	/// 两者都认，是因为表里写前置时有两种自然写法：写符文行的 Id（`rune_lei_seal`）
	/// 或写文档里的派系名（`雷之印`）—— 用户原话举的例子就是后者。</para>
	///
	/// <para>⚠️ 用 `Godot.Collections.Array&lt;string&gt;` 而不是"一整串文本 + 运行期解析"：
	/// `architecture.md` 第 12 条（P8 立的规矩）。</para>
	///
	/// <para>空 = 本符文不解锁任何前置（默认，例如「行军」「大力符」这类纯数值符文）。</para>
	/// </summary>
	[Export] public Godot.Collections.Array<string> UnlocksRequires { get; set; } = new();

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
