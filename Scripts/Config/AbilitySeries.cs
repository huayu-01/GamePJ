using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **技能的一串表参数**（P4 新增）—— 连招的"每段动画名 / 每段命中盒 / 每段伤害倍率 / 斩断子弹的名单"
/// 这类**数组型表参数**的落点。
///
/// <para><b>为什么不是技能行上的一个数组列</b>（实测 Godot 4.7.2 + 本项目的 C# 绑定）：</para>
/// <list type="bullet">
/// <item><c>[Export] float[]</c> / <c>string[]</c> 编译不过（<c>GD0102</c>：托管数组不可导出）；</item>
/// <item><c>PackedFloat32Array</c> / <c>PackedStringArray</c> 在 C# 侧没有公开类型；</item>
/// <item><c>Godot.Collections.Array&lt;T&gt;</c>、<c>T[]</c>、以及"多个 <c>[Export] Resource</c> 字段"
/// 在运行期**读不回来**（实测：`Count` 读得到、每一项都是 <c>null</c>）。</item>
/// </list>
///
/// <para><b>所以路线是"一个字段装一整串文本"</b>：<see cref="Entries"/> 里用
/// <c>名=值;名=值</c> 的形式把每一项写下来（分隔符 <see cref="PairSeparator"/> / <see cref="ValueSeparator"/>），
/// 由 <see cref="ParseFloats"/> / <see cref="ParseTexts"/> / <see cref="ParseVectors"/> 读成数组。
/// 一个 <c>[Export] string</c> 是**任何版本的 Godot 都认得**的类型 —— 这条路在"编辑器里的 C# 程序集
/// 是旧的"这种情况下也不会失效（新的导出属性在旧程序集里根本不存在）。</para>
///
/// <para>文件放 `Config/Tables/SkillSeries/&lt;技能行 Id&gt;.tres`，技能按 <see cref="ConfigRows"/>
/// 的路径约定读它 —— **技能行不需要多一列**（连招与"某个技能"是一对一的）。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilitySeries : ConfigRow
{
	/// <summary>项与项之间的分隔符。</summary>
	public const char PairSeparator = ';';

	/// <summary>名与值之间的分隔符。</summary>
	public const char ValueSeparator = '=';

	[ExportGroup("一串参数（一个字段装一整串文本，见类注释）")]
	/// <summary>
	/// 这一串的名字 → 它自己的那串值（例如 <c>ComboAnimations</c> → <c>"Skill_B1_Sword1|Skill_B1_Sword2|…"</c>）。
	///
	/// <para>格式：<c>名=值1|值2|值3;名2=…</c>（项内用 <see cref="ValueSeparator"/>，
	/// 项间用 <see cref="PairSeparator"/>，值之间用 <c>|</c>）。</para>
	/// </summary>
	[Export(PropertyHint.MultilineText)] public string Entries { get; set; } = string.Empty;

	/// <summary>这一串里每一项的类型说明（<c>float</c> / <c>string</c> / <c>vector3</c>；只给人看与自测读）。</summary>
	[Export] public string ItemType { get; set; } = "float";

	/// <summary>值之间的分隔符（默认 <c>|</c>）。</summary>
	[Export] public string ValueSeparatorText { get; set; } = "|";

	// ─────────────────────────────────────────────────────────────────────────
	// P8 第 0 步：**候选的"正规数组"列**（只加不删，探针见 docs/abilities.md §11.1）。
	// 探针要回答的唯一问题：这些 `Godot.Collections.Array<T>` 列能不能**导出 + 读回来**。
	// 结论：能（探针①②③ 全绿）→ 下一步（P8-A）就把上面那串 `Entries` 文本 + 解析器删掉。
	// ─────────────────────────────────────────────────────────────────────────
	[ExportGroup("连招 · 每一段一段动画（P8：正规数组列）")]
	/// <summary>第 1..N 段的技能动画名（<c>Skill_B1_Sword1</c>…）。</summary>
	[Export] public Godot.Collections.Array<string> ComboAnimations { get; set; } = new();

	/// <summary>开输入窗口的那个事件帧名（默认 <c>ATK</c>；留空 = 不开窗口，退化成单段）。</summary>
	[Export] public string ComboNextEvent { get; set; } = "ATK";

	/// <summary>输入窗口时长（秒；<b>&lt;= 0 = 不覆盖</b>，保留能力节点上的值）。</summary>
	[Export] public float ComboWindowSeconds { get; set; } = 0.45f;

	/// <summary>打满最后一段之后是否回到第 0 段（长枪「横扫千军」= true）。</summary>
	[Export] public bool ComboLoop { get; set; }

	[ExportGroup("连招 · 每段的判定（一项对应一段；不足时用最后一项或场景基线）")]
	/// <summary>第 i 段的伤害倍率（乘在命中盒的 <c>Damage</c> 上）。</summary>
	[Export] public Godot.Collections.Array<float> StepDamageScales { get; set; } = new();

	/// <summary>第 i 段的命中盒尺寸（米；x 宽 / y 高 / z 长；<c>z = 0</c> 且 <c>x == y</c> → 圆柱）。</summary>
	[Export] public Godot.Collections.Array<Vector3> StepBoxSizes { get; set; } = new();

	/// <summary>第 i 段的命中盒前伸（局部 Z 偏移，米）。</summary>
	[Export] public Godot.Collections.Array<float> StepBoxOffsets { get; set; } = new();

	/// <summary>第 i 段的判定持续（秒）。</summary>
	[Export] public Godot.Collections.Array<float> StepActiveTimes { get; set; } = new();

	[ExportGroup("连招 · 斩断子弹（按投射物的来源场景名匹配）")]
	/// <summary>可以斩断的投射物来源场景名（<c>Bullet</c>）。</summary>
	[Export] public Godot.Collections.Array<string> CutSceneNames { get; set; } = new();

	/// <summary>斩不断的名单（<c>EnergyWave</c> —— 文档：「无法斩断能量波」，是数据不是代码分支）。</summary>
	[Export] public Godot.Collections.Array<string> CutExcludeSceneNames { get; set; } = new();

	/// <summary>把 <see cref="Entries"/> 解析成"名 → 原始值数组"。</summary>
	public Dictionary<string, string[]> Parse()
	{
		var map = new Dictionary<string, string[]>();
		if (string.IsNullOrEmpty(Entries))
		{
			return map;
		}

		foreach (string pair in Entries.Split(PairSeparator))
		{
			if (pair.Length == 0)
			{
				continue;
			}

			int index = pair.IndexOf(ValueSeparator);
			if (index <= 0)
			{
				continue;
			}

			string key = pair.Substring(0, index).Trim();
			string rest = pair.Substring(index + 1);
			string[] values = rest.Split(string.IsNullOrEmpty(ValueSeparatorText) ? "|" : ValueSeparatorText);
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = values[i].Trim();
			}

			map[key] = values;
		}

		return map;
	}

	/// <summary>取某一串的原始值（找不到返回空数组）。</summary>
	public string[] Raw(string key) => Parse().TryGetValue(key, out string[]? values) ? values : System.Array.Empty<string>();

	/// <summary>取某一串并转成浮点数组。</summary>
	public List<float> ParseFloats(string key)
	{
		var values = new List<float>();
		foreach (string raw in Raw(key))
		{
			values.Add(float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : 0f);
		}

		return values;
	}

	/// <summary>取某一串并转成字符串数组。</summary>
	public List<string> ParseTexts(string key)
	{
		var values = new List<string>();
		foreach (string raw in Raw(key))
		{
			values.Add(raw);
		}

		return values;
	}

	/// <summary>取某一串并转成 Vector3 数组（每项写成 <c>2.2,1.6,2.8</c>）。</summary>
	public List<Vector3> ParseVectors(string key)
	{
		var values = new List<Vector3>();
		foreach (string raw in Raw(key))
		{
			values.Add(ParseVector3(raw));
		}

		return values;
	}

	/// <summary>把 <c>"(2.2, 1.6, 2.8)"</c> / <c>"2.2,1.6,2.8"</c> / <c>"2.2 1.6 2.8"</c> 解析成 Vector3。</summary>
	public static Vector3 ParseVector3(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return Vector3.Zero;
		}

		string cleaned = text.Replace("Vector3", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);
		string[] parts = cleaned.Split(',', ' ');
		var numbers = new List<float>();
		foreach (string part in parts)
		{
			if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
			{
				numbers.Add(value);
			}
		}

		return numbers.Count >= 3 ? new Vector3(numbers[0], numbers[1], numbers[2]) : Vector3.Zero;
	}
}
