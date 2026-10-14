using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **技能动画表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/SkillAnimations/`）。
///
/// 它描述的是**一次"从现有素材造一段技能动画"的配方**（不是动画本身）：
/// 从哪个 `.glb` 的哪一段现有动画复制 → 改成什么名字 → 在第几秒挂哪些事件帧 → 存到哪。
/// 真正的动画资源由 <c>Scripts/Editor/SkillAnimationGenerator.cs</c>（<c>[Tool]</c>，编辑器菜单
/// 「项目 → 工具 → 生成技能动画」）按这张表**批量生成**成 `Assets/Animations/<库>/<动画名>.tres`。
///
/// <b>为什么是"表 + 生成器"而不是写死一段代码</b>：用户要求"后续能继续加"，而 13 个 `.glb` 里
/// 只有 10 段可用素材、却有 ~26 段技能动画要造（见 docs/refactor-survey.md §1.5）。
/// 加一段新技能动画 = **加一行表 + 跑一次生成器**，不改任何 C# 代码。
///
/// <b>为什么生成而不是运行期现造</b>：运行期对 `.glb` 里**共享**的 <c>Animation</c> 子资源
/// <c>Duplicate</c> + 加轨道有"串味到同类单位"的风险；生成期落成独立 `.tres` 之后，运行期
/// 每个单位加载到的是**自己实例的一份**（`ResourceLoader` 的缓存也按实例隔离），这条风险就不存在了。
///
/// 数值权威：**这一行**。生成器读不到行时什么都不生成，并写一条明确的警告（不静默）。
/// </summary>
[Tool]
[GlobalClass]
public partial class SkillAnimationConfig : ConfigRow
{
	[ExportGroup("源片段（从哪一段现有动画复制）")]
	/// <summary>
	/// 源模型（`.glb`）。当前所有角色/敌人共用同一套 Kenney Mini Characters 骨架，
	/// 所以角色 A 的源填 <c>res://Assets/Models/player/mesh_player_body.glb</c> 即可。
	/// </summary>
	[Export(PropertyHint.File, "*.glb,*.gltf")] public string SourceModelPath { get; set; } = string.Empty;

	/// <summary>源动画名（必须是该 `.glb` 里真实存在的 32 段之一，例如 <c>holding-right-shoot</c>）。</summary>
	[Export] public string SourceAnimation { get; set; } = string.Empty;

	/// <summary>源模型子树里的 <c>AnimationPlayer</c> 相对路径（`.glb` 实例根下的直接子节点）。</summary>
	[Export] public string SourcePlayerPath { get; set; } = "AnimationPlayer";

	[ExportGroup("目标（生成出来的动画叫什么、放哪）")]
	/// <summary>
	/// 目标动画名（技能动画的稳定名，例如 <c>Skill_A1_ATK</c>）。
	/// 运行期技能行按它查动画（**必须带库名前缀**，见 <see cref="Library"/>）。
	/// </summary>
	[Export] public string TargetName { get; set; } = string.Empty;

	/// <summary>
	/// 动画库名（`AnimationMixer.AddAnimationLibrary` 的那个名字）。
	/// ⚠️ 技能动画一律放进**命名库**（当前 <c>skills</c>），于是运行期的完整路径是
	/// <c>skills/Skill_A1_ATK</c> —— 这与 `.glb` 自带的全局库（空名）天然隔离，
	/// 也不会让"裸名查不到"这个坑影响原有 32 段。
	/// </summary>
	[Export] public string Library { get; set; } = "skills";

	/// <summary>
	/// 生成目录（`res://` 开头，末尾带 <c>/</c>）。运行期单位通过
	/// <see cref="UnitConfig.AnimLibraryDir"/> 指向同一个目录来加载。
	/// </summary>
	[Export(PropertyHint.Dir)] public string OutputDir { get; set; } = "res://Assets/Animations/skills/";

	/// <summary>
	/// 目标时长（秒）。<b>&lt;= 0 = 保持源片段长度</b>。
	/// 拉长会把事件帧时间**一并等比缩放**（"0.08s 那一帧"在拉长后的动画里仍处于同样的相对位置）。
	/// </summary>
	[Export] public float Length { get; set; }

	/// <summary>生成出来的动画是否循环（技能动画一律 false；留这个列是为了让"表驱动"这条纪律完整）。</summary>
	[Export] public bool Loop { get; set; }

	[ExportGroup("事件帧（动画播到第几秒时调 anim_event）")]
	/// <summary>
	/// 事件名数组（与 <see cref="EventTimes"/> **按下标一一对应**，长度不一致时生成器报警告）。
	///
	/// 名字就是文档里的那些帧名：<c>ATK</c> / <c>Dodge</c> / <c>Dodger</c> / <c>Action</c> /
	/// <c>Charge</c> / <c>Release</c> …。运行期技能行用 <see cref="AbilityConfig.AnimEvent"/>
	/// 挑自己关心的那一个。
	/// </summary>
	[Export] public string[] EventNames { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 事件帧时刻（秒，与 <see cref="EventNames"/> 一一对应）。超出目标时长的会被夹回时长内。
	/// 开发期在 Inspector 里微调这两个数组即可，不需要改代码。
	/// </summary>
	[Export] public float[] EventTimes { get; set; } = System.Array.Empty<float>();

	/// <summary>
	/// 事件帧落在**哪个节点**上（相对 <c>AnimationMixer.RootNode</c> 的路径）。
	///
	/// ⚠️ **默认为 <c>".."</c>**：项目里 <c>AnimationPlayer</c> 挂在 <c>Model</c>（`.glb` 实例根）
	/// 的子树里，而 mixer 自己的 <c>root_node</c> 就是 <c>".."</c>（实测），所以从 root_node
	/// 再往上一级（<c>".."</c>）落在 <c>Unit</c> 上 —— 事件方法要挂在 <c>Unit</c>（或它的任意祖先）。
	/// </summary>
	[Export] public string EventTargetPath { get; set; } = "..";

	/// <summary>
	/// 事件方法名（挂在 <see cref="EventTargetPath"/> 上的那个方法）。
	/// 项目的接收方是 <c>UnitAnimator</c>（它在 <c>Unit</c> 下），方法签名 <c>anim_event(string)</c>。
	/// </summary>
	[Export] public string EventMethod { get; set; } = "anim_event";

	/// <summary>补齐后的目标时长：<see cref="Length"/> &gt; 0 就用它，否则用源片段时长。</summary>
	public float ResolveLength(float sourceLength) => Length > 0f ? Length : sourceLength;
}
