using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 角色表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Characters/chr_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Characters"/>。
///
/// 角色也是"单位"：生命/护甲/移速/减伤/攻击组/持有技能/类标签都继承 <see cref="UnitConfig"/>，
/// 这里只加角色私有的列（武器开关、默认武器、能力开关矩阵）。
///
/// 取代 `RunController.ApplyBalance` 里那串 <c>heavy ? A : summoner ? B : C</c> 三元链与
/// 硬编码的能力开关矩阵（原 `Balance.tres` 的 <c>Player*</c> / <c>Heavy*</c> / <c>Summoner*</c> 已搬到这里）。
/// </summary>
[Tool]
[GlobalClass]
public partial class CharacterConfig : UnitConfig
{
	[ExportGroup("武器")]
	/// <summary>这个角色能不能用枪（近战重装 = false，左键改由近战能力接管）。</summary>
	[Export] public bool WeaponEnabled { get; set; } = true;

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
}
