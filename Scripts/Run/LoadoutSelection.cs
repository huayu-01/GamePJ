using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Run;

/// <summary>角色与武器的归属校验；界面、交互物及场景初始化使用同一个数据源。</summary>
public static class LoadoutSelection
{
	public static bool Allows(CharacterConfig? character, WeaponConfig? weapon)
		=> character != null && weapon?.OwnerCharacter?.Id == character.Id;
	public static WeaponConfig? FirstWeapon(CharacterConfig character)
	{
		foreach (WeaponConfig row in ConfigIndex.Settings.Weapons) if (Allows(character, row)) return row;
		return null;
	}
	public static bool SelectWeapon(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		if (!Allows(RunController.CharacterRow(character), RunController.WeaponRow(weapon))) return false;
		RunSetup.Character = character;
		RunSetup.Weapon = weapon;
		RunSetup.HasSelection = true;
		LoadoutMemory.Remember(RunController.CharacterRow(character)!, RunController.WeaponRow(weapon)!);
		return true;
	}
	public static bool SelectCharacter(CharacterConfig character)
	{
		WeaponConfig? weapon = LoadoutMemory.WeaponFor(character);
		return weapon != null && SelectWeapon((PlayerCharacterKind)(character.Slot - 1), (WeaponPresetKind)(weapon.Slot - 1));
	}
}
