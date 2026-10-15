using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 角色表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Characters/chr_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Characters"/>。
///
/// 角色也是"单位"：生命/护甲/移速/减伤/攻击组/持有技能/类标签都继承 <see cref="UnitConfig"/>，
/// 这里只加角色私有的列（武器开关、默认武器、能力开关矩阵）。
///
/// 取代 `RunController.ApplyBalance` 里那串 <c>heavy ? A : summoner ? B : C</c> 三元链与
/// 硬编码的能力开关矩阵（原 `ConfigIndex.tres` 的 <c>Player*</c> / <c>Heavy*</c> / <c>Summoner*</c> 已搬到这里）。
/// </summary>
[Tool]
[GlobalClass]
public partial class CharacterConfig : UnitConfig
{
	// ⚠️ P6a **删掉了** `WeaponEnabled`（"这个角色拿不拿枪 / 近战重装收起枪"）：用户 2026-10-15 明确
	// 「"出征时按角色行把枪收起来"，**我并没有这么设计过**，因为流程定的就是：在基地 > 默认配置/选好配置 >
	// 进关卡 > 完成最终首领/esc 点重开 > 回基地，**相当于只是切场景**」。
	// 正确的归属是**武器行**：一把武器要么由 `Weapon` 组件开火、要么由武技驱动 ——
	// 见 `WeaponConfig.UsesWeaponComponent`（6 把新武器全是"武技驱动"）。
	// 从角色行读它还会造成"基地预览把枪藏起来、关卡里枪又出现"的不一致（与"只是切场景"矛盾）。

	// ⚠️ 刻意**删掉了** DefaultWeaponId：它从阶段 2 起就是"填了没人读"的列（开局武器由
	// `StartScreen` 的选择 → `RunSetup.Weapon` 决定），留着只会让人以为能配"这个角色的默认枪"。

	[ExportGroup("能力开关矩阵（取代硬编码的 if/else）")]
	/// <summary>
	/// 要让哪些 <c>Ability</c> 处于**启用**状态的名单。匹配的是**脚本类名**
	/// （`MeleeAttackAbility` / `SummonAbility` / `AreaAttackAbility`）——
	/// `RunController.ApplyAbilityMatrix` 按 <c>ability.GetType().Name</c> 比对，因为技能表落地后
	/// <c>Ability.AbilityId</c> 会变成技能行的 Id（`skl_melee` 这种）。用类名在"表在/表不在"两种情况下都成立。
	/// </summary>
	[Export] public string[] EnabledAbilityIds { get; set; } = System.Array.Empty<string>();

	/// <summary>要让哪些 <c>Ability</c> 处于**关闭**状态的名单（同样写类名）。两个名单之外的保持场景里的原状。</summary>
	[Export] public string[] DisabledAbilityIds { get; set; } = System.Array.Empty<string>();

	[ExportGroup("跳跃（只有玩家读这两列，见 CharacterMovement.ApplyJumpConfigDeferred）")]
	/// <summary>
	/// **起跳速度的固定值**（m/s，直接套到 <c>CharacterMovement.JumpVelocity</c>，不做任何倍率/平方根运算）。
	///
	/// 用户 2026-10-12 拍板（需求 A2）：「要的是**直接改固定值**哦不是做**逻辑运算**，就是觉得之前
	/// **跳跃的过程太漫长，起跳速度也要快**」—— 所以这里就是"起跳那一瞬间的垂直速度"本身，
	/// 填多少就是多少（场景里的 <c>Player.tscn → Movement.JumpVelocity</c> 只是**读不到表时的回退值**）。
	///
	/// ⚠️ 跳跃高度与起跳速度是平方关系（<c>h = v² / 2g</c>）：起跳速度翻倍 → 高度 ×4，
	/// 而且**在重力不变时滞空时间也会翻倍**（反而更"漫长"）。所以旁边还有
	/// <see cref="JumpGravityScale"/> 一起给固定值 —— 想要"起跳更快、过程更短"就要两个一起调。
	/// </summary>
	[Export(PropertyHint.Range, "1,30,0.1")] public float JumpVelocity { get; set; } = 6.5f;

	/// <summary>
	/// **重力缩放的固定值**（直接套到 <c>CharacterMovement.GravityScale</c>；1 = 引擎默认重力
	/// 9.8 × 1，1.8 = 组件原来的值）。留 0 = 不覆盖（保留场景里的值）。
	///
	/// 它决定"落回来有多快"：起跳速度抬上去之后，只有把重力也抬上去，整个跳跃过程才会**更短**
	/// （用户抱怨的就是"过程太漫长"）。两列都是固定值，没有任何倍率运算。
	/// </summary>
	[Export(PropertyHint.Range, "0,10,0.05")] public float JumpGravityScale { get; set; } = 1.8f;

	[ExportGroup("奔跑（P6d 新增；只有玩家读这两列，见 SprintController）")]
	/// <summary>
	/// **奔跑键的动作名**（`project.godot` 的 `[input]`）。
	///
	/// <para>设计文档 `角色设计.txt` 第 19 行把奔跑列在「所有角色通用的机制」里：
	/// 「奔跑(只有在移动状态时才可以按住 shift 键进入，移动的更快，释放其他任何状态或技能都会退出奔跑)」——
	/// 所以这是**表列**而不是代码里的字面量（用户点名的纪律：按键必须进表，P1 时为硬编码栽过）。</para>
	///
	/// <para>留空 = 用组件自己的 <c>[Export]</c> 默认值（`sprint`）。</para>
	/// </summary>
	[Export] public string SprintInputAction { get; set; } = string.Empty;

	/// <summary>
	/// **奔跑时的移速倍率**（2 = 移速翻倍）。**≤0 = 不覆盖**（保留场景导出值 1 = 不加速）。
	///
	/// <para>文档只写了「移动的更快」，没给数字 —— 表里填的是**预设值**，行 `Note` 里注明。</para>
	/// </summary>
	[Export] public float SprintSpeedMultiplier { get; set; }
}
