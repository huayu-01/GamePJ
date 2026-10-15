using ActRoguelike.Config;

namespace ActRoguelike.Run;

/// <summary>
/// 基地里选好的出征配置（用户需求：基地场景 + 交互台 + 出击门切场景）。
/// 跨场景传递：基地的配置台写入，关卡场景的 RunController 读取。
/// 有选过武器就代表"从基地出发"，关卡会直接开局、不再弹 Base/Configure 界面。
/// </summary>
public static class RunSetup
{
	/// <summary>基地里选定的角色。</summary>
	public static PlayerCharacterKind Character { get; set; } = PlayerCharacterKind.Hunter;

	/// <summary>基地里选定的武器。</summary>
	public static WeaponPresetKind Weapon { get; set; } = WeaponPresetKind.EnergyPalm;

	/// <summary>
	/// 是否已经在基地里做过配置（true = 出击门按过 / 命令行旁路指定过）。
	/// ⚠️ P6a 之后它**只用于日志留痕**：进关卡就是切场景，`RunController` 一律按
	/// <see cref="Character"/> / <see cref="Weapon"/> 开局（不再有"没选过就先弹出征面板"那道门）。
	/// </summary>
	public static bool HasSelection { get; set; }

	/// <summary>回到基地时清空（下次出征重新配置）。</summary>
	public static void Clear() => HasSelection = false;
}
