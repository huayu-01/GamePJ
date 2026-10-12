using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 首领表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Bosses/boss_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Bosses"/>。
///
/// 首领与普通敌人共用同一套**通用单位参数**（<see cref="UnitConfig"/>：攻击 / 生命 / 移速 /
/// 持有技能 / 行为&amp;类 / 表现），这里只加首领私有的两列：
/// 预制体路径（与 `Main.tscn` 的 `BossScene` / `EliteBossScene` 配对）与白板首领副本的血量倍率。
///
/// ⚠️ 首领的"行为"分两处，**不要混**（关卡核心文档 §7.1）：
/// <list type="bullet">
/// <item>它**会什么技能** → 本行的 <see cref="UnitConfig.Skills"/>（普通技能行）。</item>
/// <item>它**开局额外获得什么技能**（Boss Build，界面上的"技能"）→ 技能表里 <c>ApplyHandler</c> 非空的那些行。</item>
/// <item>它的**数值型强化**（Pressure）→ `Config/Tables/Pressure/`，与本表无关。</item>
/// </list>
/// </summary>
[Tool]
[GlobalClass]
public partial class BossConfig : UnitConfig
{
	[ExportGroup("首领（预制体 + 白板副本血量）")]
	/// <summary>首领预制体路径（与 `Main.tscn` 的 BossScene / EliteBossScene 按路径配对）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "res://Scenes/Enemies/Boss.tscn";

	/// <summary>
	/// 「白板首领副本」的血量倍率（相对普通首领；关卡核心文档 L231/L315/L317）。
	/// 以前这个值在 `ConfigIndex.tres` 里有一份**没有消费者**的副本（改了不起作用），
	/// 实际生效的是 `EncounterController.EliteBossHealthScale`（场景导出，默认 0.35）。
	/// 现在统一到这里：行是权威，读不到才用场景导出的值。
	/// </summary>
	[Export(PropertyHint.Range, "0.05,1,0.05")] public float EliteCopyHealthScale { get; set; } = 0.35f;
}
