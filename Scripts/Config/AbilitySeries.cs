using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **技能的一串「每段一份」的表参数**（P4 新增，**P8 改成正规数组列**）—— 角色 B 三把近战武器
/// 武技1 的连招：**每段动画名 / 每段命中盒 / 每段伤害倍率 / 斩断子弹的名单**。
///
/// <para><b>它是一个独立资源，而不是技能行上的 7 个数组列</b>：一张技能行已经有 20+ 列，
/// 再塞 7 个数组进去 Inspector 会拉成一根长条；而「连招」本身是一对一的独立概念
/// （<b>一行 = 一个技能的连招</b>）。技能行用 <see cref="AbilityConfig.ComboSeries"/>
/// 以**资源引用**指过来（不是字符串路径、也不是"按技能行 Id 约定路径 load" —— 那两条 P8 已删）。</para>
///
/// <para>⚠️ <b>P4 的旧注释是误诊，已纠正</b>：它写着「<c>[Export] float[]</c> 编译不过（<c>GD0102</c>）、
/// <c>Godot.Collections.Array&lt;T&gt;</c> 读回来每一项都是 <c>null</c>」，所以整串落进一个
/// <c>[Export] string</c>（<c>名=值1|值2;…</c>）由运行期 <c>ParseFloats/ParseTexts/ParseVectors</c> 解析。
/// **P8 在已重启的编辑器/运行期下逐条实测：数组列能导出、能写盘、能读回，项数与每一项都对**
/// （那个「读回来全是 null」的真身是"编辑器加载了旧 C# 程序集"）。
/// 原始证据（探针输出原文）见 `docs/abilities.md` §11。文本解析那一套**已删除** ——
/// 数据要一眼看得懂，**宁可老老实实加一个列**。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilitySeries : ConfigRow
{
	[ExportGroup("连招 · 每段一段动画")]
	/// <summary>
	/// 第 1..N 段各自的**技能动画名**（`Skill_B1_Sword1`…）。<b>空 = 没有连招</b>
	/// （调用方 <c>MeleeComboAbility</c> 退化成"一段"：用技能行的 <c>AnimName</c>）。
	/// </summary>
	[Export] public Godot.Collections.Array<string> ComboAnimations { get; set; } = new();

	/// <summary>
	/// 开输入窗口的那个事件帧名（默认 <c>ATK</c>；文档：武技1「连续按键可连续释放」）。
	/// **留空 = 不覆盖**（保留能力节点上的值；空值 = 不开窗口，只能打一段）。
	/// </summary>
	[Export] public string ComboNextEvent { get; set; } = "ATK";

	/// <summary>输入窗口时长（秒）。<b>&lt;= 0 = 不覆盖</b>（保留能力节点上的值）。</summary>
	[Export] public float ComboWindowSeconds { get; set; } = 0.45f;

	/// <summary>
	/// 打满最后一段之后**是否回到第 0 段**（长枪「横扫千军：挥砍&gt;上挑&gt;下劈&gt;突刺&gt;挥砍，这么循环」= true）。
	/// ⚠️ <c>bool</c> 列**没有"不覆盖"这一说**：<c>false</c> 就是"不循环"，与它是否被填过无关。
	/// </summary>
	[Export] public bool ComboLoop { get; set; }

	[ExportGroup("连招 · 每段的判定（一项对应一段；不足时用最后一项或场景基线）")]
	/// <summary>第 i 段的**伤害倍率**（乘在命中盒的 <c>Damage</c> 上）。空 = 全部按 1 算。</summary>
	[Export] public Godot.Collections.Array<float> StepDamageScales { get; set; } = new();

	/// <summary>
	/// 第 i 段的**命中盒尺寸**（米；x 宽 / y 高 / z 长）。
	/// <c>z = 0</c> 且 <c>x == y</c> 时命中盒切成**圆柱**（镰刀最后那一刀"大回旋" = 360°）。
	/// </summary>
	[Export] public Godot.Collections.Array<Vector3> StepBoxSizes { get; set; } = new();

	/// <summary>第 i 段的**命中盒前伸**（局部 Z 偏移，米）。空项 = 保留场景里那个位置。</summary>
	[Export] public Godot.Collections.Array<float> StepBoxOffsets { get; set; } = new();

	/// <summary>第 i 段的**判定持续**（秒）。空项 = 用能力节点上的 <c>StrikeActiveTime</c>。</summary>
	[Export] public Godot.Collections.Array<float> StepActiveTimes { get; set; } = new();

	[ExportGroup("连招 · 斩断子弹（按投射物的来源场景名匹配，不是敌我）")]
	/// <summary>
	/// **可以斩断的投射物场景名单**（每项一个场景基名，例如 <c>Bullet</c>）。
	/// 文档：「每段都可以摧毁碰撞盒内的子弹（包括友军的，但**无法斩断能量波**）」。空 = 不斩断任何东西。
	/// </summary>
	[Export] public Godot.Collections.Array<string> CutSceneNames { get; set; } = new();

	/// <summary>
	/// **斩不断的投射物名单**（文档：「无法斩断能量波」→ 写 <c>EnergyWave</c>）。
	/// **是表值，不是代码里的特例分支。**
	/// </summary>
	[Export] public Godot.Collections.Array<string> CutExcludeSceneNames { get; set; } = new();

	/// <summary>连招一共几段（= <see cref="ComboAnimations"/> 的项数；0 = 没配连招 / 退化成单段）。</summary>
	public int StepCount => ComboAnimations.Count;
}
