using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 升级池表的一行 —— **一个 `.tres` 文件就是一个池**（`Config/Tables/UpgradePools/pool_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.UpgradePools"/>（元素是独立 `ExtResource`）。
///
/// **关联方向（用户 2026-10 拍板，取代 config-schema.md §4.5 / §6.9 的"增幅项反向指武器"方案）**：
/// <code>
/// WeaponConfig（武器表，一行一把武器）
///   └─ Pool ──► UpgradePoolConfig（本类：一行一个池）
///                   ▲
/// RewardConfig（增幅项表，一行一个选项）
///   └─ Pool ────────┘
/// </code>
/// 即：**武器指池、池不指武器**；"哪个选项属于哪个池"写在**选项行**的 `Pool` 列上
/// （一个选项占一行 + 一个引用列 —— 见 docs/config-tooling-yard.md §4.3：一栏里塞多个选项的
/// `Array[StringName]` 恰恰是编辑器拿不到的形态，所以成员关系**不放**在池行里）。
///
/// **这个类是干什么的**：它是"池"的**身份锚点** —— 有了它，武器行与选项行之间才是**资源引用**
/// （改名/删文件时 Godot 会把引用置 null，能检测），而不是拼字符串（拼错会静默失效）。
/// 池行本身**只承载身份**（<see cref="ConfigRow.Id"/> / `DisplayName` / `Note`），
/// **不重复列一遍武器或选项** —— 成员关系只有一个真源，写在选项行上（避免 §1.4-① 那种双真源漂移）。
///
/// 📖 <b>这个类没有自己的参数</b>：它的全部可见字段都继承自 <see cref="ConfigRow"/>
/// （Id / Slot / DisplayName / DescriptionTemplate / Note / Params），在 Inspector 里落在「身份与文案」
/// 与「额外键值对」两个中文分组下。要看"每个参数是干什么的"，读 <see cref="ConfigRow"/> 的注释。
/// </summary>
[Tool]
[GlobalClass]
public partial class UpgradePoolConfig : ConfigRow
{
}
