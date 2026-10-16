using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Run;

/// <summary>独立装备偏好存档，按角色ID记武器ID，避免MetaProgress保存时覆盖。</summary>
public static class LoadoutMemory
{
	private static readonly ConfigFile File = new();
	private static bool _loaded;
	public static bool LastSaveSucceeded { get; private set; } = true;
	public static string SavePath
	{
		get
		{
#if DEBUG
			// 独立探针仍走真实序列化与跨进程读档，只隔离文件名，保护玩家偏好。
			if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--loadout-test") >= 0) return "user://batch_c_loadout_test.cfg";
#endif
			return "user://loadout.cfg";
		}
	}
	private static void Load() { if (_loaded) return; _loaded = true; File.Load(SavePath); }
	public static WeaponConfig? WeaponFor(CharacterConfig character)
	{
		Load();
		var weapon = ConfigRows.ById<WeaponConfig>(File.GetValue("weapons", character.Id, "").AsString());
		return LoadoutSelection.Allows(character, weapon) ? weapon : LoadoutSelection.FirstWeapon(character);
	}
	public static void Remember(CharacterConfig character, WeaponConfig weapon)
	{
		if (!LoadoutSelection.Allows(character, weapon)) return;
		Load();
		File.SetValue("selection", "character", character.Id);
		File.SetValue("weapons", character.Id, weapon.Id);
		Error result = File.Save(SavePath);
		LastSaveSucceeded = result == Error.Ok;
		if (!LastSaveSucceeded) GD.PushWarning($"[Loadout] 装备已切换，但偏好存档失败：{result}");
	}
	public static void Restore()
	{
		Load();
		CharacterConfig? character = ConfigRows.ById<CharacterConfig>(File.GetValue("selection", "character", "").AsString())
			?? RunController.CharacterRow(RunSetup.Character);
		if (character == null || WeaponFor(character) is not { } weapon) return;
		RunSetup.Character = (PlayerCharacterKind)(character.Slot - 1);
		RunSetup.Weapon = (WeaponPresetKind)(weapon.Slot - 1);
	}
}
