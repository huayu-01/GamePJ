using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 所有配置表行的基类（设计文档 config-schema.md §3）。
///
/// **一个 `.tres` 文件 = 一行**，放在 `Config/Tables/&lt;分组&gt;/`；一个文件夹 = 一张表。
/// 索引挂在 <see cref="BalanceConfig"/> 的 <c>[Export] XxxConfig[]</c> 数组上（元素是独立 `ExtResource`）。
///
/// 每个字段都必须有 C# 属性初始化器当默认值 —— 这就是"读不到表就用自身默认值"里的那个默认值。
///
/// 📖 <b>参数中文说明怎么看</b>：Godot 4.7 的 C# 导出**没有** tooltip/描述机制（`ExportAttribute` 只有
/// <c>Hint</c> + <c>HintString</c>，Inspector 的悬停提示只认引擎类文档与**有** <c>get_documentation()</c>
/// 的脚本语言，而 <c>CSharpScript</c> 那两处是 <c>// TODO</c> 空实现）。所以本项目的中文说明走两条路：
/// ① <b>本条与下面每个字段的 <c>/// &lt;summary&gt;</c> 中文注释</b>（IDE 里鼠标停上去就能看，读代码时就在手边）；
/// ② <b><c>[ExportGroup("中文分组 · 一句话说明")]</c></b> —— 编辑器右侧 Inspector 里**真的能看到中文标题**
/// （每个参数都落在某个中文标题下面）。改字段名会破坏另外两个并行代理正在用的东西，所以只加说明、不改名。
/// </summary>
public abstract partial class ConfigRow : Resource, IConfigRow
{
	[ExportGroup("身份与文案（所有表通用）")]
	/// <summary>稳定键（小写 snake_case，与行文件名同语义名）。一次定好不再改。用途：关卡白名单、互斥列表、日志与自测。</summary>
	[Export] public string Id { get; set; } = string.Empty;

	/// <summary>槽位号（1 起；0 = 不进槽位）。用于按 "武器1/武器2/角色1/角色2" 这种表现层顺序寻址。</summary>
	[Export] public int Slot { get; set; }

	/// <summary>界面标题 / 卡片名（**只影响界面与日志**，不要拿它当身份键 —— 身份键是 <see cref="Id"/>）。</summary>
	[Export] public string DisplayName { get; set; } = string.Empty;

	/// <summary>说明模板，支持 <c>{字段名}</c> 与 <c>{Params 键}</c> 占位符（见 <see cref="ConfigText"/>）。</summary>
	[Export(PropertyHint.MultilineText)] public string DescriptionTemplate { get; set; } = string.Empty;

	/// <summary>策划备注（也可以被 <see cref="ConfigText"/> 当占位符来源）。只给人看，代码不读。</summary>
	[Export(PropertyHint.MultilineText)] public string Note { get; set; } = string.Empty;

	[ExportGroup("额外键值对（逃生口：没有独立列的参数走这里）")]
	/// <summary>
	/// 额外键值对（多数参数项能通用的情况则使用通用项；子类各不相同的参数走这里）。
	/// 与 <c>GodotObject.Set</c> 配合可以套到任意组件的同名 <c>[Export]</c> 上。
	/// <c>Key</c> = 目标组件的属性名，<c>Value</c> = 值，<c>Note</c> = 这一条是干什么的。
	/// </summary>
	[Export] public ParamEntry[] Params { get; set; } = System.Array.Empty<ParamEntry>();

	/// <summary>把行套用到目标对象。<b>读不到行时根本不会调用它</b> —— 目标保留自身默认值。</summary>
	public virtual void ApplyTo(Node target)
	{
	}

	public override string ToString()
		=> string.IsNullOrEmpty(DisplayName) ? $"{Id}({GetType().Name})" : $"{Id}·{DisplayName}";
}
