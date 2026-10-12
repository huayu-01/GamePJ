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
	[ExportGroup("武器（这个角色拿不拿枪）")]
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

	[ExportGroup("跳跃（只有玩家读这两列，见 CharacterMovement.ApplyJumpConfig）")]
	/// <summary>
	/// **跳跃高度**倍数（1 = 与 `Player.tscn` 的 <c>Movement.JumpVelocity</c> 一样高）。
	///
	/// 高度与起跳速度是平方关系（<c>h = v² / 2g</c>），所以实现里对速度取的是**平方根**：
	/// 这一列填 1.4 = 跳得比原来高 40%，起跳速度 ×1.183。
	///
	/// ⚠️ 它**只管高度**：真正决定"跳起来多快"的是 <see cref="JumpSpeedMultiplier"/>。
	/// 用户原话是「高度 +40%，速度 +100%」—— 两者在同一个 <c>JumpVelocity</c> 上无法同时成立
	/// （速度 ×2 会让高度 ×4），所以拆成两个旋钮，谁想改哪一边就改哪一边。
	/// </summary>
	[Export(PropertyHint.Range, "0.1,6,0.05")] public float JumpHeightMultiplier { get; set; } = 1f;

	/// <summary>
	/// **跳跃速度**倍数（1 = 与场景里的值一样快）。乘在"空中加速"与"最大下落速度"上
	/// （<c>CharacterMovement.AirAcceleration</c> / <c>MaxFallSpeed</c>）——
	/// 这两个量才真的是速度：起跳速度已经被"高度"那一列按平方根占用了。
	///
	/// 起跳那一瞬间的手感（离开地面的速度）由**高度**列决定，所以想"整体更快"时两列一起加。
	/// </summary>
	[Export(PropertyHint.Range, "0.1,6,0.05")] public float JumpSpeedMultiplier { get; set; } = 1f;
}
