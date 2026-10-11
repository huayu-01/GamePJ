using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 角色表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Characters/chr_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Characters"/>。
///
/// 取代 `RunController.ApplyBalance` 里那串 <c>heavy ? A : summoner ? B : C</c> 三元链与
/// 硬编码的能力开关矩阵（原 `Balance.tres` 的 <c>Player*</c> / <c>Heavy*</c> / <c>Summoner*</c> 已搬到这里）。
/// </summary>
[GlobalClass]
public partial class CharacterConfig : ConfigRow
{
	[ExportGroup("基础数值")]
	[Export] public float MaxHealth { get; set; } = 160f;

	[Export] public float MaxArmor { get; set; } = 40f;

	/// <summary>初始护盾（当前三行都是 0；护盾数值由 <c>Unit.ApplyBaseStats</c> 统一套用）。</summary>
	[Export] public float MaxShield { get; set; }

	[Export] public float MoveSpeed { get; set; } = 7f;

	[Export(PropertyHint.Range, "0,0.9,0.01")] public float DamageReduction { get; set; }

	[ExportGroup("武器")]
	/// <summary>这个角色能不能用枪（近战重装 = false，左键改由近战能力接管）。</summary>
	[Export] public bool WeaponEnabled { get; set; } = true;

	/// <summary>开局默认拿的武器 Id（对应 <c>Config/Tables/Weapons/</c> 里的行；空 = 不动）。</summary>
	[Export] public string DefaultWeaponId { get; set; } = string.Empty;

	[ExportGroup("能力开关矩阵（取代硬编码的 if/else）")]
	/// <summary>
	/// 要让哪些 <c>Ability</c> 处于**启用**状态的 Id 列表。Id 取 <c>Ability.AbilityId</c>，
	/// 而 `Ability.cs` 在它为空时会自动填**脚本类名**（`MeleeAttackAbility` / `SummonAbility` /
	/// `AreaAttackAbility`），所以这里写类名即可，不必去改场景。
	/// </summary>
	[Export] public string[] EnabledAbilityIds { get; set; } = System.Array.Empty<string>();

	/// <summary>要让哪些 <c>Ability</c> 处于**关闭**状态的 Id 列表。两个名单之外的保持场景里的原状。</summary>
	[Export] public string[] DisabledAbilityIds { get; set; } = System.Array.Empty<string>();

	[ExportGroup("角色专属参数")]
	/// <summary>左键近战重击的伤害（只有近战重装用；范围/冷却仍在 `Player.tscn` 的 `AbilityMelee` 上）。</summary>
	[Export] public float MeleeDamage { get; set; } = 32f;

	/// <summary>
	/// 把行套到玩家身上：基础属性 + 额外减伤。**只做这一件事** ——
	/// 元进度加成、能力开关矩阵、武器套用仍由 `RunController.ApplyBalance` 编排，
	/// 这样"读表"与"流程"保持分离，回退路径也只有一个（读不到行 → 这一整段不执行）。
	/// </summary>
	public override void ApplyTo(Node target)
	{
		if (target is not Unit unit)
		{
			return;
		}

		unit.ApplyBaseStats(MaxHealth, MaxArmor, MaxShield, MoveSpeed);
		unit.DamageReduction = DamageReduction;
	}
}
